using System.Diagnostics;
using System.Text;
using FlowSpeech.Core.Abstractions;
using FlowSpeech.Core.Models;
using static FlowSpeech.Platform.Windows.Interop.NativeMethods;

namespace FlowSpeech.Platform.Windows;

/// <summary>Lê o processo e o título da janela em primeiro plano (para estilos por app).</summary>
public sealed class ForegroundAppProvider : IActiveAppProvider
{
    public DictationContext GetCurrentContext()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return DictationContext.Unknown;

        var title = new StringBuilder(512);
        GetWindowText(hwnd, title, title.Capacity);
        GetWindowThreadProcessId(hwnd, out var pid);

        try
        {
            using var process = Process.GetProcessById((int)pid);
            return new DictationContext(process.ProcessName, title.ToString());
        }
        catch (ArgumentException)
        {
            return DictationContext.Unknown with { WindowTitle = title.ToString() };
        }
    }
}
