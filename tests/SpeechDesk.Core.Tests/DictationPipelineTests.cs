using SpeechDesk.Core.Abstractions;
using SpeechDesk.Core.Models;
using SpeechDesk.Core.Pipeline;
using SpeechDesk.Core.Settings;
using SpeechDesk.Core.Text;
using Xunit;

namespace SpeechDesk.Core.Tests;

public class DictationPipelineTests
{
    private sealed class FakeRecorder(int durationMs) : IAudioRecorder
    {
        public event EventHandler<float>? LevelChanged;
        public bool IsRecording { get; private set; }
        public void Start() { IsRecording = true; LevelChanged?.Invoke(this, 0.5f); }
        public AudioClip Stop()
        {
            IsRecording = false;
            return new AudioClip(new byte[16000 * 2 * durationMs / 1000]);
        }
        public void Dispose() { }
    }

    private sealed class FakeStt(string text, bool canTranslate = false) : ISpeechToText
    {
        public string Name => "fake";
        public bool CanTranslateToEnglish => canTranslate;
        public TranscriptionOptions? LastOptions { get; private set; }
        public Task<Transcript> TranscribeAsync(AudioClip clip, TranscriptionOptions options, CancellationToken ct = default)
        {
            LastOptions = options;
            return Task.FromResult(new Transcript(text, "pt", TimeSpan.FromMilliseconds(10)));
        }
    }

    private sealed class FakeInjector : ITextInjector
    {
        public List<string> Injected { get; } = [];
        public Task InjectAsync(string text, CancellationToken ct = default) { Injected.Add(text); return Task.CompletedTask; }
    }

    private sealed class FakeApp(string process) : IActiveAppProvider
    {
        public DictationContext GetCurrentContext() => new(process, "janela");
    }

    private sealed class FakeHistory : IHistoryStore
    {
        public List<HistoryEntry> Entries { get; } = [];
        public Task AppendAsync(HistoryEntry entry, CancellationToken ct = default) { Entries.Add(entry); return Task.CompletedTask; }
        public Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int count, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<HistoryEntry>>(Entries);
    }

    private static (DictationPipeline Pipeline, FakeInjector Injector, FakeHistory History, FakeStt Stt) Build(
        string transcript, int durationMs = 1500, string process = "notepad")
    {
        var settings = new FlowSettings();
        var injector = new FakeInjector();
        var history = new FakeHistory();
        var stt = new FakeStt(transcript);
        var pipeline = new DictationPipeline(
            new FakeRecorder(durationMs), stt,
            [new RuleBasedFormatter(() => settings)],
            injector, new FakeApp(process), () => settings, history);
        return (pipeline, injector, history, stt);
    }

    [Fact]
    public async Task Full_flow_injects_formatted_text_and_records_history()
    {
        var (pipeline, injector, history, _) = Build("hum olá vírgula mundo");
        var states = new List<DictationState>();
        pipeline.StateChanged += (_, s) => states.Add(s);

        pipeline.BeginRecording();
        var result = await pipeline.EndRecordingAsync();

        Assert.Equal("Olá, mundo", result);
        Assert.Equal(["Olá, mundo"], injector.Injected);
        Assert.Single(history.Entries);
        Assert.Equal("notepad", history.Entries[0].App);
        Assert.Equal(
            [DictationState.Recording, DictationState.Transcribing, DictationState.Formatting, DictationState.Injecting, DictationState.Idle],
            states);
    }

    [Fact]
    public async Task Short_recordings_are_discarded()
    {
        var (pipeline, injector, _, _) = Build("olá", durationMs: 100);
        pipeline.BeginRecording();
        var result = await pipeline.EndRecordingAsync();

        Assert.Equal(string.Empty, result);
        Assert.Empty(injector.Injected);
        Assert.Equal(DictationState.Idle, pipeline.State);
    }

    [Fact]
    public async Task Personal_dictionary_is_sent_as_prompt_to_whisper()
    {
        var (pipeline, _, _, stt) = Build("teste");
        pipeline.BeginRecording();
        await pipeline.EndRecordingAsync();

        Assert.Contains("SpeechDesk", stt.LastOptions!.Prompt);
        Assert.Contains(".NET", stt.LastOptions!.Prompt);
    }

    [Fact]
    public void Cancel_stops_recording_without_injecting()
    {
        var (pipeline, injector, _, _) = Build("olá");
        pipeline.BeginRecording();
        pipeline.Cancel();

        Assert.False(pipeline.IsRecording);
        Assert.Empty(injector.Injected);
        Assert.Equal(DictationState.Idle, pipeline.State);
    }

    private sealed class CapturingFormatter : ITextFormatter
    {
        public string Name => "capture";
        public DictationContext? Context { get; private set; }
        public Task<string> FormatAsync(string text, DictationContext context, CancellationToken ct = default)
        {
            Context = context;
            return Task.FromResult(text);
        }
    }

    private static (FakeStt Stt, CapturingFormatter Formatter) RunWithTranslation(bool engineCanTranslate)
    {
        var settings = new FlowSettings();
        settings.Translation.Enabled = true;
        var stt = new FakeStt("olá", engineCanTranslate);
        var formatter = new CapturingFormatter();
        var pipeline = new DictationPipeline(
            new FakeRecorder(1500), stt, [formatter], new FakeInjector(), new FakeApp("notepad"), () => settings);

        pipeline.BeginRecording();
        pipeline.EndRecordingAsync().GetAwaiter().GetResult();
        return (stt, formatter);
    }

    [Fact]
    public void Translation_uses_engine_natively_when_supported()
    {
        var (stt, formatter) = RunWithTranslation(engineCanTranslate: true);

        Assert.True(stt.LastOptions!.TranslateToEnglish);
        Assert.Null(stt.LastOptions!.Prompt);
        Assert.Null(formatter.Context!.TranslateTo);
        Assert.True(formatter.Context!.IsTranslated);
    }

    [Fact]
    public void Translation_falls_back_to_ai_when_engine_cannot_translate()
    {
        var (stt, formatter) = RunWithTranslation(engineCanTranslate: false);

        Assert.False(stt.LastOptions!.TranslateToEnglish);
        Assert.Equal("English", formatter.Context!.TranslateTo);
        Assert.True(formatter.Context!.IsTranslated);
    }
}
