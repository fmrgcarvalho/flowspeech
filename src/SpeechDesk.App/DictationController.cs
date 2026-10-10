using SpeechDesk.Core.Abstractions;
using SpeechDesk.Core.Models;
using SpeechDesk.Core.Pipeline;

namespace SpeechDesk.App;

/// <summary>
/// Traduz o atalho em ações sobre o pipeline:
///  • manter premido → grava enquanto estiver premido (push-to-talk)
///  • dois toques     → mãos-livres; o toque seguinte termina
///  • Esc / combinação com outra tecla → cancela
/// </summary>
public sealed class DictationController : IDisposable
{
    private readonly DictationPipeline _pipeline;
    private readonly IHotkeyListener _hotkey;
    private readonly OverlayWindow _overlay;
    private readonly TrayIcon _tray;
    private bool _handsFree;
    private bool _ignoreNextRelease;

    public DictationController(DictationPipeline pipeline, IHotkeyListener hotkey, OverlayWindow overlay, TrayIcon tray)
    {
        _pipeline = pipeline;
        _hotkey = hotkey;
        _overlay = overlay;
        _tray = tray;

        _hotkey.Pressed += OnPressed;
        _hotkey.Released += OnReleased;
        _hotkey.DoubleTapped += OnDoubleTapped;
        _hotkey.Cancelled += OnCancelled;

        _pipeline.StateChanged += (_, state) => _overlay.Dispatcher.BeginInvoke(() =>
        {
            _overlay.SetState(state, _handsFree);
            _tray.SetState(state);
        });
        _pipeline.Failed += (_, ex) => _overlay.Dispatcher.BeginInvoke(() => _tray.ShowError(ex.Message));
    }

    private async void OnPressed(object? sender, EventArgs e)
    {
        if (_handsFree)
        {
            _handsFree = false;
            _ignoreNextRelease = true;
            await _pipeline.EndRecordingAsync();
            return;
        }
        _pipeline.BeginRecording();
    }

    private async void OnReleased(object? sender, EventArgs e)
    {
        if (_ignoreNextRelease)
        {
            _ignoreNextRelease = false;
            return;
        }
        if (_handsFree) return;

        // Toques curtos geram clips abaixo de MinRecordingMs e são descartados pelo pipeline,
        // o que permite que o 1.º toque de um duplo toque não escreva nada.
        await _pipeline.EndRecordingAsync();
    }

    private void OnDoubleTapped(object? sender, EventArgs e)
    {
        _handsFree = true;
        _pipeline.BeginRecording();
        _overlay.SetState(DictationState.Recording, handsFree: true);
    }

    private void OnCancelled(object? sender, EventArgs e)
    {
        if (!_pipeline.IsRecording) return;
        _handsFree = false;
        _pipeline.Cancel();
    }

    public void Dispose()
    {
        _hotkey.Pressed -= OnPressed;
        _hotkey.Released -= OnReleased;
        _hotkey.DoubleTapped -= OnDoubleTapped;
        _hotkey.Cancelled -= OnCancelled;
    }
}
