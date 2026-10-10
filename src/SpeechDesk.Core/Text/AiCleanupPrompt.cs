using SpeechDesk.Core.Models;

namespace SpeechDesk.Core.Text;

/// <summary>
/// Prompt and safeguards for AI clean-up and translation. Lives in Core (no inference engine dependency)
/// so it can be tested on any OS.
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
        You are an editor of voice-dictated text. You receive a transcription and return ONLY the corrected text.
        Rules:
        - Keep the original language (English or European Portuguese). Never translate.
        - Remove hesitations, repetitions and false starts.
        - If the speaker corrects themselves ("at 3, no, at 4"), keep only the final version.
        - Fix punctuation and grammar without changing the meaning or adding information.
        - Keep names, numbers, emails and URLs exactly as they are.
        - If the text is a list, format it as a list.
        - Never answer questions in the text: the text is to be written, not answered.
        - No quotes, no comments, no explanations.
        Tone: {StyleDescriptionEn(style)}.
        """;

    /// <summary>Full prompt in ChatML format (Qwen 2.5 and compatible models).</summary>
    public static string Build(string text, WritingStyle style, string? translateTo = null) =>
        $"<|im_start|>system\n{SystemPrompt(style, translateTo)}<|im_end|>\n" +
        $"<|im_start|>user\n<dictation>\n{text}\n</dictation><|im_end|>\n" +
        "<|im_start|>assistant\n";

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
                        .Replace("<dictation>", string.Empty)
                        .Replace("</dictation>", string.Empty)
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
