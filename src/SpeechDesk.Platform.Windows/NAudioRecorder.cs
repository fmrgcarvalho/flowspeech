using SpeechDesk.Core.Abstractions;
using SpeechDesk.Core.Audio;
using SpeechDesk.Core.Models;
using NAudio.Wave;

namespace SpeechDesk.Platform.Windows;

/// <summary>Captura do microfone por omissão do Windows em 16 kHz / 16-bit / mono (o formato nativo do Whisper).</summary>
public sealed class NAudioRecorder : IAudioRecorder
{
    private const int SampleRate = 16000;
    private readonly object _sync = new();
    /// <summary>WAVE_MAPPER: o microfone por omissão do Windows.</summary>
    private const int DefaultDevice = -1;
    private readonly Func<string> _microphone;
    private WaveInEvent? _waveIn;
    private MemoryStream _buffer = new();
    private ManualResetEventSlim? _stopped;

    /// <param name="microphone">Nome (ou parte) do microfone a usar; vazio = por omissão do Windows.</param>
    public NAudioRecorder(Func<string> microphone) => _microphone = microphone;

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
        lock (_sync)
        {
            if (IsRecording) return;
            _buffer = new MemoryStream();
            _stopped = new ManualResetEventSlim(false);

            // O WaveInEvent captura o SynchronizationContext no construtor. Sem contexto, os eventos
            // chegam na thread de gravação — evita deadlock quando o Stop() espera na thread de UI.
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = FindDevice(_microphone()),
                    WaveFormat = new WaveFormat(SampleRate, 16, 1),
                    BufferMilliseconds = 50
                };
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }

            _waveIn.DataAvailable += OnData;
            _waveIn.RecordingStopped += (_, _) => _stopped?.Set();
            _waveIn.StartRecording();
            IsRecording = true;
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        lock (_sync) _buffer.Write(e.Buffer, 0, e.BytesRecorded);
        LevelChanged?.Invoke(this, WavEncoder.Rms(e.Buffer.AsSpan(0, e.BytesRecorded)));
    }

    public AudioClip Stop()
    {
        WaveInEvent? waveIn;
        lock (_sync)
        {
            if (!IsRecording) return new AudioClip([]);
            IsRecording = false;
            waveIn = _waveIn;
            _waveIn = null;
        }

        waveIn?.StopRecording();
        _stopped?.Wait(TimeSpan.FromMilliseconds(500)); // deixa chegar o último buffer
        waveIn?.Dispose();

        lock (_sync) return new AudioClip(_buffer.ToArray(), SampleRate);
    }

    public void Dispose()
    {
        if (IsRecording) Stop();
        _stopped?.Dispose();
    }
}
