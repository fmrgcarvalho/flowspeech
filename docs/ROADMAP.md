# Roadmap

## v0.1 — MVP
- [x] Push-to-talk global (Right Ctrl) + mãos-livres (duplo toque) + cancelar (Esc)
- [x] Transcrição local com Parakeet TDT 0.6B v3 (sherpa-onnx), pré-carregado no arranque
- [x] Whisper large-v3-turbo e API cloud como motores alternativos
- [x] Limpeza por regras: hesitações, pontuação falada PT/EN, maiúsculas
- [x] Dicionário pessoal e snippets
- [x] Inserção em qualquer app (paste com reposição do clipboard; modo digitação)
- [x] Overlay com nível de áudio e estado; ícone na tray
- [x] Histórico local em JSONL
- [x] CI no GitHub Actions (windows-latest)

## v0.2 — Qualidade do texto
- [ ] Aprender com correções: detetar quando o utilizador edita o texto colado e propor entradas no dicionário
- [ ] Auto-correções faladas ("às 3, não, às 4" → "às 4") por regras
- [ ] Regras de estilo por app mais ricas (formal, casual, código)
- [ ] Janela de definições (WPF) em vez de editar JSON
- [ ] Escolha do microfone e teste de nível

## v0.3 — Produtividade
- [ ] Modo de comandos: selecionar texto + atalho + instrução falada
- [ ] Falar baixinho: normalização de ganho + VAD (Silero, também disponível no sherpa-onnx)
- [ ] Atalho configurável com modificadores (ex.: Ctrl+Win)
- [ ] Sons de início/fim de gravação
- [ ] Estatísticas (palavras ditadas, WPM, tempo poupado)
- [ ] Aceleração GPU (DirectML/CUDA) no sherpa-onnx

## v0.4 — Distribuição
- [ ] Instalador (MSIX ou Velopack) com download do modelo no primeiro arranque
- [ ] Arranque automático com o Windows
- [ ] Atualizações automáticas
- [ ] Transcrição em streaming (texto parcial enquanto fala)
