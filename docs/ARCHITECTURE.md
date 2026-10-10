# Arquitetura

## Princípios

1. **Local primeiro.** O caminho por omissão não faz chamadas de rede. Cloud é um adaptador opcional.
2. **O núcleo não conhece o Windows.** `FlowPT.Core` é `net8.0` puro e testável em qualquer SO.
   Tudo o que toca Win32, áudio, Whisper ou Ollama vive atrás de interfaces.
3. **A ditação nunca bloqueia.** Cada etapa opcional (LLM, histórico) degrada graciosamente:
   se falhar, o texto segue sem ela.
4. **Latência é a funcionalidade.** Modelo pré-carregado, regras determinísticas antes do LLM,
   timeout curto no LLM, paste em vez de digitação.

## Dependências entre projetos

```
              FlowPT.App (WPF, composition root)
             /        |           |          \
 Platform.Windows  Transcription  Formatting  │
             \        |           |          /
                   FlowPT.Core (abstrações + domínio)
```

## Abstrações (`FlowPT.Core.Abstractions`)

| Interface | Implementação atual | Alternativas futuras |
|---|---|---|
| `IHotkeyListener` | `LowLevelHotkeyListener` (WH_KEYBOARD_LL) | Raw Input, atalhos com modificadores |
| `IAudioRecorder` | `NAudioRecorder` (WaveIn 16 kHz mono) | WASAPI com escolha de dispositivo, VAD |
| `ISpeechToText` | `WhisperLocalSpeechToText` | `CloudSpeechToText` (OpenAI/Groq/Azure), streaming |
| `ITextFormatter` | `RuleBasedFormatter` → `OllamaFormatter` | LLM embebido (LLamaSharp), ONNX |
| `ITextInjector` | `ClipboardTextInjector` (Paste/Type) | UI Automation `ValuePattern` |
| `IActiveAppProvider` | `ForegroundAppProvider` | Ler texto à volta do cursor (contexto) |
| `IHistoryStore` | `JsonlHistoryStore` | SQLite com pesquisa |

## Fluxo de uma ditação

```
Idle ──BeginRecording──► Recording ──EndRecording──► Transcribing ──► Formatting ──► Injecting ──► Idle
  ▲                          │                                                                      │
  └──────── Cancel ──────────┘                       (qualquer exceção) ──► Error ──────────────────┘
```

1. `BeginRecording` captura a app em primeiro plano **antes** de qualquer UI nossa aparecer,
   e escolhe o `WritingStyle` a partir de `AppStyles`.
2. Clips mais curtos que `MinRecordingMs` são descartados (toques acidentais e 1.º toque do duplo-toque).
3. O dicionário pessoal é enviado ao Whisper como *initial prompt*, o que melhora muito a grafia
   de nomes próprios e jargão (ex.: "Gaião", "FlowPT").
4. Formatadores correm em cadeia: regras (sempre, instantâneo) e depois LLM (opcional, com timeout).
5. Injeção: guarda o clipboard, cola, espera 250 ms, repõe o clipboard.

## Decisões técnicas

- [ADR-0001 — Stack .NET 8 + WPF + whisper.cpp](adr/0001-stack.md)
- [ADR-0002 — Inserção de texto via clipboard](adr/0002-text-injection.md)
- [ADR-0003 — STT local ativo, cloud apenas como adaptador](adr/0003-stt-local-first.md)

## Threading

- O hook de teclado é instalado na thread de UI (precisa de message loop) e devolve de imediato;
  os eventos são publicados com `SynchronizationContext.Post`.
- O `WaveInEvent` é criado sem `SynchronizationContext` para os buffers chegarem na thread de áudio
  e o `Stop()` poder esperar pelo último buffer sem deadlock.
- O clipboard WPF requer STA → `ClipboardTextInjector` usa o `Dispatcher`.
- Whisper e Ollama correm em background (`async`); a UI só recebe mudanças de estado.
