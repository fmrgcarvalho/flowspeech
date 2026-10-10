using SpeechDesk.Core.Models;

namespace SpeechDesk.Core.Abstractions;

/// <summary>Captura áudio do microfone em PCM 16 kHz, 16-bit, mono.</summary>
public interface IAudioRecorder : IDisposable
{
    /// <summary>Nível de entrada normalizado (0..1), para animar o overlay.</summary>
    event EventHandler<float>? LevelChanged;

    bool IsRecording { get; }
    void Start();
    /// <summary>Pára a gravação e devolve o clip capturado.</summary>
    AudioClip Stop();
}

/// <summary>Speech-to-text. Implementações: Whisper local (ativa) e cloud (opcional).</summary>
public interface ISpeechToText
{
    string Name { get; }
    /// <summary>True se o motor consegue devolver diretamente em inglês (ex.: Whisper "translate").</summary>
    bool CanTranslateToEnglish => false;
    Task<Transcript> TranscribeAsync(AudioClip clip, TranscriptionOptions options, CancellationToken ct = default);
}

/// <summary>Limpeza/formatação do texto transcrito (regras locais).</summary>
public interface ITextFormatter
{
    string Name { get; }
    Task<string> FormatAsync(string text, DictationContext context, CancellationToken ct = default);
}

/// <summary>Escreve o texto final na aplicação que tem o foco.</summary>
public interface ITextInjector
{
    Task InjectAsync(string text, CancellationToken ct = default);
}

/// <summary>Atalho global de "push-to-talk" / mãos-livres.</summary>
public interface IHotkeyListener : IDisposable
{
    event EventHandler? Pressed;
    event EventHandler? Released;
    /// <summary>Dois toques rápidos: alterna o modo mãos-livres.</summary>
    event EventHandler? DoubleTapped;
    /// <summary>Esc, ou outra tecla premida em conjunto (ex.: Ctrl+C) — descarta a gravação.</summary>
    event EventHandler? Cancelled;
    void Start();
}

/// <summary>Identifica a aplicação ativa (para estilos por app).</summary>
public interface IActiveAppProvider
{
    DictationContext GetCurrentContext();
}

/// <summary>Histórico local das ditações.</summary>
public interface IHistoryStore
{
    Task AppendAsync(HistoryEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int count, CancellationToken ct = default);
}
