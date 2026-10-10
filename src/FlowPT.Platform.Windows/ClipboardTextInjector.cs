using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using FlowPT.Core.Abstractions;
using FlowPT.Core.Models;
using static FlowPT.Platform.Windows.Interop.NativeMethods;

namespace FlowPT.Platform.Windows;

/// <summary>Insere o texto onde está o cursor, em qualquer aplicação.</summary>
public sealed class ClipboardTextInjector(Dispatcher dispatcher, Func<InjectionMode> mode) : ITextInjector
{
    public Task InjectAsync(string text, CancellationToken ct = default) =>
        mode() == InjectionMode.Type ? TypeAsync(text) : PasteAsync(text, ct);

    private async Task PasteAsync(string text, CancellationToken ct)
    {
        // O clipboard WPF exige thread STA → corre tudo no dispatcher de UI.
        var backup = await dispatcher.InvokeAsync(SnapshotClipboard);
        await dispatcher.InvokeAsync(() => SetClipboardText(text));

        SendKeys(Key(VK_CONTROL, false), Key(VK_V, false), Key(VK_V, true), Key(VK_CONTROL, true));

        // A app de destino lê o clipboard de forma assíncrona; dar-lhe tempo antes de repor.
        await Task.Delay(250, ct);
        await dispatcher.InvokeAsync(() => RestoreClipboard(backup));
    }

    private static Task TypeAsync(string text)
    {
        var inputs = new List<INPUT>(text.Length * 2);
        foreach (var c in text.Replace("\r\n", "\n"))
        {
            if (c == '\n')
            {
                inputs.Add(Key(0x0D, false));
                inputs.Add(Key(0x0D, true));
                continue;
            }
            inputs.Add(Unicode(c, false));
            inputs.Add(Unicode(c, true));
        }
        SendKeys([.. inputs]);
        return Task.CompletedTask;
    }

    private static void SendKeys(params INPUT[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                "SendInput falhou (a app de destino pode estar a correr como Administrador).");
    }

    private static void SetClipboardText(string text)
    {
        // O clipboard pode estar momentaneamente bloqueado por outra app.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, copy: true);
                return;
            }
            catch (COMException) when (attempt < 5)
            {
                Thread.Sleep(30);
            }
        }
    }

    private static DataObject? SnapshotClipboard()
    {
        try
        {
            var current = Clipboard.GetDataObject();
            if (current is null) return null;
            var copy = new DataObject();
            foreach (var format in current.GetFormats(autoConvert: false))
            {
                try
                {
                    var data = current.GetData(format, autoConvert: false);
                    if (data is not null) copy.SetData(format, data);
                }
                catch
                {
                    // Alguns formatos (ex.: ficheiros virtuais) não são copiáveis — ignorar.
                }
            }
            return copy;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static void RestoreClipboard(DataObject? backup)
    {
        try
        {
            if (backup is null) Clipboard.Clear();
            else Clipboard.SetDataObject(backup, copy: true);
        }
        catch (COMException)
        {
            // Melhor perder o backup do que falhar a ditação.
        }
    }
}
