using System.Diagnostics;
using System.IO;
using FlowSpeech.Core.Models;
using FlowSpeech.Core.Settings;
using Forms = System.Windows.Forms;

namespace FlowSpeech.App;

/// <summary>Ícone na área de notificação com o menu da app.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _status;
    private readonly Forms.ToolStripMenuItem _aiToggle;
    private readonly Func<bool> _aiEnabled;
    private readonly Forms.ToolStripMenuItem _translateToggle;
    private readonly Func<bool> _translationEnabled;

    public TrayIcon(
        Action reloadSettings, Action exit,
        Func<bool> aiEnabled, Action<bool> setAiEnabled,
        Func<bool> translationEnabled, Action<bool> setTranslationEnabled)
    {
        _aiEnabled = aiEnabled;
        _translationEnabled = translationEnabled;
        _translateToggle = new Forms.ToolStripMenuItem("Traduzir para inglês") { CheckOnClick = true, Checked = translationEnabled() };
        _translateToggle.CheckedChanged += (_, _) =>
        {
            if (_translateToggle.Checked != _translationEnabled()) setTranslationEnabled(_translateToggle.Checked);
        };
        _status = new Forms.ToolStripMenuItem("Pronto") { Enabled = false };
        _aiToggle = new Forms.ToolStripMenuItem("Limpar texto com IA") { CheckOnClick = true, Checked = aiEnabled() };
        _aiToggle.CheckedChanged += (_, _) =>
        {
            if (_aiToggle.Checked != _aiEnabled()) setAiEnabled(_aiToggle.Checked);
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_status);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_aiToggle);
        menu.Items.Add(_translateToggle);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Abrir definições", null, (_, _) => Open(SettingsStore.SettingsPath));
        menu.Items.Add("Recarregar definições", null, (_, _) => reloadSettings());
        menu.Items.Add("Abrir histórico", null, (_, _) => Open(Path.Combine(SettingsStore.AppDataDir, "history.jsonl")));
        menu.Items.Add("Pasta de modelos", null, (_, _) =>
        {
            var dir = Path.Combine(SettingsStore.AppDataDir, "models");
            Directory.CreateDirectory(dir);
            Open(dir);
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => exit());

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "flowspeech.ico");
        _icon = new Forms.NotifyIcon
        {
            Icon = File.Exists(iconPath) ? new System.Drawing.Icon(iconPath) : System.Drawing.SystemIcons.Application,
            Text = "FlowSpeech",
            ContextMenuStrip = menu,
            Visible = true
        };
    }

    /// <summary>Atualiza o visto do menu depois de recarregar as definições.</summary>
    public void SyncToggles()
    {
        _aiToggle.Checked = _aiEnabled();
        _translateToggle.Checked = _translationEnabled();
    }

    public void SetState(DictationState state)
    {
        var text = state switch
        {
            DictationState.Recording => "A gravar",
            DictationState.Transcribing => "A transcrever",
            DictationState.Formatting => _translationEnabled() ? "A traduzir" : _aiEnabled() ? "A limpar com IA" : "A formatar",
            DictationState.Injecting => "A escrever",
            DictationState.Error => "Erro",
            _ => "Pronto"
        };
        _status.Text = text;
        _icon.Text = $"FlowSpeech — {text}";
    }

    public void ShowInfo(string message) => _icon.ShowBalloonTip(3000, "FlowSpeech", message, Forms.ToolTipIcon.Info);

    public void ShowError(string message) => _icon.ShowBalloonTip(5000, "FlowSpeech", message, Forms.ToolTipIcon.Error);

    private static void Open(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) File.WriteAllText(path, string.Empty);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
