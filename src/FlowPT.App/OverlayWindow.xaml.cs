using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowPT.Core.Models;

namespace FlowPT.App;

/// <summary>
/// "Pílula" flutuante no fundo do ecrã que mostra o estado. Nunca recebe foco nem cliques,
/// para não tirar o cursor da app onde o texto vai ser escrito.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
    private static readonly double[] BarWeights = [0.5, 0.8, 1.0, 0.8, 0.5];
    private readonly Random _jitter = new();

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        };
    }

    public void SetState(DictationState state, bool handsFree)
    {
        switch (state)
        {
            case DictationState.Recording:
                Label.Text = handsFree ? "Mãos-livres · toca para parar" : "A ouvir…";
                ShowAtBottom();
                break;
            case DictationState.Transcribing:
            case DictationState.Formatting:
                Label.Text = "A transcrever…";
                SetLevel(0.1f);
                break;
            case DictationState.Injecting:
                Label.Text = "A escrever…";
                break;
            case DictationState.Error:
                Label.Text = "Erro — ver notificação";
                _ = HideAfterAsync(2000);
                break;
            default:
                Hide();
                break;
        }
        Width = handsFree && state == DictationState.Recording ? 270 : 200;
    }

    public void SetLevel(float level)
    {
        for (var i = 0; i < Bars.Children.Count; i++)
        {
            if (Bars.Children[i] is not Rectangle bar) continue;
            var jitter = 0.85 + _jitter.NextDouble() * 0.3;
            bar.Height = Math.Clamp(4 + level * 20 * BarWeights[i] * jitter, 4, 24);
        }
    }

    private void ShowAtBottom()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Bottom - Height - 24;
        if (!IsVisible) Show();
    }

    private async Task HideAfterAsync(int ms)
    {
        await Task.Delay(ms);
        Hide();
    }

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
