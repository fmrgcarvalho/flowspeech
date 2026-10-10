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
    private readonly int _deviceNumber;
    private WaveInEvent? _waveIn;
    private MemoryStream _buffer = new();
    private ManualResetEventSlim? _stopped;

    public NAudioRecorder(int deviceNumber = 0) => _deviceNumber = deviceNumber;

    public event EventHandler<float>? LevelChanged;

    public bool IsRecording { get; private set; }

    public static IReadOnlyList<string> ListDevices() =>
        Enumerable.Range(0, WaveInEvent.DeviceCount)
            .Select(i => WaveInEvent.GetCapabilities(i).ProductName)
            .ToList();

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
                    DeviceNumber = _deviceNumber,
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
