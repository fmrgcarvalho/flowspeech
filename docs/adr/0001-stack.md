# ADR-0001 — Stack: .NET 8 + WPF

**Estado:** aceite · **Data:** 2026-10-10

## Contexto
App residente no Windows que precisa de: hook global de teclado, captura de áudio, transcrição local
com baixa latência e uma UI mínima (tray + overlay). O autor é arquiteto .NET.

## Opções
| Opção | Prós | Contras |
|---|---|---|
| **.NET 8 + WPF** | Interop Win32 direto (P/Invoke), bindings C# do sherpa-onnx e do whisper.cpp, stack conhecida | Só Windows |
| Electron/Tauri | Multiplataforma, UI web | Hooks globais e injeção de teclas exigem módulos nativos; mais memória |
| Python | Ecossistema ML | Distribuição difícil, arranque lento, interop Win32 frágil |

## Decisão
.NET 8 + WPF para a app; sherpa-onnx (Parakeet) e Whisper.net para transcrição; NAudio para áudio;
Win32 via P/Invoke. O domínio fica em `net8.0` puro para permitir no futuro outra plataforma
sem reescrever o núcleo.

## Consequências
- Build da UI requer Windows (CI em `windows-latest`); o Core compila e testa em qualquer SO.
