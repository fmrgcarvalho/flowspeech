namespace SpeechDesk.Core.Settings;

/// <summary>A downloadable model: where it comes from and how to tell that it is installed.</summary>
public sealed record ModelSpec(
    string Id,
    string DisplayName,
    string Url,
    /// <summary>True for a .tar.bz2 that unpacks into a folder; false for a single file.</summary>
    bool IsArchive,
    /// <summary>Settings key for the "do you want to download it?" question.</summary>
    string AskKey)
{
    /// <summary>Path that must exist for the model to count as installed.</summary>
    public string InstalledMarker(AppSettings s) => Id switch
    {
        ModelCatalog.ParakeetId => Path.Combine(SettingsStore.ResolveModelPath(s.Transcription.ParakeetModelDir), "encoder.int8.onnx"),
        ModelCatalog.WhisperId => SettingsStore.ResolveModelPath(s.Transcription.WhisperModelPath),
        ModelCatalog.AiId => SettingsStore.ResolveModelPath(s.Formatting.Ai.ModelPath),
        _ => throw new ArgumentOutOfRangeException(nameof(Id), Id, null)
    };

    public bool IsInstalled(AppSettings s) => File.Exists(InstalledMarker(s));
}

public static class ModelCatalog
{
    public const string ParakeetId = "parakeet";
    public const string WhisperId = "whisper";
    public const string AiId = "ai";

    public static ModelSpec Parakeet { get; } = new(
        ParakeetId,
        "NVIDIA Parakeet TDT 0.6B v3",
        "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2",
        IsArchive: true,
        "model.ask.parakeet");

    public static ModelSpec Whisper { get; } = new(
        WhisperId,
        "OpenAI Whisper large-v3-turbo",
        "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin",
        IsArchive: false,
        "model.ask.whisper");

    public static ModelSpec Ai { get; } = new(
        AiId,
        "Qwen 2.5 1.5B Instruct",
        "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf",
        IsArchive: false,
        "model.ask.ai");

    /// <summary>The speech model the current settings need, or null for the cloud engine.</summary>
    public static ModelSpec? SpeechModelFor(AppSettings s) => s.Transcription.Engine.ToLowerInvariant() switch
    {
        TranscriptionEngines.Cloud => null,
        TranscriptionEngines.Whisper => Whisper,
        _ => Parakeet
    };
}
