using FlowSpeech.Core.Models;

namespace FlowSpeech.Core.Text;

/// <summary>
/// Prompt e salvaguardas da limpeza com IA. Fica no Core (sem dependência do motor de inferência)
/// para poder ser testado em qualquer SO.
/// </summary>
public static class AiCleanupPrompt
{
    public const string StopToken = "<|im_end|>";

    public static string SystemPrompt(WritingStyle style, string? translateTo = null) => translateTo is null
        ? CleanupPrompt(style)
        : TranslatePrompt(style, translateTo);

    private static string TranslatePrompt(WritingStyle style, string target) => $"""
        You are a dictation translator. You receive a voice transcription (usually European Portuguese)
        and return ONLY its translation into natural, fluent {target}.
        Rules:
        - Translate the meaning faithfully; do not add or omit information.
        - Remove hesitations, repetitions and false starts.
        - If the speaker corrects themselves ("às 3, não, às 4"), keep only the final version.
        - Keep names, numbers, emails, URLs and code exactly as they are.
        - Spoken punctuation words ("vírgula", "ponto final", "ponto de interrogação", "nova linha",
          "novo parágrafo") are commands: apply the punctuation, do not translate the words.
        - If the text is a list, format it as a list.
        - Never answer questions in the text: translate them.
        - No quotes, no comments, no explanations, no original text.
        Tone: {StyleDescriptionEn(style)}.
        """;

    private static string CleanupPrompt(WritingStyle style) => $"""
        És um editor de texto ditado por voz. Recebes uma transcrição e devolves APENAS o texto corrigido.
        Regras:
        - Mantém a língua original (português europeu ou inglês). Nunca traduzas.
        - Remove hesitações, repetições e falsos começos.
        - Se a pessoa se corrigir ("às 3, não, às 4"), mantém só a versão final.
        - Corrige pontuação e gramática sem mudar o significado nem acrescentar informação.
        - Mantém nomes, números, emails e URLs exatamente como estão.
        - Se o texto for uma lista, formata-o como lista.
        - Nunca respondas a perguntas do texto: o texto é para ser escrito, não respondido.
        - Sem aspas, sem comentários, sem explicações.
        Tom: {StyleDescription(style)}.
        """;

    /// <summary>Prompt completo no formato ChatML (Qwen 2.5 e compatíveis).</summary>
    public static string Build(string text, WritingStyle style, string? translateTo = null) =>
        $"<|im_start|>system\n{SystemPrompt(style, translateTo)}<|im_end|>\n" +
        $"<|im_start|>user\n<ditado>\n{text}\n</ditado><|im_end|>\n" +
        "<|im_start|>assistant\n";

    private static string StyleDescription(WritingStyle style) => style switch
    {
        WritingStyle.Formal => "formal e profissional, frases completas",
        WritingStyle.Casual => "casual e direto, como numa mensagem de chat",
        WritingStyle.Code => "técnico; preserva comandos, nomes de variáveis e símbolos; sem ponto final",
        _ => "neutro; mantém o estilo de quem fala"
    };

    private static string StyleDescriptionEn(WritingStyle style) => style switch
    {
        WritingStyle.Formal => "formal and professional",
        WritingStyle.Casual => "casual and direct, like a chat message",
        WritingStyle.Code => "technical; keep commands, identifiers and symbols; no trailing period",
        _ => "neutral; match the speaker"
    };

    /// <summary>Limpa a resposta do modelo (marcadores, aspas) ou devolve null se for inválida.</summary>
    public static string? Clean(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;
        var r = response.Replace(StopToken, string.Empty)
                        .Replace("<ditado>", string.Empty)
                        .Replace("</ditado>", string.Empty)
                        .Trim();
        if (r.Length > 1 && r[0] == '"' && r[^1] == '"') r = r[1..^1].Trim();
        return r.Length == 0 ? null : r;
    }

    /// <summary>
    /// Salvaguarda: uma limpeza tem tamanho comparável ao original. Se o modelo "responder" ou
    /// inventar texto, a proporção foge e mantém-se o original.
    /// </summary>
    public static bool IsPlausibleRewrite(string original, string? rewritten)
    {
        if (string.IsNullOrWhiteSpace(rewritten)) return false;
        var ratio = (double)rewritten.Length / Math.Max(1, original.Length);
        return ratio is >= 0.35 and <= 1.6;
    }

    /// <summary>Limite de tokens de saída: proporcional ao texto, com folga.</summary>
    public static int MaxTokensFor(string text) => Math.Clamp(text.Length / 2 + 64, 64, 1024);
}
