# SpeechDesk

[English](README.md) · **Português**

[![build](https://github.com/fmrgcarvalho/speechdesk/actions/workflows/build.yml/badge.svg)](https://github.com/fmrgcarvalho/speechdesk/actions/workflows/build.yml)

Ditado por voz para Windows, **100% local e gratuito**.
Mantém uma tecla premida, fala, larga: o texto aparece onde está o cursor, em qualquer aplicação
(Outlook, Teams, Slack, VS Code, browser…). Nenhum áudio sai da máquina.

**Página do projeto, com demonstração interativa:** https://fmrgcarvalho.github.io/speechdesk/ ([versão em inglês](https://fmrgcarvalho.github.io/speechdesk/en/))

> **Estado: em desenvolvimento (v0.1).** Ainda não há instalador — compila-se a partir do código.
> O núcleo tem testes automáticos; a parte Windows e os motores de IA estão implementados e a ser
> validados em máquinas reais. Problemas e sugestões: [Issues](https://github.com/fmrgcarvalho/speechdesk/issues).

## Atalhos

| Atalho | O que faz |
|---|---|
| Manter **Ctrl direito** premido | Grava enquanto a tecla está premida; ao largar, escreve o texto |
| **Dois toques** no Ctrl direito | Modo mãos-livres: grava sem segurar a tecla |
| **Um toque** no Ctrl direito (em mãos-livres) | Pára e escreve o texto |
| **Esc** | Cancela a gravação |
| Ctrl direito **+ outra tecla** (ex.: Ctrl+C) | Cancela — é um atalho normal |

A **limpeza com IA** e a **tradução para inglês** ligam-se e desligam-se no menu do ícone junto ao relógio
(botão direito). A tecla muda-se em `Hotkey.VirtualKey` nas definições: `163` = Ctrl direito (omissão),
`145` = Scroll Lock, `165` = Alt direito (evitar em teclado português — é o AltGr do @ e do €).

## Como funciona

O que acontece por baixo:

```
 Tecla global ──► Microfone 16 kHz ──► Parakeet TDT 0.6B v3 ──► [Limpeza com IA] ──► Regras ──► Ctrl+V na app ativa
 (hook de teclado)    (NAudio)          (sherpa-onnx, local)     opcional, local     dicionário,  (clipboard reposto)
                                                                 Qwen 2.5 1.5B       snippets
```

## Motor de transcrição

Por omissão o SpeechDesk usa o **NVIDIA Parakeet TDT 0.6B v3** (licença CC-BY-4.0), executado localmente
com [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx):

- 25 línguas europeias, incluindo **português (treinado com português europeu)** e inglês
- deteção automática da língua — podes alternar PT/EN sem mudar nada
- pontuação e maiúsculas já vêm do modelo
- muito rápido em CPU (versão int8), sem precisar de GPU

O **Whisper large-v3-turbo** (whisper.cpp) continua disponível como alternativa — útil para línguas fora
das 25 suportadas — e há um adaptador para APIs cloud compatíveis com OpenAI, desligado por omissão.

## Falar em português, escrever em inglês (opcional)

No menu do ícone → **Traduzir para inglês**. Falas em português e o texto é escrito em inglês na app
onde estás. Aparece cerca de 1–2 s depois de largares a tecla, frase a frase (o overlay mostra **→ EN**).

| Motor ativo | Quem traduz | Precisa de |
|---|---|---|
| Whisper | O próprio Whisper (tarefa *translate* nativa) | Só o modelo Whisper |
| Parakeet (omissão) | O modelo de IA local (Qwen 2.5), depois de transcrever | `download-model.ps1 -Engine ai` |

Com o modelo de IA a língua de destino pode ser outra (`Translation.TargetLanguage`, ex.: `"French"`);
o Whisper só traduz para inglês. Se a tradução falhar ou demorar mais de `Translation.TimeoutSeconds`,
o texto é escrito no original e aparece um aviso — nunca passa português despercebido.
Os snippets são procurados no texto final: com a tradução ligada, cria gatilhos em inglês (ex.: `"my email"`).

## Limpeza com IA (opcional)

Podes escolher entre dois modos, no menu do ícone → **Limpar texto com IA**:

| Modo | O que faz | Velocidade |
|---|---|---|
| **Sem IA** (omissão) | Regras: tira "hum"/"ãh", pontuação ditada, dicionário, snippets | Instantâneo |
| **Com IA** | Antes das regras, um modelo de linguagem local corrige o texto: resolve auto-correções ("às 3, não, às 4" → "às 4"), corrige gramática, formata listas e ajusta o tom à app (formal no Outlook, casual no Slack) | +0,5–2 s por ditação em CPU |

O modelo é o **Qwen 2.5 1.5B Instruct** (GGUF Q4_K_M, ~1 GB, licença Apache 2.0), a correr dentro da própria
app com llama.cpp — sem servidor nem instalações extra. Salvaguardas: se o modelo demorar mais do que
`TimeoutSeconds`, faltar, ou devolver algo que não pareça uma limpeza (ex.: responder a uma pergunta ditada),
o SpeechDesk escreve o texto sem IA. A ditação nunca fica bloqueada.

```powershell
./scripts/download-model.ps1 -Engine ai     # uma vez
```

Para melhor qualidade em máquinas com mais RAM, qualquer modelo GGUF em formato ChatML serve
(ex.: `qwen2.5-3b-instruct-q4_k_m.gguf`) — basta mudar `Formatting.Ai.ModelPath`.

## Funcionalidades

| Funcionalidade | Estado | Notas |
|---|---|---|
| Ditado em qualquer app com atalho global | ✅ | Push-to-talk + mãos-livres |
| Transcrição local rápida, PT-PT e EN | ✅ | Parakeet v3; Whisper como alternativa |
| Remove "hum", "ãh" e hesitações | ✅ | Regras locais |
| Pontuação e maiúsculas automáticas | ✅ | Modelo + regras; "vírgula", "ponto final", "nova linha" ditados |
| Limpeza com IA local (auto-correções, gramática, tom) | ✅ | Opcional, interruptor no menu do ícone |
| Falar em português e escrever em inglês | ✅ | Opcional, interruptor no menu; Whisper nativo ou IA local |
| Dicionário pessoal (nomes, termos técnicos) | ✅ | Substituições aplicadas ao texto final |
| Snippets ("o meu email" → endereço) | ✅ | `Snippets` em `settings.json` |
| Estilo por aplicação (formal, casual, código) | ✅ | Regras básicas; tom completo com a IA ligada |
| Histórico das ditações | ✅ | `history.jsonl` local |
| Modo de comandos sobre texto selecionado | ⏳ | Roadmap |
| Falar baixinho (ganho automático + VAD) | ⏳ | Roadmap |

Detalhe em [docs/ROADMAP.md](docs/ROADMAP.md).

## Requisitos

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (para compilar)
- Disco: ~0,7 GB para o modelo de transcrição (+1 GB para a limpeza com IA / tradução)

| Configuração | RAM usada pelo SpeechDesk (estimativa) | RAM do PC recomendada |
|---|---|---|
| Só transcrição (Parakeet ou Whisper) | ~1–1,2 GB | 8 GB (funciona com 4 GB) |
| Transcrição + limpeza com IA ou tradução | ~2,3–2,6 GB | 8 GB (mínimo ~6 GB) |

O modelo de IA só é carregado quando ligas a limpeza com IA ou a tradução.

## Arranque rápido

```powershell
git clone https://github.com/fmrgcarvalho/speechdesk.git
cd speechdesk

# 1. Descarregar o modelo Parakeet (~0,7 GB, uma vez)
./scripts/download-model.ps1

# 2. Correr
dotnet run --project src/SpeechDesk.App
```

Aparece um ícone junto ao relógio. Abre o Bloco de Notas, mantém **Right Ctrl** premido e fala.

## Configuração

`%AppData%\SpeechDesk\settings.json` (criado no 1.º arranque; menu do ícone → *Abrir definições* / *Recarregar definições*):

```jsonc
{
  "Hotkey": { "VirtualKey": 163, "DoubleTapMs": 350, "MinRecordingMs": 300 },  // 163 = Right Ctrl
  "Transcription": {
    "Engine": "parakeet",                      // "parakeet" | "whisper" | "cloud"
    "ParakeetModelDir": "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8",
    "WhisperModelPath": "ggml-large-v3-turbo-q5_0.bin",
    "Language": "auto"                         // só Whisper/cloud; o Parakeet deteta sozinho
  },
  "Formatting": {
    "RuleBasedCleanup": true,
    "Ai": { "Enabled": false, "ModelPath": "qwen2.5-1.5b-instruct-q4_k_m.gguf", "TimeoutSeconds": 6, "GpuLayers": 0 }
  },
  "Translation": { "Enabled": false, "TargetLanguage": "English", "TimeoutSeconds": 12 },
  "Dictionary": { "speech desk": "SpeechDesk", "dot net": ".NET" },
  "Snippets":   { "o meu email": "nome@exemplo.pt" },
  "AppStyles":  { "OUTLOOK": "Formal", "slack": "Casual", "Code": "Code" },
  "Injection": "Paste"                         // "Type" para apps que bloqueiam colar
}
```

Para usar o Whisper: `./scripts/download-model.ps1 -Engine whisper` e `"Engine": "whisper"`.

## Estrutura

```
speechdesk/
├── src/
│   ├── SpeechDesk.Core/               # Domínio puro (net8.0): abstrações, pipeline, regras de texto, definições
│   ├── SpeechDesk.Transcription/      # Motores: Parakeet (omissão), Whisper, API cloud
│   ├── SpeechDesk.AiCleanup/          # Limpeza opcional com modelo de linguagem local (LLamaSharp)
│   ├── SpeechDesk.Platform.Windows/   # Win32: hook de teclado, microfone, inserção de texto, app ativa
│   └── SpeechDesk.App/                # WPF: composition root, ícone na tray, overlay, controlador do atalho
├── tests/SpeechDesk.Core.Tests/       # Testes unitários do domínio (xUnit)
├── scripts/download-model.ps1
├── docs/                              # Página do projeto (PT em /, EN em /en), arquitetura, roadmap e ADRs
└── .github/workflows/build.yml        # CI em windows-latest: build, testes e publish
```

Arquitetura em [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Limitações conhecidas

- Não escreve em apps a correr **como Administrador** (restrição UIPI do Windows) a não ser que o SpeechDesk também corra elevado.
- A primeira ditação após o arranque pode demorar 1–2 s enquanto o modelo carrega.
- O Parakeet não aceita vocabulário prévio; nomes próprios corrigem-se com o dicionário pessoal.

## Licenças de terceiros

- Parakeet TDT 0.6B v3 — NVIDIA, CC-BY-4.0
- Qwen 2.5 1.5B Instruct — Alibaba, Apache 2.0
- sherpa-onnx — Apache 2.0 · whisper.cpp / Whisper.net — MIT · llama.cpp / LLamaSharp — MIT · NAudio — MIT
