using System.Diagnostics;
using FlowSpeech.Core.Abstractions;
using FlowSpeech.Core.Models;
using FlowSpeech.Core.Settings;

namespace FlowSpeech.Core.Pipeline;

/// <summary>
/// Orquestra uma ditação: gravar → transcrever → formatar → inserir no cursor.
/// Não conhece Windows nem o motor de transcrição — só as abstrações.
/// </summary>
public sealed class DictationPipeline
{
    private readonly IAudioRecorder _recorder;
    private readonly ISpeechToText _stt;
    private readonly IReadOnlyList<ITextFormatter> _formatters;
    private readonly ITextInjector _injector;
    private readonly IActiveAppProvider _activeApp;
    private readonly IHistoryStore? _history;
    private readonly Func<FlowSettings> _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private DictationContext _context = DictationContext.Unknown;
    private DictationState _state = DictationState.Idle;

    public DictationPipeline(
        IAudioRecorder recorder,
        ISpeechToText stt,
        IEnumerable<ITextFormatter> formatters,
        ITextInjector injector,
        IActiveAppProvider activeApp,
        Func<FlowSettings> settings,
        IHistoryStore? history = null)
    {
        _recorder = recorder;
        _stt = stt;
        _formatters = formatters.ToList();
        _injector = injector;
        _activeApp = activeApp;
        _settings = settings;
        _history = history;
    }

    public event EventHandler<DictationState>? StateChanged;
    public event EventHandler<string>? Completed;
    public event EventHandler<Exception>? Failed;

    public DictationState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            StateChanged?.Invoke(this, value);
        }
    }

    public bool IsRecording => _recorder.IsRecording;

    public void BeginRecording()
    {
        if (State != DictationState.Idle && State != DictationState.Error) return;

        // Capturar a app ativa ANTES de qualquer janela nossa ganhar foco.
        var ctx = _activeApp.GetCurrentContext();
        _context = ctx with { Style = _settings().StyleFor(ctx.ProcessName) };

        _recorder.Start();
        State = DictationState.Recording;
    }

    /// <summary>Pára a gravação e processa. Devolve o texto inserido (ou vazio).</summary>
    public async Task<string> EndRecordingAsync(CancellationToken ct = default)
    {
        if (!_recorder.IsRecording) return string.Empty;
        var clip = _recorder.Stop();

        if (!await _gate.WaitAsync(0, ct))
        {
            State = DictationState.Idle;
            return string.Empty; // ainda a processar a ditação anterior
        }

        try
        {
            if (clip.IsEmpty || clip.Duration.TotalMilliseconds < _settings().Hotkey.MinRecordingMs)
            {
                State = DictationState.Idle;
                return string.Empty;
            }

            State = DictationState.Transcribing;
            var s = _settings();
            var sw = Stopwatch.StartNew();
            var transcript = await _stt.TranscribeAsync(clip, new TranscriptionOptions(
                s.Transcription.Language,
                BuildPrompt(s)), ct);
            var transcriptionTime = sw.Elapsed;

            State = DictationState.Formatting;
            var text = transcript.Text;
            foreach (var formatter in _formatters)
            {
                text = await formatter.FormatAsync(text, _context, ct);
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                State = DictationState.Injecting;
                await _injector.InjectAsync(text, ct);
            }

            if (_history is not null && s.SaveHistory && !string.IsNullOrWhiteSpace(text))
            {
                await _history.AppendAsync(new HistoryEntry(
                    DateTimeOffset.Now, _context.ProcessName, transcript.Text, text,
                    clip.Duration, transcriptionTime, _stt.Name), ct);
            }

            State = DictationState.Idle;
            Completed?.Invoke(this, text);
            return text;
        }
        catch (Exception ex)
        {
            State = DictationState.Error;
            Failed?.Invoke(this, ex);
            return string.Empty;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Cancel()
    {
        if (_recorder.IsRecording) _recorder.Stop();
        State = DictationState.Idle;
    }

    /// <summary>Vocabulário pessoal enviado aos motores que aceitam prompt (Whisper, cloud).</summary>
    internal static string? BuildPrompt(FlowSettings s)
    {
        var words = s.Dictionary.Values.Distinct().Take(50).ToList();
        return words.Count == 0 ? null : string.Join(", ", words) + ".";
    }
}
