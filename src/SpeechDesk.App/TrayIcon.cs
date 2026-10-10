using System.Diagnostics;
using System.IO;
using SpeechDesk.Core.Localization;
using SpeechDesk.Core.Models;
using SpeechDesk.Core.Settings;
using Forms = System.Windows.Forms;

namespace SpeechDesk.App;

/// <summary>Callbacks the tray menu needs from the app.</summary>
public sealed record TrayActions(
    Action ReloadSettings,
    Action Exit,
    Func<bool> AiEnabled,
    Action<bool> SetAiEnabled,
    Func<bool> TranslationEnabled,
    Action<bool> SetTranslationEnabled,
    Func<string> Language,
    Action<string> SetLanguage,
    Action DownloadModels);

/// <summary>Notification-area icon and its menu. All texts come from <see cref="L"/>.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly TrayActions _actions;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _status = new() { Enabled = false };
    private readonly Forms.ToolStripMenuItem _aiToggle = new() { CheckOnClick = true };
    private readonly Forms.ToolStripMenuItem _translateToggle = new() { CheckOnClick = true };
    private readonly Forms.ToolStripMenuItem _language = new();
    private readonly Forms.ToolStripMenuItem _english = new("English");
    private readonly Forms.ToolStripMenuItem _portuguese = new("Português");
    private readonly Forms.ToolStripMenuItem _downloadModels = new();
    private readonly Forms.ToolStripMenuItem _openSettings = new();
    private readonly Forms.ToolStripMenuItem _reloadSettings = new();
    private readonly Forms.ToolStripMenuItem _openHistory = new();
    private readonly Forms.ToolStripMenuItem _modelsFolder = new();
    private readonly Forms.ToolStripMenuItem _exit = new();
    private DictationState _state = DictationState.Idle;

    public TrayIcon(TrayActions actions)
    {
        _actions = actions;

        _aiToggle.CheckedChanged += (_, _) =>
        {
            if (_aiToggle.Checked != _actions.AiEnabled()) _actions.SetAiEnabled(_aiToggle.Checked);
        };
        _translateToggle.CheckedChanged += (_, _) =>
        {
            if (_translateToggle.Checked != _actions.TranslationEnabled()) _actions.SetTranslationEnabled(_translateToggle.Checked);
        };
        _english.Click += (_, _) => _actions.SetLanguage(L.English);
        _portuguese.Click += (_, _) => _actions.SetLanguage(L.Portuguese);
        _language.DropDownItems.AddRange([_english, _portuguese]);
        _downloadModels.Click += (_, _) => _actions.DownloadModels();
        _openSettings.Click += (_, _) => Open(SettingsStore.SettingsPath);
        _reloadSettings.Click += (_, _) => _actions.ReloadSettings();
        _openHistory.Click += (_, _) => Open(Path.Combine(SettingsStore.AppDataDir, "history.jsonl"));
        _modelsFolder.Click += (_, _) =>
        {
            Directory.CreateDirectory(SettingsStore.ModelsDir);
            Open(SettingsStore.ModelsDir);
        };
        _exit.Click += (_, _) => _actions.Exit();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.AddRange(
        [
            _status,
            new Forms.ToolStripSeparator(),
            _aiToggle,
            _translateToggle,
            _language,
            new Forms.ToolStripSeparator(),
            _openSettings,
            _reloadSettings,
            _openHistory,
            _modelsFolder,
            _downloadModels,
            new Forms.ToolStripSeparator(),
            _exit
        ]);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "speechdesk.ico");
        _icon = new Forms.NotifyIcon
        {
            Icon = File.Exists(iconPath) ? new System.Drawing.Icon(iconPath) : System.Drawing.SystemIcons.Application,
            Text = "SpeechDesk",
            ContextMenuStrip = menu,
            Visible = true
        };

        ApplyLanguage();
    }

    /// <summary>Refreshes every menu text and check mark (after a language change or settings reload).</summary>
    public void ApplyLanguage()
    {
        _aiToggle.Text = L.T("tray.aiToggle");
        _translateToggle.Text = L.T("tray.translateToggle");
        _language.Text = L.T("tray.language");
        _downloadModels.Text = L.T("tray.downloadModel");
        _openSettings.Text = L.T("tray.openSettings");
        _reloadSettings.Text = L.T("tray.reloadSettings");
        _openHistory.Text = L.T("tray.openHistory");
        _modelsFolder.Text = L.T("tray.modelsFolder");
        _exit.Text = L.T("tray.exit");
        _english.Checked = !L.IsPortuguese;
        _portuguese.Checked = L.IsPortuguese;
        _aiToggle.Checked = _actions.AiEnabled();
        _translateToggle.Checked = _actions.TranslationEnabled();
        SetState(_state);
    }

    public void SetState(DictationState state)
    {
        _state = state;
        var text = L.T(state switch
        {
            DictationState.Recording => "tray.recording",
            DictationState.Transcribing => "tray.transcribing",
            DictationState.Formatting => _actions.TranslationEnabled() ? "tray.translating"
                : _actions.AiEnabled() ? "tray.cleaningAi" : "tray.formatting",
            DictationState.Injecting => "tray.typing",
            DictationState.Error => "tray.error",
            _ => "tray.ready"
        });
        _status.Text = text;
        _icon.Text = $"SpeechDesk — {text}";
    }

    public void ShowInfo(string message) => _icon.ShowBalloonTip(3000, "SpeechDesk", message, Forms.ToolTipIcon.Info);

    public void ShowError(string message) => _icon.ShowBalloonTip(5000, "SpeechDesk", message, Forms.ToolTipIcon.Error);

    private static void Open(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
