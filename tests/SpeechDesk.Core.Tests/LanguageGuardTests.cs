using SpeechDesk.Core.Abstractions;
using SpeechDesk.Core.Models;
using SpeechDesk.Core.Pipeline;
using SpeechDesk.Core.Settings;
using SpeechDesk.Core.Text;
using Xunit;

namespace SpeechDesk.Core.Tests;

public class LanguageGuardTests
{
    // Frases reais do histórico, ditas em português.
    [Theory]
    [InlineData("Faz favor amanhã criar uma nota.", "pt")]
    [InlineData("Não, não é inglês.", "pt")]
    [InlineData("É um teste.", "pt")]
    [InlineData("Três são dois três e estão.", "pt")]
    [InlineData("No debo ir.", null)]
    [InlineData("And now they've lied.", "en")]
    [InlineData("Es a un test.", "es")]
    [InlineData("Ask not what your country can do for you.", "en")]
    [InlineData("No preguntes qué puede hacer tu país por ti.", "es")]
    [InlineData("Alles hat ein Ende, nur die Wurst hat zwei.", "de")]
    public void Guesses_language_of_short_sentences(string text, string? expected) =>
        Assert.Equal(expected, TextLanguage.Guess(text));

    private sealed class FixedStt(string text) : ISpeechToText
    {
        public string Name => "fixed";
        public int Calls { get; private set; }
        public TranscriptionOptions? LastOptions { get; private set; }
        public Task<Transcript> TranscribeAsync(AudioClip clip, TranscriptionOptions options, CancellationToken ct = default)
        {
            Calls++;
            LastOptions = options;
            return Task.FromResult(new Transcript(text, null, TimeSpan.FromMilliseconds(100)));
        }
    }

    private static (LanguageGuardSpeechToText Guard, FixedStt Fallback) Build(string primaryText, string languages, bool fallbackInstalled = true)
    {
        var settings = new AppSettings();
        settings.Transcription.Languages = languages;
        var fallback = new FixedStt("Não devo ir.");
        var guard = new LanguageGuardSpeechToText(new FixedStt(primaryText), () => fallbackInstalled ? fallback : null, () => settings);
        return (guard, fallback);
    }

    private static readonly AudioClip Clip = new(new byte[32000]);

    [Fact]
    public async Task Wrong_language_is_retried_with_the_pinned_language()
    {
        var (guard, fallback) = Build("No debo ir.", "pt");

        var result = await guard.TranscribeAsync(Clip, new TranscriptionOptions());

        Assert.Equal("Não devo ir.", result.Text);
        Assert.Equal("pt", fallback.LastOptions!.Language);
        Assert.Contains("português de Portugal", fallback.LastOptions.Prompt);
    }

    [Fact]
    public async Task Right_language_skips_the_slow_engine()
    {
        var (guard, fallback) = Build("Faz favor amanhã criar uma nota.", "pt,en");

        var result = await guard.TranscribeAsync(Clip, new TranscriptionOptions());

        Assert.Equal("Faz favor amanhã criar uma nota.", result.Text);
        Assert.Equal(0, fallback.Calls);
    }

    [Theory]
    [InlineData("")]      // sem línguas definidas: comportamento antigo
    [InlineData("auto")]
    public async Task No_pinned_language_keeps_parakeet_output(string languages)
    {
        var (guard, fallback) = Build("No debo ir.", languages);

        Assert.Equal("No debo ir.", (await guard.TranscribeAsync(Clip, new TranscriptionOptions())).Text);
        Assert.Equal(0, fallback.Calls);
    }

    [Fact]
    public async Task Without_whisper_model_keeps_parakeet_output()
    {
        var (guard, _) = Build("No debo ir.", "pt", fallbackInstalled: false);

        Assert.Equal("No debo ir.", (await guard.TranscribeAsync(Clip, new TranscriptionOptions())).Text);
    }
}
