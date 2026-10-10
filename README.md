# FlowPT

Ditado por voz para Windows, **100% local** — um clone do [Wispr Flow](https://wisprflow.ai) que corre no teu PC.
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
 Tecla global ──► Microfone (16 kHz) ──► Whisper local ──► Limpeza ──► LLM local* ──► Ctrl+V na app ativa
 (hook WH_KEYBOARD_LL)   (NAudio)       (whisper.cpp)   (regras)    (Ollama)       (clipboard reposto)
                                                                     * opcional, desligado no MVP
```

## Funcionalidades do Wispr Flow e o estado no FlowPT

| Wispr Flow | FlowPT | Notas |
|---|---|---|
| Ditado em qualquer app com atalho global | ✅ | Push-to-talk + mãos-livres |
| Transcrição rápida e precisa, multilingue | ✅ | Whisper large-v3-turbo local; deteção automática PT/EN |
| Remove "hum", "ãh" e hesitações | ✅ | Regras locais |
| Pontuação e maiúsculas automáticas | ✅ | Whisper + regras; "vírgula", "ponto final", "nova linha" ditados |
| Dicionário pessoal (nomes, termos técnicos) | ✅ | Substituições + *prompt* que enviesa o Whisper |
| Snippets ("o meu email" → endereço) | ✅ | `Snippets` em `settings.json` |
| Tom adaptado à app (formal no Outlook, casual no Slack) | 🟡 | Deteção da app feita; reescrita de tom exige ativar o LLM |
| Auto-correção ("às 3, não, às 4" → "às 4") | 🟡 | Via LLM local (Ollama), desligado por omissão |
| Histórico das ditações | ✅ | `history.jsonl` local |
| Command Mode (selecionar texto e dizer "torna isto mais formal") | ⏳ | Roadmap |
| Whisper mode (falar baixinho) | ⏳ | Roadmap — ganho automático + VAD |
| Aprender correções do utilizador automaticamente | ⏳ | Roadmap |

Detalhe em [docs/ROADMAP.md](docs/ROADMAP.md).

## Requisitos

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (para compilar)
- ~1 GB livre para o modelo; 8 GB RAM recomendados
- Opcional: GPU NVIDIA (ver *Aceleração GPU*) e [Ollama](https://ollama.com) para o pós-processamento com LLM

## Arranque rápido

```powershell
git clone https://github.com/fmrgcarvalho/flowspeech.git
cd flowspeech

# 1. Descarregar o modelo Whisper (~550 MB, uma vez)
./scripts/download-model.ps1

# 2. Correr
dotnet run --project src/FlowPT.App
```

Aparece um ícone junto ao relógio. Abre o Bloco de Notas, mantém **Right Ctrl** premido e fala.

## Configuração

`%AppData%\FlowPT\settings.json` (criado no 1.º arranque; menu do ícone → *Abrir definições* / *Recarregar definições*):

```jsonc
{
  "Hotkey": { "VirtualKey": 163, "DoubleTapMs": 350, "MinRecordingMs": 300 },  // 163 = Right Ctrl
  "Transcription": {
    "Engine": "local",                         // "local" | "cloud"
    "Language": "auto",                        // ou "pt", "en"
    "ModelPath": "ggml-large-v3-turbo-q5_0.bin"
  },
  "Formatting": { "RuleBasedCleanup": true, "UseLlm": false, "OllamaModel": "qwen2.5:7b-instruct" },
  "Dictionary": { "flow pt": "FlowPT", "gaiao": "Gaião" },
  "Snippets":   { "o meu email": "mgaiao25@gmail.com" },
  "AppStyles":  { "OUTLOOK": "Formal", "slack": "Casual", "Code": "Code" },
  "Injection": "Paste"                         // "Type" para apps que bloqueiam colar
}
```

### Ativar o LLM local (opcional)

```powershell
winget install Ollama.Ollama
ollama pull qwen2.5:7b-instruct
```

Depois, em `settings.json`: `"UseLlm": true`. Se o Ollama estiver em baixo ou demorar mais do que
`LlmTimeoutSeconds`, o FlowPT escreve o texto sem LLM — a ditação nunca bloqueia.

### Aceleração GPU

Em `src/FlowPT.Transcription/FlowPT.Transcription.csproj` adicionar `Whisper.net.Runtime.Cuda`
(NVIDIA) ou `Whisper.net.Runtime.Vulkan`. O Whisper.net escolhe automaticamente o melhor runtime disponível.

## Estrutura

```
flowpt/
├── src/
│   ├── FlowPT.Core/               # Domínio puro (net8.0): abstrações, pipeline, regras de texto, definições
│   ├── FlowPT.Transcription/      # Adaptadores STT: Whisper local (ativo) e API cloud (opcional)
│   ├── FlowPT.Formatting/         # Pós-processamento com LLM local (Ollama)
│   ├── FlowPT.Platform.Windows/   # Win32: hook de teclado, microfone, inserção de texto, app ativa
│   └── FlowPT.App/                # WPF: composition root, ícone na tray, overlay, controlador do atalho
├── tests/FlowPT.Core.Tests/       # Testes unitários do domínio (xUnit)
├── scripts/download-model.ps1
├── docs/                          # Arquitetura, roadmap e ADRs
└── .github/workflows/build.yml    # CI em windows-latest: build, testes e publish
```

Arquitetura em [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Limitações conhecidas

- Não escreve em apps a correr **como Administrador** (restrição UIPI do Windows) a não ser que o FlowPT também corra elevado.
- Primeira ditação após o arranque pode demorar 1–3 s enquanto o modelo carrega.
- Em CPU, o modelo `turbo` transcreve ~5 s de fala em ~1–2 s num portátil moderno; usar `small` em máquinas fracas.
