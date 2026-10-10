using System.Diagnostics;
using System.Text;
using FlowPT.Core.Abstractions;
using FlowPT.Core.Audio;
using FlowPT.Core.Models;
using Whisper.net;

namespace FlowPT.Transcription;

/// <summary>
/// Transcrição 100% local com whisper.cpp (via Whisper.net). Nenhum áudio sai do PC.
/// O modelo é carregado uma única vez (demora 1–3 s) e reutilizado em todas as ditações.
/// </summary>
public sealed class WhisperLocalSpeechToText : ISpeechToText, IDisposable
{
    private readonly string _modelPath;
    private readonly int _threads;
    private readonly Lazy<WhisperFactory> _factory;

    public WhisperLocalSpeechToText(string modelPath, int threads = 0)
    {
        _modelPath = modelPath;
        _threads = threads > 0 ? threads : Math.Max(2, Environment.ProcessorCount - 2);
        _factory = new Lazy<WhisperFactory>(() =>
        {
            if (!File.Exists(_modelPath))
                throw new FileNotFoundException(
                    $"Modelo Whisper não encontrado. Corre scripts/download-model.ps1. Caminho: {_modelPath}", _modelPath);
            return WhisperFactory.FromPath(_modelPath);
        });
    }

    public string Name => $"whisper-local:{Path.GetFileNameWithoutExtension(_modelPath)}";

    /// <summary>Pré-carrega o modelo em background no arranque, para a 1.ª ditação ser rápida.</summary>
    public Task WarmUpAsync() => Task.Run(() => _ = _factory.Value);

    public async Task<Transcript> TranscribeAsync(AudioClip clip, TranscriptionOptions options, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var builder = _factory.Value.CreateBuilder()
            .WithThreads(_threads)
            .WithLanguage(string.IsNullOrWhiteSpace(options.Language) ? "auto" : options.Language)
            .WithNoContext()
            .WithSingleSegment();

        if (!string.IsNullOrWhiteSpace(options.Prompt))
            builder = builder.WithPrompt(options.Prompt);

        await using var processor = builder.Build();
        using var wav = new MemoryStream(WavEncoder.ToWav(clip));

        var text = new StringBuilder();
        string? language = null;
        await foreach (var segment in processor.ProcessAsync(wav, ct))
        {
            language ??= segment.Language;
            text.Append(segment.Text);
        }

        return new Transcript(CleanWhisperArtifacts(text.ToString()), language, sw.Elapsed);
    }

    /// <summary>O Whisper às vezes "alucina" marcadores em silêncio: [BLANK_AUDIO], (música), etc.</summary>
    internal static string CleanWhisperArtifacts(string text)
    {
        var cleaned = System.Text.RegularExpressions.Regex.Replace(text, @"\[[^\]]*\]|\([^)]*(música|music|silêncio|silence)[^)]*\)", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    public void Dispose()
    {
        if (_factory.IsValueCreated) _factory.Value.Dispose();
    }
}
