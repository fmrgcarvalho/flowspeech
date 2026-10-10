using SpeechDesk.Core.Abstractions;
using SpeechDesk.Core.Models;
using SpeechDesk.Core.Settings;
using SpeechDesk.Core.Text;

namespace SpeechDesk.Core.Pipeline;

/// <summary>
/// O Parakeet deteta a língua sozinho e, em frases curtas, às vezes escreve português em espanhol ou inglês.
/// Quando o utilizador indica as línguas que fala (Transcription.Languages) e o texto sai noutra,
/// repete a transcrição com um motor que aceita língua forçada (Whisper). É mais lento (~1 s por segundo
/// de áudio), por isso só corre nesses casos.
/// </summary>
public sealed class LanguageGuardSpeechToText(
    ISpeechToText primary,
    Func<ISpeechToText?> fallback,
    Func<AppSettings> settings) : ISpeechToText
{
    /// <summary>Orienta o Whisper para a variante e a pontuação certas.</summary>
    private static readonly Dictionary<string, string> Prompts = new()
    {
        ["pt"] = "Olá, bom dia. Isto é um ditado em português de Portugal.",
        ["en"] = "Hello, good morning. This is a dictation in English.",
    };

    public string Name => primary.Name;
    public bool CanTranslateToEnglish => primary.CanTranslateToEnglish;

    public async Task<Transcript> TranscribeAsync(AudioClip clip, TranscriptionOptions options, CancellationToken ct = default)
    {
        var transcript = await primary.TranscribeAsync(clip, options, ct);

        var allowed = ParseLanguages(settings().Transcription.Languages);
        if (allowed.Count == 0 || options.TranslateToEnglish || string.IsNullOrWhiteSpace(transcript.Text))
            return transcript;

        var detected = TextLanguage.Guess(transcript.Text);
        if (detected is not null && allowed.Contains(detected)) return transcript;

        var engine = fallback();
        if (engine is null) return transcript; // modelo Whisper não instalado

        var language = allowed[0];
        var prompt = Prompts.TryGetValue(language, out var p) ? p : null;
        if (!string.IsNullOrWhiteSpace(options.Prompt)) prompt = $"{prompt} {options.Prompt}".Trim();

        try
        {
            var retry = await engine.TranscribeAsync(clip, options with { Language = language, Prompt = prompt }, ct);
            if (string.IsNullOrWhiteSpace(retry.Text)) return transcript;
            return retry with { Language = language, Elapsed = transcript.Elapsed + retry.Elapsed };
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return transcript; // na dúvida, fica o texto do Parakeet
        }
    }

    internal static List<string> ParseLanguages(string? languages) =>
        (languages ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim().ToLowerInvariant())
            .Where(l => l is not "auto")
            .ToList();
}
