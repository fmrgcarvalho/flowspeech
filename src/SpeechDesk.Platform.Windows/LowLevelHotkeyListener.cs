using System.Diagnostics;
using System.Runtime.InteropServices;
using SpeechDesk.Core.Abstractions;
using SpeechDesk.Core.Localization;
using SpeechDesk.Platform.Windows.Interop;
using static SpeechDesk.Platform.Windows.Interop.NativeMethods;

namespace SpeechDesk.Platform.Windows;

/// <summary>
/// Hook global de teclado (WH_KEYBOARD_LL). Funciona em qualquer app, sem roubar o foco.
/// Deve ser criado na thread de UI (precisa de message loop). Os eventos são publicados
/// de forma assíncrona para que o hook devolva rapidamente (o Windows remove hooks lentos).
/// </summary>
public sealed class LowLevelHotkeyListener : IHotkeyListener
{
    private readonly Func<int> _virtualKey;
    private readonly Func<int> _doubleTapMs;
    private readonly LowLevelKeyboardProc _proc; // referência mantida para o GC não recolher o delegate
    private readonly SynchronizationContext _ui;
    private IntPtr _hook;

    private bool _isDown;
    private bool _chorded;
    private long _downAt;
    private long _lastTapUpAt = long.MinValue;
    private bool _lastWasShortTap;

    public LowLevelHotkeyListener(Func<int> virtualKey, Func<int> doubleTapMs)
    {
        _virtualKey = virtualKey;
        _doubleTapMs = doubleTapMs;
        _proc = HookCallback;
        _ui = SynchronizationContext.Current
              ?? throw new InvalidOperationException("LowLevelHotkeyListener must be created on the UI thread.");
    }

    public event EventHandler? Pressed;
    public event EventHandler? Released;
    public event EventHandler? DoubleTapped;
    public event EventHandler? Cancelled;

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), L.T("error.hook"));
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            // Ignorar teclas que nós próprios injetamos (o Ctrl+V do paste).
            if ((data.flags & LLKHF_INJECTED) == 0)
                Handle((int)data.vkCode, (int)wParam is WM_KEYDOWN or WM_SYSKEYDOWN);
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void Handle(int vk, bool down)
    {
        var now = Stopwatch.GetTimestamp();

        if (vk == _virtualKey())
        {
            if (down)
            {
                if (_isDown) return; // auto-repeat
                _isDown = true;
                _chorded = false;
                _downAt = now;

                var sinceLastTap = Stopwatch.GetElapsedTime(_lastTapUpAt == long.MinValue ? 0 : _lastTapUpAt, now);
                if (_lastWasShortTap && sinceLastTap.TotalMilliseconds <= _doubleTapMs())
                {
                    _lastWasShortTap = false;
                    Raise(DoubleTapped);
                }
                else
                {
                    Raise(Pressed);
                }
            }
            else if (_isDown)
            {
                _isDown = false;
                var held = Stopwatch.GetElapsedTime(_downAt, now);
                _lastWasShortTap = !_chorded && held.TotalMilliseconds <= _doubleTapMs();
                _lastTapUpAt = now;
                if (!_chorded) Raise(Released);
            }
            return;
        }

        if (!down) return;

        if (vk == VK_ESCAPE)
        {
            Raise(Cancelled);
        }
        else if (_isDown && !_chorded)
        {
            // Ex.: Right Ctrl + C — o utilizador queria um atalho, não ditar.
            _chorded = true;
            Raise(Cancelled);
        }
    }

    private void Raise(EventHandler? handler)
    {
        if (handler is null) return;
        _ui.Post(_ => handler(this, EventArgs.Empty), null);
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
