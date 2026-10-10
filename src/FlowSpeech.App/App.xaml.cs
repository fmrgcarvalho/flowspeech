using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using FlowSpeech.AiCleanup;
using FlowSpeech.Core.Abstractions;
using FlowSpeech.Core.History;
using FlowSpeech.Core.Pipeline;
using FlowSpeech.Core.Settings;
using FlowSpeech.Core.Text;
using FlowSpeech.Platform.Windows;
using FlowSpeech.Transcription;

namespace FlowSpeech.App;

/// <summary>Composition root: liga as peças e arranca em segundo plano (só ícone na barra de tarefas).</summary>
public partial class App : Application
{
    private Mutex? _singleInstance;
    private FlowSettings _settings = new();
    private TrayIcon? _tray;
    private LocalAiFormatter? _ai;
    private OverlayWindow? _overlay;
    private DictationController? _controller;
    private readonly List<IDisposable> _disposables = [];

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\FlowSpeech.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("O FlowSpeech já está a correr (ícone junto ao relógio).", "FlowSpeech");
            Shutdown();
            return;
        }

        if (SynchronizationContext.Current is null)
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher));

        DispatcherUnhandledException += (_, args) =>
        {
            _tray?.ShowError(args.Exception.Message);
            args.Handled = true;
        };

        _settings = SettingsStore.Load();
        Func<FlowSettings> settings = () => _settings;

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var stt = CreateSpeechToText(_settings, http);

        // IA (opcional) primeiro, regras depois: o dicionário e os snippets têm sempre a última palavra.
        _ai = new LocalAiFormatter(settings);
        _ai.LoadFailed += (_, message) => Dispatcher.BeginInvoke(() => _tray?.ShowError(message));
        _disposables.Add(_ai);
        var formatters = new List<ITextFormatter> { _ai, new RuleBasedFormatter(settings) };

        var recorder = new NAudioRecorder();
        var hotkey = new LowLevelHotkeyListener(() => _settings.Hotkey.VirtualKey, () => _settings.Hotkey.DoubleTapMs);
        _disposables.AddRange([recorder, hotkey]);

        var pipeline = new DictationPipeline(
            recorder,
            stt,
            formatters,
            new ClipboardTextInjector(Dispatcher, () => _settings.Injection),
            new ForegroundAppProvider(),
            settings,
            new JsonlHistoryStore(Path.Combine(SettingsStore.AppDataDir, "history.jsonl")));

        _overlay = new OverlayWindow();
        recorder.LevelChanged += (_, level) => Dispatcher.BeginInvoke(() => _overlay.SetLevel(level));

        _tray = new TrayIcon(ReloadSettings, () => Shutdown(), () => _settings.Formatting.Ai.Enabled, SetAiEnabled);
        _controller = new DictationController(pipeline, hotkey, _overlay, _tray);

        hotkey.Start();
        _ = _ai.WarmUpAsync();
        _tray.ShowInfo("FlowSpeech pronto. Mantém Right Ctrl premido para ditar; dois toques = mãos-livres.");
    }

    private ISpeechToText CreateSpeechToText(FlowSettings s, HttpClient http)
    {
        var t = s.Transcription;
        switch (t.Engine.ToLowerInvariant())
        {
            case TranscriptionEngines.Cloud:
                return new CloudSpeechToText(http, t.Cloud);

            case TranscriptionEngines.Whisper:
                var whisper = new WhisperLocalSpeechToText(SettingsStore.ResolveModelPath(t.WhisperModelPath), t.Threads);
                _disposables.Add(whisper);
                WarmUp(whisper.WarmUpAsync());
                return whisper;

            default:
                var parakeet = new ParakeetSpeechToText(SettingsStore.ResolveModelPath(t.ParakeetModelDir), t.Threads);
                _disposables.Add(parakeet);
                WarmUp(parakeet.WarmUpAsync());
                return parakeet;
        }
    }

    /// <summary>Carrega o modelo em background; se faltar, avisa logo em vez de na 1.ª ditação.</summary>
    private void WarmUp(Task warmUp) =>
        warmUp.ContinueWith(
            t => Dispatcher.BeginInvoke(() => _tray?.ShowError(t.Exception!.GetBaseException().Message)),
            TaskContinuationOptions.OnlyOnFaulted);

    private void SetAiEnabled(bool enabled)
    {
        _settings.Formatting.Ai.Enabled = enabled;
        SettingsStore.Save(_settings);
        if (enabled)
        {
            _tray?.ShowInfo("Limpeza com IA ligada. A carregar o modelo…");
            _ = _ai?.WarmUpAsync();
        }
        else
        {
            _tray?.ShowInfo("Limpeza com IA desligada — só regras.");
        }
    }

    private void ReloadSettings()
    {
        _settings = SettingsStore.Load();
        _tray?.SyncAiToggle();
        _ = _ai?.WarmUpAsync();
        _tray?.ShowInfo("Definições recarregadas. (Mudar o motor ou o modelo requer reiniciar.)");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        foreach (var d in _disposables) d.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
