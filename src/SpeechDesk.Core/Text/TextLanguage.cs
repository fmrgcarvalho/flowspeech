using System.Text.RegularExpressions;

namespace SpeechDesk.Core.Text;

/// <summary>
/// Adivinha a língua de uma frase curta pelas palavras funcionais e acentos típicos.
/// Serve para apanhar o Parakeet quando troca de língua (português ouvido como espanhol ou inglês).
/// </summary>
public static partial class TextLanguage
{
    // Só palavras que não existem (ou são raras) nas outras línguas da lista.
    private static readonly Dictionary<string, HashSet<string>> Words = new()
    {
        ["pt"] = Set("o os as um uma uns umas é não sim eu tu você ele ela nós eles elas do da dos das no na nos nas ao aos pelo pela " +
                     "com para isso isto este esta esse essa aquele aquilo muito muita mas também já mais ou foi vai vou estou está " +
                     "estão são tem têm temos fazer faz favor obrigado obrigada amanhã hoje ontem aqui agora então porque quando onde " +
                     "como meu minha teu tua seu sua nosso nossa bem bom boa depois ainda só até sobre entre"),
        ["es"] = Set("el los las un una es y pero muy mucho mucha yo usted él ella nosotros ellos del al por para con eso esto " +
                     "este esta ese esa también ya más fue va voy estoy está están son tiene tenemos hacer hace gracias mañana hoy " +
                     "ayer aquí ahora entonces porque cuando donde como mi tu su nuestro bien bueno buena después todavía solo hasta " +
                     "debo puedo quiero sí"),
        ["en"] = Set("the an is are was were be i you he she we they it of to in on at for with and but or not no yes this that " +
                     "these those my your his her our their have has had do does did will would can could should what when where " +
                     "why how there here now then just like so very bye hello thanks test"),
        ["fr"] = Set("le la les un une des est et mais très je tu il elle nous vous ils elles du au aux pour avec ce cette ces " +
                     "pas oui non mon ma mes ton ta son sa notre votre être avoir fait faire merci demain aujourd'hui ici maintenant"),
        ["de"] = Set("der die das ein eine einen ist und aber sehr ich du er sie wir ihr es nicht ja nein mit für auf von zu " +
                     "dem den des im am bin bist sind hat haben danke morgen heute hier jetzt auch noch nur"),
    };

    private static HashSet<string> Set(string words) => new(words.Split(' '), StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"[\p{L}']+")] private static partial Regex WordRegex();

    /// <summary>Código ISO de duas letras (pt, es, en, fr, de) ou null quando não há indícios suficientes.</summary>
    public static string? Guess(string text)
    {
        var scores = Words.Keys.ToDictionary(k => k, _ => 0.0);
        foreach (Match m in WordRegex().Matches(text))
        {
            var word = m.Value.ToLowerInvariant();
            foreach (var (language, words) in Words)
                if (words.Contains(word)) scores[language] += 1;

            // Letras que denunciam a língua mesmo em palavras que não estão na lista.
            if (word.IndexOfAny(['ã', 'õ']) >= 0) scores["pt"] += 2;
            if (word.Contains('ç')) { scores["pt"] += 1; scores["fr"] += 1; }
            if (word.Contains('ñ')) scores["es"] += 2;
            if (word.IndexOfAny(['ä', 'ö', 'ü', 'ß']) >= 0) scores["de"] += 2;
        }
        if (text.IndexOfAny(['¿', '¡']) >= 0) scores["es"] += 2;

        var ranked = scores.OrderByDescending(kv => kv.Value).ToList();
        // Empate ou nenhum indício: não arrisca.
        if (ranked[0].Value <= 0 || ranked[0].Value == ranked[1].Value) return null;
        return ranked[0].Key;
    }
}
