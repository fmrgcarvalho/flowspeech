# FlowSpeech

Ditado por voz para Windows, **100% local e gratuito**.
Mantém uma tecla premida, fala, larga: o texto aparece onde está o cursor, em qualquer aplicação
(Outlook, Teams, Slack, VS Code, browser…). Nenhum áudio sai da máquina.

## Como funciona

| Ação | Resultado |
|---|---|
| Manter **Right Ctrl** premido e falar | Grava enquanto a tecla está premida; ao largar, escreve o texto |
| **Dois toques** em Right Ctrl | Modo mãos-livres; mais um toque termina |
| **Esc**, ou Right Ctrl + outra tecla | Cancela a gravação |

O que acontece por baixo:

```
 Tecla global ──► Microfone 16 kHz ──► Parakeet TDT 0.6B v3 ──► Limpeza por regras ──► Ctrl+V na app ativa
 (hook de teclado)    (NAudio)          (sherpa-onnx, local)    (hesitações, dicionário,   (clipboard reposto)
                                                                  snippets, pontuação)
```

## Motor de transcrição

Por omissão o FlowSpeech usa o **NVIDIA Parakeet TDT 0.6B v3** (licença CC-BY-4.0), executado localmente
com [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx):

- 25 línguas europeias, incluindo **português (treinado com português europeu)** e inglês
- deteção automática da língua — podes alternar PT/EN sem mudar nada
- pontuação e maiúsculas já vêm do modelo
- muito rápido em CPU (versão int8), sem precisar de GPU

O **Whisper large-v3-turbo** (whisper.cpp) continua disponível como alternativa — útil para línguas fora
das 25 suportadas — e há um adaptador para APIs cloud compatíveis com OpenAI, desligado por omissão.

## Funcionalidades

| Funcionalidade | Estado | Notas |
|---|---|---|
| Ditado em qualquer app com atalho global | ✅ | Push-to-talk + mãos-livres |
| Transcrição local rápida, PT-PT e EN | ✅ | Parakeet v3; Whisper como alternativa |
| Remove "hum", "ãh" e hesitações | ✅ | Regras locais |
| Pontuação e maiúsculas automáticas | ✅ | Modelo + regras; "vírgula", "ponto final", "nova linha" ditados |
| Dicionário pessoal (nomes, termos técnicos) | ✅ | Substituições aplicadas ao texto final |
| Snippets ("o meu email" → endereço) | ✅ | `Snippets` em `settings.json` |
| Estilo por aplicação (ex.: código sem ponto final) | 🟡 | Deteção da app feita; regras de estilo básicas |
| Histórico das ditações | ✅ | `history.jsonl` local |
| Modo de comandos sobre texto selecionado | ⏳ | Roadmap |
| Falar baixinho (ganho automático + VAD) | ⏳ | Roadmap |

Detalhe em [docs/ROADMAP.md](docs/ROADMAP.md).

## Requisitos

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (para compilar)
- ~1 GB livre para o modelo; 8 GB RAM recomendados

## Arranque rápido

```powershell
git clone https://github.com/fmrgcarvalho/flowspeech.git
cd flowspeech

# 1. Descarregar o modelo Parakeet (~0,7 GB, uma vez)
./scripts/download-model.ps1

# 2. Correr
dotnet run --project src/FlowSpeech.App
```

Aparece um ícone junto ao relógio. Abre o Bloco de Notas, mantém **Right Ctrl** premido e fala.

## Configuração

`%AppData%\FlowSpeech\settings.json` (criado no 1.º arranque; menu do ícone → *Abrir definições* / *Recarregar definições*):

```jsonc
{
  "Hotkey": { "VirtualKey": 163, "DoubleTapMs": 350, "MinRecordingMs": 300 },  // 163 = Right Ctrl
  "Transcription": {
    "Engine": "parakeet",                      // "parakeet" | "whisper" | "cloud"
    "ParakeetModelDir": "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8",
    "WhisperModelPath": "ggml-large-v3-turbo-q5_0.bin",
    "Language": "auto"                         // só Whisper/cloud; o Parakeet deteta sozinho
  },
  "Formatting": { "RuleBasedCleanup": true },
  "Dictionary": { "flow speech": "FlowSpeech", "gaiao": "Gaião" },
  "Snippets":   { "o meu email": "mgaiao25@gmail.com" },
  "AppStyles":  { "OUTLOOK": "Formal", "slack": "Casual", "Code": "Code" },
  "Injection": "Paste"                         // "Type" para apps que bloqueiam colar
}
```

Para usar o Whisper: `./scripts/download-model.ps1 -Engine whisper` e `"Engine": "whisper"`.

## Estrutura

```
flowspeech/
├── src/
│   ├── FlowSpeech.Core/               # Domínio puro (net8.0): abstrações, pipeline, regras de texto, definições
│   ├── FlowSpeech.Transcription/      # Motores: Parakeet (omissão), Whisper, API cloud
│   ├── FlowSpeech.Platform.Windows/   # Win32: hook de teclado, microfone, inserção de texto, app ativa
│   └── FlowSpeech.App/                # WPF: composition root, ícone na tray, overlay, controlador do atalho
├── tests/FlowSpeech.Core.Tests/       # Testes unitários do domínio (xUnit)
├── scripts/download-model.ps1
├── docs/                              # Arquitetura, roadmap e ADRs
└── .github/workflows/build.yml        # CI em windows-latest: build, testes e publish
```

Arquitetura em [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Limitações conhecidas

- Não escreve em apps a correr **como Administrador** (restrição UIPI do Windows) a não ser que o FlowSpeech também corra elevado.
- A primeira ditação após o arranque pode demorar 1–2 s enquanto o modelo carrega.
- O Parakeet não aceita vocabulário prévio; nomes próprios corrigem-se com o dicionário pessoal.

## Licenças de terceiros

- Parakeet TDT 0.6B v3 — NVIDIA, CC-BY-4.0
- sherpa-onnx — Apache 2.0 · whisper.cpp / Whisper.net — MIT · NAudio — MIT
