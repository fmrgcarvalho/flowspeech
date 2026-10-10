namespace SpeechDesk.Core.Models;

/// <summary>Áudio PCM 16-bit little-endian, mono.</summary>
public sealed record AudioClip(byte[] Pcm16, int SampleRate = 16000)
{
    public TimeSpan Duration => TimeSpan.FromSeconds(Pcm16.Length / 2.0 / SampleRate);
    public bool IsEmpty => Pcm16.Length == 0;
}

public sealed record Transcript(string Text, string? Language, TimeSpan Elapsed);

public sealed record TranscriptionOptions(
    string Language = "auto",
    /// <summary>Texto que orienta o modelo (vocabulário pessoal, nomes próprios).</summary>
    string? Prompt = null,
    /// <summary>Pedir ao motor que devolva já em inglês (só motores com tradução nativa).</summary>
    bool TranslateToEnglish = false);

public enum WritingStyle
{
    /// <summary>Texto cru, só limpeza básica.</summary>
    Raw,
    Formal,
    Casual,
    /// <summary>IDEs/terminais: sem pontuação "inteligente", preserva identificadores.</summary>
    Code
}

public sealed record DictationContext(
    string ProcessName,
    string WindowTitle,
    WritingStyle Style = WritingStyle.Raw,
    /// <summary>Língua para a qual os formatadores devem traduzir (null = manter a original).</summary>
    string? TranslateTo = null,
    /// <summary>True quando o texto final vai estar traduzido (pela IA ou pelo próprio motor).</summary>
    bool IsTranslated = false)
{
    public static DictationContext Unknown { get; } = new("unknown", string.Empty);
}

public sealed record HistoryEntry(
    DateTimeOffset Timestamp,
    string App,
    string RawText,
    string FinalText,
    TimeSpan AudioDuration,
    TimeSpan TranscriptionTime,
    string Engine);

public enum DictationState
{
    Idle,
    Recording,
    Transcribing,
    Formatting,
    Injecting,
    Error
}

public enum InjectionMode
{
    /// <summary>Copia para o clipboard, envia Ctrl+V e repõe o clipboard original. Rápido e fiável.</summary>
    Paste,
    /// <summary>Escreve carácter a carácter (Unicode). Para apps que bloqueiam o paste.</summary>
    Type
}
