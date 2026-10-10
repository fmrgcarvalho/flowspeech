using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using SpeechDesk.Core.Models;

namespace SpeechDesk.App;

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

    /// <summary>Mostra "→ EN" no overlay quando a tradução está ligada.</summary>
    public bool TranslationOn { get; set; }

    public void SetState(DictationState state, bool handsFree)
    {
        var suffix = TranslationOn ? "  → EN" : string.Empty;

        switch (state)
        {
            case DictationState.Recording:
                // Largura fixada no início da gravação, antes de centrar; mantém-se até esconder.
                Width = 200 + (handsFree ? 70 : 0) + (TranslationOn ? 50 : 0);
                Label.Text = (handsFree ? "Mãos-livres · toca para parar" : "A ouvir…") + suffix;
                ShowAtBottom();
                break;
            case DictationState.Transcribing:
                Label.Text = "A transcrever…";
                SetLevel(0.1f);
                break;
            case DictationState.Formatting:
                Label.Text = TranslationOn ? "A traduzir…" : "A limpar texto…";
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
