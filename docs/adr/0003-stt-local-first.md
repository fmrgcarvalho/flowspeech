# ADR-0003 — STT local ativo; cloud apenas como adaptador

**Estado:** aceite · **Data:** 2026-10-10

## Contexto
O Wispr Flow transcreve na cloud. O objetivo do FlowPT é correr localmente (privacidade, custo zero,
funcionar offline), mas queremos poder comparar qualidade e latência com serviços cloud.

## Decisão
- Implementar `ISpeechToText` em dois adaptadores: `WhisperLocalSpeechToText` e `CloudSpeechToText`
  (API compatível com OpenAI — serve OpenAI, Groq, Azure e servidores locais como faster-whisper-server).
- Por omissão `"Engine": "local"`. A chave cloud só é lida de variável de ambiente, nunca do ficheiro.
- Modelo por omissão: `ggml-large-v3-turbo-q5_0` (bom equilíbrio qualidade/velocidade em PT-PT).
- MVP com texto "cru" + regras determinísticas; pós-processamento com LLM entra como formatador
  opcional (`OllamaFormatter`), também local.

## Consequências
- Primeira utilização requer descarregar ~550 MB.
- Latência depende do hardware; em CPU fraca recomenda-se o modelo `small`.
