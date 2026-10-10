using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SpeechDesk.Core.Abstractions;
using SpeechDesk.Core.Audio;
using SpeechDesk.Core.Localization;
using SpeechDesk.Core.Models;
using SherpaOnnx;

namespace SpeechDesk.Transcription;

/// <summary>
/// Motor por omissão: NVIDIA Parakeet TDT 0.6B v3 (CC-BY-4.0), executado localmente com sherpa-onnx.
/// 25 línguas europeias incluindo português (treinado com português europeu) e inglês, deteção de
/// língua automática, pontuação e maiúsculas incluídas. Muito mais rápido que o Whisper em CPU.
/// </summary>
public sealed class ParakeetSpeechToText : ISpeechToText, IDisposable
{
    private readonly string _modelDir;
    private readonly int _threads;
    private readonly Lazy<OfflineRecognizer> _recognizer;
    private readonly object _decodeLock = new();

    public ParakeetSpeechToText(string modelDir, int threads = 0)
    {
        _modelDir = modelDir;
        _threads = threads > 0 ? threads : Math.Clamp(Environment.ProcessorCount / 2, 2, 8);
        // PublicationOnly: um erro (ex.: modelo em falta) não fica em cache — depois do download volta a tentar.
        _recognizer = new Lazy<OfflineRecognizer>(Create, LazyThreadSafetyMode.PublicationOnly);
    }

    public string Name => "parakeet-tdt-0.6b-v3";

    /// <summary>Carrega o modelo em background no arranque, para a 1.ª ditação ser imediata.</summary>
    public Task WarmUpAsync() => Task.Run(() => _ = _recognizer.Value);

    public Task<Transcript> TranscribeAsync(AudioClip clip, TranscriptionOptions options, CancellationToken ct = default)
    {
        // O Parakeet não aceita prompt nem língua forçada: o dicionário pessoal é aplicado depois, nas regras.
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var sw = Stopwatch.StartNew();
            var samples = AudioPreprocessor.Prepare(clip.Pcm16, clip.SampleRate);

            string text;
            lock (_decodeLock)
            {
                using var stream = _recognizer.Value.CreateStream();
                stream.AcceptWaveform(clip.SampleRate, samples);
                _recognizer.Value.Decode(stream);
                text = stream.Result.Text;
            }

            return new Transcript(text.Trim(), null, sw.Elapsed);
        }, ct);
    }

    private OfflineRecognizer Create()
    {
        string File(string name)
        {
            var path = Path.Combine(_modelDir, name);
            if (!System.IO.File.Exists(path))
                throw new FileNotFoundException(L.T("model.missing.parakeet", path), path);
            return NativePath(path);
        }

        var config = new OfflineRecognizerConfig();
        config.FeatConfig.SampleRate = 16000;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Transducer.Encoder = File("encoder.int8.onnx");
        config.ModelConfig.Transducer.Decoder = File("decoder.int8.onnx");
        config.ModelConfig.Transducer.Joiner = File("joiner.int8.onnx");
        config.ModelConfig.Tokens = File("tokens.txt");
        config.ModelConfig.ModelType = "nemo_transducer";
        config.ModelConfig.NumThreads = _threads;
        config.ModelConfig.Provider = "cpu";
        config.ModelConfig.Debug = 0;
        config.DecodingMethod = "greedy_search";
        return new OfflineRecognizer(config);
    }

    /// <summary>
    /// O sherpa-onnx abre os ficheiros com a code page ANSI do Windows: um caminho com acentos
    /// (ex.: C:\Users\Fábio\...) não é encontrado. Usa-se o nome curto 8.3, que é só ASCII.
    /// </summary>
    private static string NativePath(string path)
    {
        if (path.All(char.IsAscii) || !OperatingSystem.IsWindows()) return path;

        var buffer = new StringBuilder(1024);
        var length = GetShortPathName(path, buffer, buffer.Capacity);
        var shortPath = length > 0 && length < buffer.Capacity ? buffer.ToString() : path;
        if (!shortPath.All(char.IsAscii))
            throw new IOException(L.T("model.nonAsciiPath", path));
        return shortPath;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetShortPathName(string longPath, StringBuilder shortPath, int bufferSize);

    public void Dispose()
    {
        if (_recognizer.IsValueCreated) _recognizer.Value.Dispose();
    }
}
