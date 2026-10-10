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

    public TrayIcon(Action reloadSettings, Action exit)
    {
        _status = new Forms.ToolStripMenuItem("Pronto") { Enabled = false };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_status);
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

    public void SetState(DictationState state)
    {
        var text = state switch
        {
            DictationState.Recording => "A gravar",
            DictationState.Transcribing => "A transcrever",
            DictationState.Formatting => "A formatar",
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
