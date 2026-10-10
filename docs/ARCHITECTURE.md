# Arquitetura

## Princípios

1. **Local primeiro.** O caminho por omissão não faz chamadas de rede. Cloud é um adaptador opcional.
2. **O núcleo não conhece o Windows.** `FlowSpeech.Core` é `net8.0` puro e testável em qualquer SO.
   Tudo o que toca Win32, áudio ou motores de transcrição vive atrás de interfaces.
3. **A ditação nunca bloqueia.** Etapas opcionais (histórico) degradam graciosamente:
   se falharem, o texto segue sem elas.
4. **Latência é a funcionalidade.** Modelo pré-carregado, limpeza determinística instantânea,
   paste em vez de digitação.

## Dependências entre projetos

```
            FlowSpeech.App (WPF, composition root)
              /               |                \
 Platform.Windows      Transcription            │
              \               |                /
              FlowSpeech.Core (abstrações + domínio)
```

## Abstrações (`FlowSpeech.Core.Abstractions`)

| Interface | Implementação atual | Alternativas |
|---|---|---|
| `IHotkeyListener` | `LowLevelHotkeyListener` (WH_KEYBOARD_LL) | Raw Input, atalhos com modificadores |
| `IAudioRecorder` | `NAudioRecorder` (WaveIn 16 kHz mono) | WASAPI com escolha de dispositivo, VAD |
| `ISpeechToText` | `ParakeetSpeechToText` (sherpa-onnx) | `WhisperLocalSpeechToText`, `CloudSpeechToText` |
| `ITextFormatter` | `RuleBasedFormatter` | Regras por estilo/app |
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
3. Transcrição com o motor configurado. O Parakeet deteta a língua e já devolve pontuação e maiúsculas;
   Whisper e cloud recebem o dicionário pessoal como *prompt*.
4. Limpeza por regras: hesitações, pontuação falada, dicionário, snippets, espaçamento, maiúsculas.
5. Injeção: guarda o clipboard, cola, espera 250 ms, repõe o clipboard.

## Decisões técnicas

- [ADR-0001 — Stack .NET 8 + WPF](adr/0001-stack.md)
- [ADR-0002 — Inserção de texto via clipboard](adr/0002-text-injection.md)
- [ADR-0003 — Motor de transcrição: Parakeet local](adr/0003-stt-local-first.md)

## Threading

- O hook de teclado é instalado na thread de UI (precisa de message loop) e devolve de imediato;
  os eventos são publicados com `SynchronizationContext.Post`.
- O `WaveInEvent` é criado sem `SynchronizationContext` para os buffers chegarem na thread de áudio
  e o `Stop()` poder esperar pelo último buffer sem deadlock.
- O clipboard WPF requer STA → `ClipboardTextInjector` usa o `Dispatcher`.
- A transcrição corre em background (`Task.Run`); a UI só recebe mudanças de estado.
