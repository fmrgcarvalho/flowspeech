# ADR-0004 — Limpeza opcional com modelo de linguagem embebido

**Estado:** aceite · **Data:** 2026-10-10

## Contexto
As regras determinísticas resolvem hesitações, pontuação ditada, dicionário e snippets, mas não
resolvem auto-correções ("às 3, não, às 4"), gramática nem tom. O utilizador quer poder escolher entre
"só regras" (instantâneo) e "com IA", sem instalar nem manter um servidor externo.

## Opções
| Opção | Prós | Contras |
|---|---|---|
| **LLamaSharp (llama.cpp) embebido + modelo GGUF** | Corre no processo, CPU ou GPU, qualquer modelo GGUF | +1 GB de modelo; latência em CPU |
| Servidor de modelos externo | Fácil trocar modelos | Instalação e processo extra — rejeitado pelo utilizador |
| ONNX Runtime GenAI | Integração Microsoft | Menos modelos prontos; conversão necessária |

## Decisão
- `LocalAiFormatter` (projeto `FlowSpeech.AiCleanup`) com LLamaSharp e **Qwen 2.5 1.5B Instruct Q4_K_M**
  (Apache 2.0, bom em PT e EN, rápido em CPU).
- **Desligado por omissão**; interruptor "Limpar texto com IA" no menu do ícone, guardado em `settings.json`.
- Corre **antes** das regras, para o dicionário e os snippets terem sempre a última palavra.
- Salvaguardas: timeout (6 s), rejeição de respostas com tamanho implausível, e erro → texto sem IA.
- Prompt e salvaguardas no Core (`AiCleanupPrompt`) para serem testáveis sem o motor de inferência.

## Consequências
- O modelo só é carregado em memória (~1,2 GB RAM) quando a opção está ligada.
- O llama.cpp (LLamaSharp) e o whisper.cpp (Whisper.net) trazem bibliotecas nativas `ggml`; validar no
  primeiro build que não há colisões de nomes no diretório de saída.
