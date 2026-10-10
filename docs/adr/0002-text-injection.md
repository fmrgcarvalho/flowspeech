# ADR-0002 — Inserção de texto via clipboard + Ctrl+V

**Estado:** aceite · **Data:** 2026-10-10

## Contexto
O texto tem de aparecer no cursor de qualquer aplicação (Win32, UWP, Electron, browsers, terminais).

## Opções
1. **Clipboard + `SendInput(Ctrl+V)`** — instantâneo para textos longos, preserva acentos e emojis, funciona quase em todo o lado. Risco: apagar o clipboard do utilizador.
2. **`SendInput` com `KEYEVENTF_UNICODE`** — não toca no clipboard, mas é lento em textos longos e algumas apps (jogos, RDP, terminais) interpretam mal.
3. **UI Automation `ValuePattern.SetValue`** — substitui o conteúdo inteiro do campo; não insere no cursor.

## Decisão
Opção 1 por omissão, com *snapshot* e reposição do clipboard 250 ms depois. Opção 2 disponível
via `"Injection": "Type"` para apps que bloqueiam colar.

## Consequências
- Gestores de clipboard (Win+V) podem registar o texto ditado. Mitigação futura: marcar o conteúdo com
  `ExcludeClipboardContentFromMonitorProcessing`.
- Apps elevadas (Administrador) não recebem input de processos não elevados (UIPI).
