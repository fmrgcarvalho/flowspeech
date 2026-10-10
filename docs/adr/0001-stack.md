# ADR-0001 — Stack: .NET 8 + WPF + whisper.cpp (Whisper.net)

**Estado:** aceite · **Data:** 2026-10-10

## Contexto
App residente no Windows que precisa de: hook global de teclado, captura de áudio, inferência de
speech-to-text local com baixa latência e uma UI mínima (tray + overlay). O autor é arquiteto .NET.

## Opções
| Opção | Prós | Contras |
|---|---|---|
| **.NET 8 + WPF + Whisper.net** | Interop Win32 direto (P/Invoke), whisper.cpp nativo com runtimes CPU/CUDA/Vulkan, stack conhecida | Só Windows |
| Electron/Tauri + whisper.cpp | Multiplataforma, UI web | Hooks globais e injeção de teclas exigem módulos nativos; mais memória |
| Python + faster-whisper | Ecossistema ML | Distribuição difícil, arranque lento, interop Win32 frágil |

## Decisão
.NET 8 + WPF para a app; whisper.cpp via Whisper.net para STT; NAudio para áudio; Win32 via P/Invoke.
O domínio fica em `net8.0` puro para permitir no futuro uma camada macOS/Linux sem reescrever o núcleo.

## Consequências
- Build da UI requer Windows (CI em `windows-latest`); o Core compila e testa em qualquer SO.
- GPU opcional adicionando o pacote de runtime correspondente.
