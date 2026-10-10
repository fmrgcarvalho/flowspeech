# Roadmap

## v0.1 — MVP
- [x] Push-to-talk global (Right Ctrl) + mãos-livres (duplo toque) + cancelar (Esc)
- [x] Transcrição local com Parakeet TDT 0.6B v3 (sherpa-onnx), pré-carregado no arranque
- [x] Whisper large-v3-turbo e API cloud como motores alternativos
- [x] Limpeza por regras: hesitações, pontuação falada PT/EN, maiúsculas
- [x] Dicionário pessoal e snippets
- [x] Limpeza opcional com IA local (Qwen 2.5 1.5B via LLamaSharp), interruptor no menu
- [x] Inserção em qualquer app (paste com reposição do clipboard; modo digitação)
- [x] Overlay com nível de áudio e estado; ícone na tray
- [x] Histórico local em JSONL
- [x] CI no GitHub Actions (windows-latest)

## v0.2 — Qualidade do texto
- [ ] Aprender com correções: detetar quando o utilizador edita o texto colado e propor entradas no dicionário
- [ ] Escolher o modelo de IA na janela de definições (1.5B rápido / 3B melhor)
- [ ] Aceleração GPU para a IA (backend CUDA/Vulkan do llama.cpp)
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
