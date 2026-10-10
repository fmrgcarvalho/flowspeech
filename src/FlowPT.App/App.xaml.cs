using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using FlowPT.Core.Abstractions;
using FlowPT.Core.History;
using FlowPT.Core.Pipeline;
using FlowPT.Core.Settings;
using FlowPT.Core.Text;
using FlowPT.Formatting;
using FlowPT.Platform.Windows;
using FlowPT.Transcription;

namespace FlowPT.App;

/// <summary>Composition root: liga as peças e arranca em segundo plano (só ícone na barra de tarefas).</summary>
public partial class App : Application
{
    private Mutex? _singleInstance;
    private FlowSettings _settings = new();
    private TrayIcon? _tray;
    private OverlayWindow? _overlay;
    private DictationController? _controller;
    private readonly List<IDisposable> _disposables = [];

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\FlowPT.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("O FlowPT já está a correr (ícone junto ao relógio).", "FlowPT");
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

        var formatters = new List<ITextFormatter>
        {
            new RuleBasedFormatter(settings),
            new OllamaFormatter(http, settings)
        };

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

        _tray = new TrayIcon(ReloadSettings, () => Shutdown());
        _controller = new DictationController(pipeline, hotkey, _overlay, _tray);

        hotkey.Start();
        _tray.ShowInfo("FlowPT pronto. Mantém Right Ctrl premido para ditar; dois toques = mãos-livres.");
    }

    private ISpeechToText CreateSpeechToText(FlowSettings s, HttpClient http)
    {
        if (s.Transcription.Engine.Equals("cloud", StringComparison.OrdinalIgnoreCase))
            return new CloudSpeechToText(http, s.Transcription.Cloud);

        var modelPath = SettingsStore.ResolveModelPath(s.Transcription.ModelPath);
        var whisper = new WhisperLocalSpeechToText(modelPath, s.Transcription.Threads);
        _disposables.Add(whisper);

        // Carregar o modelo em background; se faltar, avisar logo em vez de na 1.ª ditação.
        _ = whisper.WarmUpAsync().ContinueWith(
            t => Dispatcher.BeginInvoke(() => _tray?.ShowError(t.Exception!.GetBaseException().Message)),
            TaskContinuationOptions.OnlyOnFaulted);
        return whisper;
    }

    private void ReloadSettings()
    {
        _settings = SettingsStore.Load();
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
