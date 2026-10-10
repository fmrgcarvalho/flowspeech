# ADR-0003 — Motor de transcrição: Parakeet TDT 0.6B v3, local

**Estado:** aceite · **Data:** 2026-10-10

## Contexto
O FlowSpeech tem de transcrever localmente (privacidade, custo zero, offline), com qualidade em
português europeu e inglês e latência baixa em CPU comum. Sem dependência de LLMs externos.

## Opções
| Motor | Línguas | Qualidade PT | Velocidade CPU | Licença |
|---|---|---|---|---|
| **Parakeet TDT 0.6B v3** (sherpa-onnx, int8) | 25 europeias, deteção automática | Treinado com PT-PT; WER 4,8% no FLEURS | Muito rápida | CC-BY-4.0 |
| Whisper large-v3-turbo (whisper.cpp) | 99+ | Boa, tendência para PT-BR | Média | MIT |
| Modelos só-inglês (Canary-Qwen, Distil-Whisper, Parakeet 1.1B) | EN | — | — | — |

## Decisão
- `ISpeechToText` com três adaptadores: `ParakeetSpeechToText` (omissão), `WhisperLocalSpeechToText`
  (alternativa para línguas fora das 25) e `CloudSpeechToText` (API compatível OpenAI, desligado).
- A chave cloud só é lida de variável de ambiente, nunca do ficheiro.
- Pós-processamento por regras determinísticas; limpeza com modelo de linguagem é opcional (ver ADR-0004).

## Consequências
- Primeira utilização requer descarregar ~0,7 GB.
- O Parakeet não aceita *prompt* de vocabulário: nomes próprios corrigem-se com o dicionário pessoal.
- Atribuição CC-BY-4.0 ao modelo NVIDIA no README.
