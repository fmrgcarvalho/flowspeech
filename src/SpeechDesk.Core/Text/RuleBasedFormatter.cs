using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SpeechDesk.Core.Abstractions;
using SpeechDesk.Core.Models;
using SpeechDesk.Core.Settings;

namespace SpeechDesk.Core.Text;

/// <summary>
/// Limpeza determinística, 100% local e instantânea: hesitações, pontuação falada,
/// dicionário pessoal, snippets, espaçamento e maiúsculas.
/// </summary>
public sealed partial class RuleBasedFormatter(Func<AppSettings> settings) : ITextFormatter
{
    public string Name => "rules";

    public Task<string> FormatAsync(string text, DictationContext context, CancellationToken ct = default)
        => Task.FromResult(Format(text, context.Style, context.IsTranslated));

    /// <param name="translated">
    /// Texto já traduzido: não se aplica a pontuação falada (em inglês "period" ou "colon" são palavras normais).
    /// </param>
    public string Format(string text, WritingStyle style, bool translated = false)
    {
        var s = settings();
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        if (!s.Formatting.RuleBasedCleanup) return text.Trim();

        var result = RemoveFillers(text);
        if (!translated) result = ApplySpokenPunctuation(result);
        result = ApplyReplacements(result, s.Dictionary);
        result = ApplyReplacements(result, s.Snippets);
        result = NormalizeSpacing(result);

        result = style == WritingStyle.Code
            ? TrimTrailingPeriod(result)
            : Capitalize(result);

        return result;
    }

    // ---- 1. Hesitações -------------------------------------------------------------

    [GeneratedRegex(@"(?<![\p{L}])(hum+|hmm+|ã+h*|ah+m*|eh+m*|uh+m*|um+|er+m*)(?![\p{L}])[,.]?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex FillerRegex();

    internal static string RemoveFillers(string text) => FillerRegex().Replace(text, string.Empty);

    // ---- 2. Pontuação falada (PT + EN) --------------------------------------------

    private static readonly (string Spoken, string Symbol)[] SpokenPunctuation =
    [
        ("novo parágrafo", "\n\n"), ("new paragraph", "\n\n"),
        ("nova linha", "\n"), ("new line", "\n"),
        ("ponto de interrogação", "?"), ("question mark", "?"),
        ("ponto de exclamação", "!"), ("exclamation mark", "!"),
        ("ponto e vírgula", ";"), ("semicolon", ";"),
        ("dois pontos", ":"), ("colon", ":"),
        ("ponto final", "."), ("full stop", "."), ("period", "."),
        ("vírgula", ","), ("comma", ","),
        ("abre parênteses", "("), ("fecha parênteses", ")"),
    ];

    internal static string ApplySpokenPunctuation(string text)
    {
        foreach (var (spoken, symbol) in SpokenPunctuation)
        {
            // Remove pontuação que o Whisper tenha posto à volta da palavra falada.
            var pattern = $@"[,.]?\s*(?<![\p{{L}}]){Regex.Escape(spoken)}(?![\p{{L}}])[,.]?";
            text = Regex.Replace(text, pattern, symbol, RegexOptions.IgnoreCase);
        }
        return text;
    }

    // ---- 3./4. Dicionário pessoal e snippets --------------------------------------

    internal static string ApplyReplacements(string text, IReadOnlyDictionary<string, string> map)
    {
        // Frases mais longas primeiro, para "o meu email pessoal" ganhar a "o meu email".
        foreach (var (from, to) in map.OrderByDescending(kv => kv.Key.Length))
        {
            if (string.IsNullOrWhiteSpace(from)) continue;
            var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(from)}(?![\p{{L}}\p{{N}}])";
            text = Regex.Replace(text, pattern, to.Replace("$", "$$"), RegexOptions.IgnoreCase);
        }
        return text;
    }

    // ---- 5. Espaçamento e maiúsculas ----------------------------------------------

    // Só remove o espaço se a pontuação não iniciar uma palavra (preserva ".NET", ".gitignore").
    [GeneratedRegex(@"[ \t]+([,.;:!?)])(?![\p{L}\p{N}])")] private static partial Regex SpaceBeforePunct();
    // Só antes de letras: preserva "10:30", "1,5", emails e URLs.
    [GeneratedRegex(@"([,;!?])(?=\p{L})")] private static partial Regex MissingSpaceAfterPunct();
    [GeneratedRegex(@"\(\s+")] private static partial Regex SpaceAfterOpenParen();
    [GeneratedRegex(@"[ \t]{2,}")] private static partial Regex MultiSpace();
    [GeneratedRegex(@"[ \t]*\n[ \t]*")] private static partial Regex SpacesAroundNewline();
    [GeneratedRegex(@"([,.;:!?])\1+")] private static partial Regex RepeatedPunct();

    internal static string NormalizeSpacing(string text)
    {
        text = SpaceBeforePunct().Replace(text, "$1");
        text = RepeatedPunct().Replace(text, "$1");
        text = MissingSpaceAfterPunct().Replace(text, "$1 ");
        text = SpaceAfterOpenParen().Replace(text, "(");
        text = MultiSpace().Replace(text, " ");
        text = SpacesAroundNewline().Replace(text, "\n");
        return text.Trim(' ', '\t', ',');
    }

    internal static string Capitalize(string text)
    {
        var sb = new StringBuilder(text);
        var capitalizeNext = true;
        for (var i = 0; i < sb.Length; i++)
        {
            var c = sb[i];
            if (capitalizeNext && char.IsLetter(c))
            {
                sb[i] = char.ToUpper(c, CultureInfo.CurrentCulture);
                capitalizeNext = false;
            }
            else if (c == '\n' || (c is '.' or '!' or '?' && (i + 1 == sb.Length || char.IsWhiteSpace(sb[i + 1]))))
            {
                // Só é fim de frase se vier espaço a seguir — não mexe em "gmail.com" ou "v1.2".
                capitalizeNext = true;
            }
            else if (!char.IsWhiteSpace(c) && c != '(' && c != '"')
            {
                capitalizeNext = false;
            }
        }
        return sb.ToString();
    }

    private static string TrimTrailingPeriod(string text) =>
        text.EndsWith('.') && !text.EndsWith("..") ? text[..^1] : text;
}
