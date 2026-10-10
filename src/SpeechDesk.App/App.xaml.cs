using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using SpeechDesk.AiCleanup;
using SpeechDesk.Core.Abstractions;
using SpeechDesk.Core.History;
using SpeechDesk.Core.Localization;
using SpeechDesk.Core.Pipeline;
using SpeechDesk.Core.Settings;
using SpeechDesk.Core.Text;
using SpeechDesk.Platform.Windows;
using SpeechDesk.Transcription;

namespace SpeechDesk.App;

/// <summary>Composition root: wires everything up and runs in the background (tray icon only).</summary>
public partial class App : Application
{
    private Mutex? _singleInstance;
    private AppSettings _settings = new();
    private TrayIcon? _tray;
    private LocalAiFormatter? _ai;
    private ISpeechToText? _stt;
    private ParakeetSpeechToText? _parakeet;
    private WhisperLocalSpeechToText? _languageFallback;
    private readonly object _fallbackLock = new();
    private OverlayWindow? _overlay;
    private DictationController? _controller;
    private ModelDownloader? _downloader;
    private bool _downloading;
    private readonly List<IDisposable> _disposables = [];

    /// <summary>Translation only needs the AI model when the engine cannot translate by itself (e.g. Parakeet).</summary>
    private bool TranslationNeedsAi => _settings.Translation.Enabled && _stt?.CanTranslateToEnglish != true;

    private bool AiModelNeeded => _settings.Formatting.Ai.Enabled || TranslationNeedsAi;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = SettingsStore.Load();
        L.Language = _settings.UiLanguage;

        _singleInstance = new Mutex(true, @"Local\SpeechDesk.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show(L.T("app.alreadyRunning"), "SpeechDesk");
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

        Func<AppSettings> settings = () => _settings;

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _downloader = new ModelDownloader(new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
        _stt = CreateSpeechToText(_settings, http);

        // AI (optional) first, rules last: the dictionary and snippets always have the final say.
        _ai = new LocalAiFormatter(settings);
        _ai.LoadFailed += (_, message) => Dispatcher.BeginInvoke(() => _tray?.ShowError(message));
        _ai.Warning += (_, message) => Dispatcher.BeginInvoke(() => _tray?.ShowError(message));
        _disposables.Add(_ai);
        var formatters = new List<ITextFormatter> { _ai, new RuleBasedFormatter(settings) };

        var recorder = new NAudioRecorder(() => _settings.Microphone, () => _settings.MicrophoneWarmSeconds);
        var hotkey = new LowLevelHotkeyListener(() => _settings.Hotkey.VirtualKey, () => _settings.Hotkey.DoubleTapMs);
        _disposables.AddRange([recorder, hotkey]);

        var pipeline = new DictationPipeline(
            recorder,
            _stt,
            formatters,
            new ClipboardTextInjector(Dispatcher, () => _settings.Injection),
            new ForegroundAppProvider(),
            settings,
            new JsonlHistoryStore(Path.Combine(SettingsStore.AppDataDir, "history.jsonl")));

        _overlay = new OverlayWindow { TranslationOn = _settings.Translation.Enabled };
        recorder.LevelChanged += (_, level) => Dispatcher.BeginInvoke(() => _overlay.SetLevel(level));

        _tray = new TrayIcon(new TrayActions(
            ReloadSettings: ReloadSettings,
            Exit: () => Shutdown(),
            AiEnabled: () => _settings.Formatting.Ai.Enabled,
            SetAiEnabled: enabled => _ = SetAiEnabledAsync(enabled),
            TranslationEnabled: () => _settings.Translation.Enabled,
            SetTranslationEnabled: enabled => _ = SetTranslationEnabledAsync(enabled),
            Language: () => L.Language,
            SetLanguage: SetLanguage,
            Microphone: () => _settings.Microphone,
            SetMicrophone: SetMicrophone,
            DownloadModels: () => _ = EnsureModelsAsync(askFirst: false)));
        _controller = new DictationController(pipeline, hotkey, _overlay, _tray);

        hotkey.Start();
        _tray.ShowInfo(L.T("app.ready"));

        await EnsureModelsAsync(askFirst: true);
    }

    private ISpeechToText CreateSpeechToText(AppSettings s, HttpClient http)
    {
        var t = s.Transcription;
        switch (t.Engine.ToLowerInvariant())
        {
            case TranscriptionEngines.Cloud:
                return new CloudSpeechToText(http, t.Cloud);

            case TranscriptionEngines.Whisper:
                var whisper = new WhisperLocalSpeechToText(SettingsStore.ResolveModelPath(t.WhisperModelPath), t.Threads);
                _disposables.Add(whisper);
                return whisper;

            default:
                _parakeet = new ParakeetSpeechToText(SettingsStore.ResolveModelPath(t.ParakeetModelDir), t.Threads);
                _disposables.Add(_parakeet);
                return new LanguageGuardSpeechToText(_parakeet, LanguageFallback, () => _settings);
        }
    }

    private bool LanguagesPinned => !string.IsNullOrWhiteSpace(_settings.Transcription.Languages);

    /// <summary>Whisper para repetir frases que o Parakeet escreveu na língua errada; null sem o modelo.</summary>
    private ISpeechToText? LanguageFallback()
    {
        if (!ModelCatalog.Whisper.IsInstalled(_settings)) return null;
        lock (_fallbackLock)
        {
            if (_languageFallback is null)
            {
                var t = _settings.Transcription;
                _languageFallback = new WhisperLocalSpeechToText(SettingsStore.ResolveModelPath(t.WhisperModelPath), t.Threads);
                _disposables.Add(_languageFallback);
            }
            return _languageFallback;
        }
    }

    /// <summary>
    /// Makes sure the models the current settings need are installed (offering to download them),
    /// then loads them in the background so the first dictation is fast.
    /// </summary>
    private async Task EnsureModelsAsync(bool askFirst)
    {
        if (_downloading) return;

        var speech = ModelCatalog.SpeechModelFor(_settings);
        if (speech is not null && !await EnsureInstalledAsync(speech, askFirst)) return;
        WarmUpSpeech();

        if (_parakeet is not null && LanguagesPinned && await EnsureInstalledAsync(ModelCatalog.Whisper, askFirst))
            _ = (LanguageFallback() as WhisperLocalSpeechToText)?.WarmUpAsync();

        if (AiModelNeeded && await EnsureInstalledAsync(ModelCatalog.Ai, askFirst))
            _ = _ai?.WarmUpAsync(force: true);
    }

    private async Task<bool> EnsureInstalledAsync(ModelSpec spec, bool askFirst)
    {
        if (spec.IsInstalled(_settings)) return true;

        if (askFirst)
        {
            var answer = MessageBox.Show(L.T(spec.AskKey), L.T("model.ask.title"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                _tray?.ShowInfo(L.T("model.declined"));
                return false;
            }
        }

        _downloading = true;
        try
        {
            var progress = new Progress<int>(p => _overlay?.ShowProgress(p));
            await _downloader!.DownloadAsync(spec, _settings, progress);
            _tray?.ShowInfo(L.T("model.downloaded", spec.DisplayName));
            return true;
        }
        catch (Exception ex)
        {
            _tray?.ShowError(L.T("model.downloadFailed", ex.Message));
            return false;
        }
        finally
        {
            _downloading = false;
            _overlay?.HideProgress();
        }
    }

    private void WarmUpSpeech()
    {
        var warmUp = _stt switch
        {
            WhisperLocalSpeechToText w => w.WarmUpAsync(),
            _ => _parakeet?.WarmUpAsync() ?? Task.CompletedTask
        };
        warmUp.ContinueWith(
            t => Dispatcher.BeginInvoke(() => _tray?.ShowError(t.Exception!.GetBaseException().Message)),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task SetAiEnabledAsync(bool enabled)
    {
        _settings.Formatting.Ai.Enabled = enabled;
        SettingsStore.Save(_settings);
        _tray?.ShowInfo(L.T(enabled ? "ai.on" : "ai.off"));
        if (enabled && await EnsureInstalledAsync(ModelCatalog.Ai, askFirst: true))
            _ = _ai?.WarmUpAsync();
    }

    private async Task SetTranslationEnabledAsync(bool enabled)
    {
        _settings.Translation.Enabled = enabled;
        SettingsStore.Save(_settings);
        if (_overlay is not null) _overlay.TranslationOn = enabled;

        if (!enabled)
        {
            _tray?.ShowInfo(L.T("translation.off"));
        }
        else if (TranslationNeedsAi)
        {
            _tray?.ShowInfo(L.T("translation.onAi", _settings.Translation.TargetLanguage));
            if (await EnsureInstalledAsync(ModelCatalog.Ai, askFirst: true))
                _ = _ai?.WarmUpAsync(force: true);
        }
        else
        {
            _tray?.ShowInfo(L.T("translation.onWhisper"));
        }
    }

    private void SetLanguage(string language)
    {
        L.Language = language;
        _settings.UiLanguage = L.Language;
        SettingsStore.Save(_settings);
        _tray?.ApplyLanguage();
        _tray?.ShowInfo(L.T("app.languageChanged"));
    }

    private void SetMicrophone(string name)
    {
        _settings.Microphone = name;
        SettingsStore.Save(_settings);
        _tray?.ShowInfo(L.T("app.microphoneChanged", string.IsNullOrWhiteSpace(name) ? L.T("tray.microphoneDefault") : name));
    }

    private void ReloadSettings()
    {
        _settings = SettingsStore.Load();
        L.Language = _settings.UiLanguage;
        _tray?.ApplyLanguage();
        if (_overlay is not null) _overlay.TranslationOn = _settings.Translation.Enabled;
        _tray?.ShowInfo(L.T("app.settingsReloaded"));
        _ = EnsureModelsAsync(askFirst: true);
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
