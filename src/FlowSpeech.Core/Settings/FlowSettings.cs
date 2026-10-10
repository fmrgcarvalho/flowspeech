using System.Text.Json;
using System.Text.Json.Serialization;
using FlowSpeech.Core.Models;

namespace FlowSpeech.Core.Settings;

public sealed class FlowSettings
{
    public HotkeySettings Hotkey { get; set; } = new();
    public TranscriptionSettings Transcription { get; set; } = new();
    public FormattingSettings Formatting { get; set; } = new();

    /// <summary>Substituições pessoais: "flow speech" → "FlowSpeech", "gaiao" → "Gaião".</summary>
    public Dictionary<string, string> Dictionary { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["flow speech"] = "FlowSpeech",
        ["gaiao"] = "Gaião"
    };

    /// <summary>Snippets: dizer o gatilho expande para o texto.</summary>
    public Dictionary<string, string> Snippets { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["o meu email"] = "mgaiao25@gmail.com"
    };

    /// <summary>Estilo de escrita por processo (sem .exe).</summary>
    public Dictionary<string, WritingStyle> AppStyles { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OUTLOOK"] = WritingStyle.Formal,
        ["WINWORD"] = WritingStyle.Formal,
        ["Teams"] = WritingStyle.Casual,
        ["ms-teams"] = WritingStyle.Casual,
        ["slack"] = WritingStyle.Casual,
        ["WhatsApp"] = WritingStyle.Casual,
        ["Code"] = WritingStyle.Code,
        ["devenv"] = WritingStyle.Code,
        ["rider64"] = WritingStyle.Code,
        ["WindowsTerminal"] = WritingStyle.Code
    };

    public InjectionMode Injection { get; set; } = InjectionMode.Paste;
    public bool SaveHistory { get; set; } = true;

    public WritingStyle StyleFor(string processName) =>
        AppStyles.TryGetValue(processName, out var style) ? style : WritingStyle.Raw;
}

public sealed class HotkeySettings
{
    /// <summary>Virtual-key da tecla de push-to-talk. 0xA3 = Right Ctrl.</summary>
    public int VirtualKey { get; set; } = 0xA3;
    /// <summary>Janela (ms) para considerar dois toques como duplo toque.</summary>
    public int DoubleTapMs { get; set; } = 350;
    /// <summary>Gravações mais curtas que isto são descartadas (toques acidentais).</summary>
    public int MinRecordingMs { get; set; } = 300;
}

public sealed class TranscriptionSettings
{
    /// <summary>"parakeet" (por omissão), "whisper" ou "cloud" (API compatível OpenAI).</summary>
    public string Engine { get; set; } = TranscriptionEngines.Parakeet;
    /// <summary>Pasta do modelo Parakeet (sherpa-onnx). Relativa a %AppData%\FlowSpeech\models.</summary>
    public string ParakeetModelDir { get; set; } = "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8";
    /// <summary>Só para o motor Whisper: ficheiro GGML. Relativo a %AppData%\FlowSpeech\models.</summary>
    public string WhisperModelPath { get; set; } = "ggml-large-v3-turbo-q5_0.bin";
    /// <summary>Só Whisper/cloud: "auto", "pt", "en". O Parakeet deteta a língua sozinho.</summary>
    public string Language { get; set; } = "auto";
    public int Threads { get; set; } = 0; // 0 = automático
    public CloudSettings Cloud { get; set; } = new();
}

public static class TranscriptionEngines
{
    public const string Parakeet = "parakeet";
    public const string Whisper = "whisper";
    public const string Cloud = "cloud";
}

public sealed class CloudSettings
{
    public string Endpoint { get; set; } = "https://api.openai.com/v1/audio/transcriptions";
    public string Model { get; set; } = "gpt-4o-transcribe";
    /// <summary>A chave é lida desta variável de ambiente — nunca guardada no ficheiro.</summary>
    public string ApiKeyEnvVar { get; set; } = "FLOWSPEECH_STT_API_KEY";
}

public sealed class FormattingSettings
{
    /// <summary>Regras locais: remover hesitações, pontuação falada, maiúsculas, dicionário e snippets.</summary>
    public bool RuleBasedCleanup { get; set; } = true;
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string AppDataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlowSpeech");

    public static string SettingsPath => Path.Combine(AppDataDir, "settings.json");

    public static FlowSettings Load(string? path = null)
    {
        path ??= SettingsPath;
        if (!File.Exists(path))
        {
            var defaults = new FlowSettings();
            Save(defaults, path);
            return defaults;
        }

        var loaded = JsonSerializer.Deserialize<FlowSettings>(File.ReadAllText(path), Json) ?? new FlowSettings();
        // Os dicionários desserializados perdem o comparador case-insensitive.
        loaded.Dictionary = new(loaded.Dictionary, StringComparer.OrdinalIgnoreCase);
        loaded.Snippets = new(loaded.Snippets, StringComparer.OrdinalIgnoreCase);
        loaded.AppStyles = new(loaded.AppStyles, StringComparer.OrdinalIgnoreCase);
        return loaded;
    }

    public static void Save(FlowSettings settings, string? path = null)
    {
        path ??= SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, Json));
    }

    public static string ModelsDir => Path.Combine(AppDataDir, "models");

    public static string ResolveModelPath(string modelPath) =>
        Path.IsPathRooted(modelPath) ? modelPath : Path.Combine(ModelsDir, modelPath);
}
