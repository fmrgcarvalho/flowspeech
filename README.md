# SpeechDesk

**English** · [Português](README.pt-PT.md)

[![build](https://github.com/fmrgcarvalho/speechdesk/actions/workflows/build.yml/badge.svg)](https://github.com/fmrgcarvalho/speechdesk/actions/workflows/build.yml)

Voice dictation for Windows, **100% local and free**.
Hold a key, speak, let go: the text appears wherever the cursor is, in any application
(Outlook, Teams, Slack, VS Code, your browser…). No audio ever leaves your machine.

**Project page with an interactive demo:** https://fmrgcarvalho.github.io/speechdesk/en/

> **Status: in development (v0.1).** There is no installer yet — you build it from source.
> The core has automated tests and CI builds the full Windows app; real-world testing on different
> machines is ongoing. Bugs and ideas: [Issues](https://github.com/fmrgcarvalho/speechdesk/issues).

## Shortcuts

| Shortcut | What it does |
|---|---|
| Hold **Right Ctrl** | Records while the key is held; types the text when you let go |
| **Double-tap** Right Ctrl | Hands-free mode: records without holding the key |
| **Tap** Right Ctrl (in hands-free mode) | Stops and types the text |
| **Esc** | Cancels the recording |
| Right Ctrl **+ another key** (e.g. Ctrl+C) | Cancels — it is a normal shortcut |

**AI clean-up** and **translation to English** are switched on and off from the tray icon menu next to
the clock (right-click). Change the key with `Hotkey.VirtualKey` in the settings: `163` = Right Ctrl
(default), `145` = Scroll Lock, `165` = Right Alt (avoid on keyboards where it is AltGr).

## How it works

```
 Global key ──► Microphone 16 kHz ──► Parakeet TDT 0.6B v3 ──► [AI clean-up] ──► Rules ──► Ctrl+V into the active app
 (keyboard hook)    (NAudio)           (sherpa-onnx, local)     optional, local    dictionary,  (clipboard restored)
                                                                 Qwen 2.5 1.5B      snippets
```

## Speech recognition engine

By default SpeechDesk uses **NVIDIA Parakeet TDT 0.6B v3** (CC-BY-4.0), running locally with
[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx):

- 25 European languages, including **English and Portuguese** (trained on European Portuguese)
- automatic language detection — switch languages without changing anything
- punctuation and capitalisation come from the model
- very fast on CPU (int8 build), no GPU needed

**OpenAI Whisper large-v3-turbo** (whisper.cpp) is available as an alternative — useful for languages
outside those 25 — and there is an adapter for OpenAI-compatible cloud APIs, off by default.

## Speak Portuguese, type English (optional)

Tray menu → **Translate to English**. You speak Portuguese and the text is typed in English into the app
you are using. It appears about 1–2 s after you let go of the key, one utterance at a time (the overlay
shows **→ EN**).

| Active engine | Who translates | What you need |
|---|---|---|
| Whisper | Whisper itself (built-in *translate* task) | Only the Whisper model |
| Parakeet (default) | The local AI model (Qwen 2.5), after transcribing | `download-model.ps1 -Engine ai` |

With the AI model the target language can be something else (`Translation.TargetLanguage`, e.g.
`"French"`); Whisper only translates into English. If translation fails or takes longer than
`Translation.TimeoutSeconds`, the original text is typed and a warning is shown — Portuguese never slips
through unnoticed. Snippets are matched against the final text: with translation on, add English triggers
(e.g. `"my email"`).

## AI clean-up (optional)

Two modes, from the tray menu → **Clean up text with AI**:

| Mode | What it does | Speed |
|---|---|---|
| **No AI** (default) | Rules: removes "um"/"uh", spoken punctuation, dictionary, snippets | Instant |
| **With AI** | Before the rules, a local language model fixes the text: resolves self-corrections ("at 3, no, at 4" → "at 4"), fixes grammar, formats lists and adapts the tone to the app (formal in Outlook, casual in Slack) | +0.5–2 s per dictation on CPU |

The model is **Qwen 2.5 1.5B Instruct** (GGUF Q4_K_M, ~1 GB, Apache 2.0), running inside the app with
llama.cpp — no server, nothing else to install. Safeguards: if the model is slower than `TimeoutSeconds`,
missing, or returns something that does not look like a clean-up (e.g. it answers a question you
dictated), SpeechDesk types the text without AI. Dictation never gets stuck.

```powershell
./scripts/download-model.ps1 -Engine ai     # once
```

For better quality on machines with more RAM, any ChatML-format GGUF model works
(e.g. `qwen2.5-3b-instruct-q4_k_m.gguf`) — just change `Formatting.Ai.ModelPath`.

## Features

| Feature | Status | Notes |
|---|---|---|
| Dictation into any app with a global key | ✅ | Hold-to-talk + hands-free |
| Fast local speech recognition, EN and PT | ✅ | Parakeet v3; Whisper as an alternative |
| Removes "um", "uh" and filler words | ✅ | Local rules |
| Automatic punctuation and capitalisation | ✅ | Model + rules; spoken "comma", "full stop", "new line" |
| Local AI clean-up (self-corrections, grammar, tone) | ✅ | Optional, tray menu switch |
| Speak Portuguese, type English | ✅ | Optional, tray menu switch; native Whisper or local AI |
| Personal dictionary (names, technical terms) | ✅ | Replacements applied to the final text |
| Snippets ("my email" → full address) | ✅ | `Snippets` in `settings.json` |
| Per-app style (formal, casual, code) | ✅ | Basic rules; full tone with AI on |
| Dictation history | ✅ | Local `history.jsonl` |
| Voice commands on selected text | ⏳ | Roadmap |
| Quiet speech (auto gain + VAD) | ⏳ | Roadmap |

Details in [docs/ROADMAP.md](docs/ROADMAP.md) (in Portuguese).

## Requirements

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build)
- Disk: ~0.7 GB for the speech model (+1 GB for AI clean-up / translation)

| Setup | RAM used by SpeechDesk (estimate) | Recommended PC RAM |
|---|---|---|
| Speech recognition only (Parakeet or Whisper) | ~1–1.2 GB | 8 GB (works with 4 GB) |
| Speech recognition + AI clean-up or translation | ~2.3–2.6 GB | 8 GB (minimum ~6 GB) |

The AI model is only loaded when you turn on AI clean-up or translation.

## Quick start

```powershell
git clone https://github.com/fmrgcarvalho/speechdesk.git
cd speechdesk

# 1. Download the Parakeet model (~0.7 GB, once)
./scripts/download-model.ps1

# 2. Run
dotnet run --project src/SpeechDesk.App
```

An icon appears next to the clock. Open Notepad, hold **Right Ctrl** and speak.

Every push to `main` also produces a ready-to-run build: open the latest successful run under
[Actions](https://github.com/fmrgcarvalho/speechdesk/actions/workflows/build.yml) and download the
`SpeechDesk-win-x64` artifact (you still need to download the model with the script).

## Configuration

`%AppData%\SpeechDesk\settings.json` (created on first run; tray menu → *Open settings* / *Reload settings*):

```jsonc
{
  "Hotkey": { "VirtualKey": 163, "DoubleTapMs": 350, "MinRecordingMs": 300 },  // 163 = Right Ctrl
  "Transcription": {
    "Engine": "parakeet",                      // "parakeet" | "whisper" | "cloud"
    "ParakeetModelDir": "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8",
    "WhisperModelPath": "ggml-large-v3-turbo-q5_0.bin",
    "Language": "auto"                         // Whisper/cloud only; Parakeet detects it
  },
  "Formatting": {
    "RuleBasedCleanup": true,
    "Ai": { "Enabled": false, "ModelPath": "qwen2.5-1.5b-instruct-q4_k_m.gguf", "TimeoutSeconds": 6, "GpuLayers": 0 }
  },
  "Translation": { "Enabled": false, "TargetLanguage": "English", "TimeoutSeconds": 12 },
  "Dictionary": { "speech desk": "SpeechDesk", "dot net": ".NET" },
  "Snippets":   { "my email": "name@example.com" },
  "AppStyles":  { "OUTLOOK": "Formal", "slack": "Casual", "Code": "Code" },
  "Injection": "Paste"                         // "Type" for apps that block pasting
}
```

To use Whisper: `./scripts/download-model.ps1 -Engine whisper` and `"Engine": "whisper"`.

## Project structure

```
speechdesk/
├── src/
│   ├── SpeechDesk.Core/               # Pure domain (net8.0): abstractions, pipeline, text rules, settings
│   ├── SpeechDesk.Transcription/      # Engines: Parakeet (default), Whisper, cloud API
│   ├── SpeechDesk.AiCleanup/          # Optional clean-up/translation with a local language model (LLamaSharp)
│   ├── SpeechDesk.Platform.Windows/   # Win32: keyboard hook, microphone, text insertion, active app
│   └── SpeechDesk.App/                # WPF: composition root, tray icon, overlay, hotkey controller
├── tests/SpeechDesk.Core.Tests/       # Domain unit tests (xUnit)
├── scripts/download-model.ps1
├── docs/                              # Project page (PT at /, EN at /en), architecture, roadmap and ADRs
└── .github/workflows/build.yml        # CI on windows-latest: build, tests and publish
```

Architecture in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) (in Portuguese).

## Known limitations

- It cannot type into apps running **as Administrator** (Windows UIPI restriction) unless SpeechDesk also runs elevated.
- The first dictation after start-up can take 1–2 s while the model loads.
- Parakeet does not accept a custom vocabulary; fix proper names with the personal dictionary.

## Third-party licences

- Parakeet TDT 0.6B v3 — NVIDIA, CC-BY-4.0
- Whisper — OpenAI, MIT
- Qwen 2.5 1.5B Instruct — Alibaba, Apache 2.0
- sherpa-onnx — Apache 2.0 · whisper.cpp / Whisper.net — MIT · llama.cpp / LLamaSharp — MIT · NAudio — MIT
