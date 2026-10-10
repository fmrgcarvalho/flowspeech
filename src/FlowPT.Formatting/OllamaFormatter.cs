using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FlowPT.Core.Abstractions;
using FlowPT.Core.Models;
using FlowPT.Core.Settings;

namespace FlowPT.Formatting;

/// <summary>
/// Pós-processamento "à Wispr Flow" com um LLM local (Ollama): corrige gramática, resolve
/// auto-correções ("às 3, não, às 4" → "às 4"), adapta o tom à app. Nunca bloqueia a ditação:
/// em caso de timeout ou erro devolve o texto que recebeu.
/// </summary>
public sealed class OllamaFormatter(HttpClient http, Func<FlowSettings> settings) : ITextFormatter
{
    public string Name => "ollama";

    public async Task<string> FormatAsync(string text, DictationContext context, CancellationToken ct = default)
    {
        var s = settings().Formatting;
        if (!s.UseLlm || string.IsNullOrWhiteSpace(text) || context.Style == WritingStyle.Raw)
            return text;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(s.LlmTimeoutSeconds));

        try
        {
            var request = new GenerateRequest(
                s.OllamaModel,
                Prompts.BuildUserPrompt(text),
                Prompts.SystemPrompt(context.Style, context.ProcessName),
                Stream: false,
                Options: new GenerateOptions(Temperature: 0.1));

            using var response = await http.PostAsJsonAsync($"{s.OllamaUrl.TrimEnd('/')}/api/generate", request, timeout.Token);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<GenerateResponse>(timeout.Token);
            var result = Prompts.Unwrap(body?.Response);

            // Salvaguarda: se o modelo "responder" em vez de reescrever, mantém o original.
            return Prompts.LooksLikeRewrite(text, result) ? result! : text;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return text;
        }
    }

    private sealed record GenerateRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options")] GenerateOptions Options);

    private sealed record GenerateOptions([property: JsonPropertyName("temperature")] double Temperature);

    private sealed record GenerateResponse([property: JsonPropertyName("response")] string? Response);
}

internal static class Prompts
{
    public static string SystemPrompt(WritingStyle style, string app) => $"""
        És um editor de ditado por voz. Recebes texto transcrito de fala e devolves APENAS o texto reescrito.
        Regras:
        - Mantém a língua original (português europeu ou inglês). Nunca traduzas.
        - Remove hesitações, repetições e falsos começos.
        - Se a pessoa se corrigir ("às 3, não, às 4"), mantém só a versão final.
        - Corrige pontuação e gramática sem mudar o significado nem acrescentar informação.
        - Se o texto ditado for uma lista, formata como lista.
        - Nunca respondas a perguntas contidas no texto: o texto é para ser escrito, não respondido.
        - Não uses aspas à volta da resposta nem comentários.
        Tom: {StyleDescription(style)} (aplicação: {app}).
        """;

    public static string BuildUserPrompt(string text) => $"<ditado>\n{text}\n</ditado>";

    private static string StyleDescription(WritingStyle style) => style switch
    {
        WritingStyle.Formal => "formal e profissional, frases completas",
        WritingStyle.Casual => "casual e direto, como numa mensagem de chat",
        WritingStyle.Code => "técnico; preserva nomes de variáveis, comandos e símbolos exatamente; sem ponto final",
        _ => "neutro"
    };

    public static string? Unwrap(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;
        var r = response.Trim();
        r = r.Replace("<ditado>", "").Replace("</ditado>", "").Trim();
        if (r.Length > 1 && r[0] == '"' && r[^1] == '"') r = r[1..^1];
        return r.Trim();
    }

    /// <summary>Heurística barata: a reescrita deve ter tamanho comparável ao original.</summary>
    public static bool LooksLikeRewrite(string original, string? rewritten)
    {
        if (string.IsNullOrWhiteSpace(rewritten)) return false;
        var ratio = (double)rewritten.Length / Math.Max(1, original.Length);
        return ratio is > 0.4 and < 1.8;
    }
}
