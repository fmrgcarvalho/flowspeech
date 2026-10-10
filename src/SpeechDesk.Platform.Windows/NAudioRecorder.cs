using SpeechDesk.Core.Abstractions;
using SpeechDesk.Core.Audio;
using SpeechDesk.Core.Models;
using NAudio.Wave;

namespace SpeechDesk.Platform.Windows;

/// <summary>
/// Captura do microfone em 16 kHz / 16-bit / mono.
/// Depois de cada ditado o microfone fica aberto durante <c>warmSeconds</c>: o ditado seguinte começa sem
/// atraso (os auscultadores Bluetooth demoram ~1 s a arrancar) e inclui os ~250 ms anteriores à tecla,
/// para não perder a primeira sílaba de quem começa a falar ao mesmo tempo que carrega.
/// </summary>
public sealed class NAudioRecorder : IAudioRecorder
{
    private const int SampleRate = 16000;
    private const int BytesPerMs = SampleRate * 2 / 1000;
    private const int PreRollMs = 250;
    /// <summary>WAVE_MAPPER: o microfone por omissão do Windows.</summary>
    private const int DefaultDevice = -1;

    private readonly object _sync = new();
    private readonly Func<string> _microphone;
    private readonly Func<int> _warmSeconds;
    private readonly Timer _closeTimer;
    private WaveInEvent? _waveIn;
    /// <summary>Microfone a fechar no fim de um ditado: os últimos buffers ainda contam.</summary>
    private WaveInEvent? _draining;
    private int _openDevice;
    private MemoryStream _buffer = new();
    /// <summary>Últimos milissegundos ouvidos enquanto o microfone está aberto sem gravar.</summary>
    private readonly Queue<byte[]> _preRoll = new();
    private int _preRollBytes;
    private ManualResetEventSlim? _stopped;

    /// <param name="microphone">Nome (ou parte) do microfone a usar; vazio = por omissão do Windows.</param>
    /// <param name="warmSeconds">Segundos que o microfone fica aberto depois de um ditado (0 = fecha logo).</param>
    public NAudioRecorder(Func<string> microphone, Func<int> warmSeconds)
    {
        _microphone = microphone;
        _warmSeconds = warmSeconds;
        _closeTimer = new Timer(_ => CloseIfIdle());
    }

    public event EventHandler<float>? LevelChanged;

    public bool IsRecording { get; private set; }

    public static IReadOnlyList<string> ListDevices() =>
        Enumerable.Range(0, WaveInEvent.DeviceCount)
            .Select(i => WaveInEvent.GetCapabilities(i).ProductName)
            .ToList();

    /// <summary>Resolvido a cada gravação: o microfone pode ter sido ligado ou desligado entretanto.</summary>
    private static int FindDevice(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return DefaultDevice;
        var devices = ListDevices();
        for (var i = 0; i < devices.Count; i++)
        {
            // O Windows corta o nome a 31 caracteres: aceita correspondência parcial nos dois sentidos.
            if (devices[i].Contains(name, StringComparison.OrdinalIgnoreCase) ||
                name.Contains(devices[i], StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return DefaultDevice;
    }

    public void Start()
    {
        WaveInEvent? stale = null;
        lock (_sync)
        {
            if (IsRecording) return;
            _closeTimer.Change(Timeout.Infinite, Timeout.Infinite);

            var device = FindDevice(_microphone());
            if (_waveIn is not null && device != _openDevice)
            {
                stale = _waveIn; // mudou de microfone no menu
                _waveIn = null;
            }

            _buffer = new MemoryStream();
            if (_waveIn is null)
            {
                Open(device);
            }
            else
            {
                // Microfone já aberto: o som de antes da tecla é real e entra no início da gravação.
                foreach (var chunk in _preRoll) _buffer.Write(chunk);
            }
            _preRoll.Clear();
            _preRollBytes = 0;
            IsRecording = true;
        }
        if (stale is not null) Close(stale);
    }

    private void Open(int device)
    {
        _stopped = new ManualResetEventSlim(false);

        // O WaveInEvent captura o SynchronizationContext no construtor. Sem contexto, os eventos
        // chegam na thread de gravação — evita deadlock quando o Stop() espera na thread de UI.
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = device,
                WaveFormat = new WaveFormat(SampleRate, 16, 1),
                BufferMilliseconds = 50
            };
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        var stopped = _stopped;
        _waveIn.DataAvailable += OnData;
        _waveIn.RecordingStopped += (_, _) => stopped.Set();
        _waveIn.StartRecording();
        _openDevice = device;
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        lock (_sync)
        {
            if (sender == _draining)
            {
                _buffer.Write(e.Buffer, 0, e.BytesRecorded);
                return;
            }
            if (sender != _waveIn) return; // buffer atrasado de um microfone já fechado
            if (IsRecording)
            {
                _buffer.Write(e.Buffer, 0, e.BytesRecorded);
            }
            else
            {
                _preRoll.Enqueue(e.Buffer.AsSpan(0, e.BytesRecorded).ToArray());
                _preRollBytes += e.BytesRecorded;
                while (_preRollBytes - _preRoll.Peek().Length >= PreRollMs * BytesPerMs)
                    _preRollBytes -= _preRoll.Dequeue().Length;
            }
        }
        if (IsRecording) LevelChanged?.Invoke(this, WavEncoder.Rms(e.Buffer.AsSpan(0, e.BytesRecorded)));
    }

    public AudioClip Stop()
    {
        WaveInEvent? toClose = null;
        AudioClip clip;
        lock (_sync)
        {
            if (!IsRecording) return new AudioClip([]);
            IsRecording = false;

            var warm = Math.Max(0, _warmSeconds());
            if (warm > 0)
            {
                // O microfone continua aberto: o áudio até aqui já chegou (a cauda do pipeline cobre o último buffer).
                _closeTimer.Change(TimeSpan.FromSeconds(warm), Timeout.InfiniteTimeSpan);
                return new AudioClip(_buffer.ToArray(), SampleRate);
            }

            toClose = _waveIn;
            _draining = toClose;
            _waveIn = null;
        }

        var stopped = _stopped;
        toClose?.StopRecording();
        stopped?.Wait(TimeSpan.FromMilliseconds(500)); // deixa chegar o último buffer
        lock (_sync)
        {
            _draining = null;
            clip = new AudioClip(_buffer.ToArray(), SampleRate);
        }
        toClose?.Dispose();
        return clip;
    }

    private void CloseIfIdle()
    {
        WaveInEvent? toClose;
        lock (_sync)
        {
            if (IsRecording || _waveIn is null) return;
            toClose = _waveIn;
            _waveIn = null;
            _preRoll.Clear();
            _preRollBytes = 0;
        }
        Close(toClose);
    }

    private static void Close(WaveInEvent waveIn)
    {
        waveIn.StopRecording();
        waveIn.Dispose();
    }

    public void Dispose()
    {
        _closeTimer.Dispose();
        if (IsRecording) Stop();
        WaveInEvent? toClose;
        lock (_sync)
        {
            toClose = _waveIn;
            _waveIn = null;
        }
        if (toClose is not null) Close(toClose);
    }
}
