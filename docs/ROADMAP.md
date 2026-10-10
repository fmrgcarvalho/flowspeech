# Roadmap

## v0.1 — MVP (este commit)
- [x] Push-to-talk global (Right Ctrl) + mãos-livres (duplo toque) + cancelar (Esc)
- [x] Whisper local (large-v3-turbo) com pré-carregamento
- [x] Limpeza por regras: hesitações, pontuação falada PT/EN, maiúsculas
- [x] Dicionário pessoal (substituição + prompt do Whisper) e snippets
- [x] Inserção em qualquer app (paste com reposição do clipboard; modo digitação)
- [x] Overlay com nível de áudio e estado; ícone na tray
- [x] Histórico local em JSONL
- [x] Adaptador cloud (desligado) e LLM local via Ollama (desligado)
- [x] CI no GitHub Actions (windows-latest)

## v0.2 — Qualidade do texto
- [ ] Ativar o LLM por omissão quando o Ollama estiver disponível (deteção automática)
- [ ] Contexto: enviar ao LLM o texto à volta do cursor (UI Automation) para continuidade
- [ ] Aprender com correções: detetar quando o utilizador edita o texto colado e propor entradas no dicionário
- [ ] Janela de definições (WPF) em vez de editar JSON
- [ ] Escolha do microfone e teste de nível

## v0.3 — Paridade com o Wispr Flow
- [ ] **Command Mode**: selecionar texto + atalho + instrução falada ("traduz para inglês", "mais curto")
- [ ] **Whisper mode**: normalização de ganho + VAD (Silero) para falar baixinho
- [ ] Atalho configurável com modificadores (ex.: Ctrl+Win, como o Wispr)
- [ ] Sons de início/fim de gravação
- [ ] Estatísticas (palavras ditadas, WPM, tempo poupado)

## v0.4 — Distribuição
- [ ] Instalador (MSIX ou Velopack) com download do modelo no primeiro arranque
- [ ] Arranque automático com o Windows
- [ ] Atualizações automáticas
- [ ] Transcrição em streaming (texto parcial enquanto fala)
