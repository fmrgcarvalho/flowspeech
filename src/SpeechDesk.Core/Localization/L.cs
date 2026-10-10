using System.Globalization;

namespace SpeechDesk.Core.Localization;

/// <summary>
/// User-facing strings. English is the default; Portuguese (pt-PT) is available from the tray menu
/// or with "UiLanguage": "pt" in settings.json.
/// </summary>
public static class L
{
    public const string English = "en";
    public const string Portuguese = "pt";

    private static string _language = English;

    public static string Language
    {
        get => _language;
        set => _language = Normalize(value);
    }

    public static bool IsPortuguese => _language == Portuguese;

    public static string Normalize(string? language) =>
        language?.Trim().ToLowerInvariant() is "pt" or "pt-pt" or "pt-br" or "portuguese" or "português" ? Portuguese : English;

    public static string T(string key) =>
        Strings.TryGetValue(key, out var s) ? (IsPortuguese ? s.Pt : s.En) : key;

    public static string T(string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, T(key), args);

    internal static IReadOnlyCollection<string> Keys => Strings.Keys;

    private static readonly Dictionary<string, (string En, string Pt)> Strings = new()
    {
        // App lifecycle
        ["app.alreadyRunning"] = ("SpeechDesk is already running (icon next to the clock).", "O SpeechDesk já está a correr (ícone junto ao relógio)."),
        ["app.ready"] = ("SpeechDesk is ready. Hold Right Ctrl to dictate; double-tap for hands-free.", "SpeechDesk pronto. Mantém o Ctrl direito premido para ditar; dois toques = mãos-livres."),
        ["app.settingsReloaded"] = ("Settings reloaded. (Changing the engine or model requires a restart.)", "Definições recarregadas. (Mudar o motor ou o modelo requer reiniciar.)"),
        ["app.microphoneChanged"] = ("Microphone: {0}", "Microfone: {0}"),
        ["app.languageChanged"] = ("Language set to English.", "Língua alterada para português."),

        // Tray menu
        ["tray.ready"] = ("Ready", "Pronto"),
        ["tray.recording"] = ("Recording", "A gravar"),
        ["tray.transcribing"] = ("Transcribing", "A transcrever"),
        ["tray.formatting"] = ("Formatting", "A formatar"),
        ["tray.cleaningAi"] = ("Cleaning up with AI", "A limpar com IA"),
        ["tray.translating"] = ("Translating", "A traduzir"),
        ["tray.typing"] = ("Typing", "A escrever"),
        ["tray.error"] = ("Error", "Erro"),
        ["tray.aiToggle"] = ("Clean up text with AI", "Limpar texto com IA"),
        ["tray.translateToggle"] = ("Translate to English", "Traduzir para inglês"),
        ["tray.language"] = ("Language", "Língua"),
        ["tray.microphone"] = ("Microphone", "Microfone"),
        ["tray.microphoneDefault"] = ("Windows default", "Por omissão do Windows"),
        ["tray.openSettings"] = ("Open settings", "Abrir definições"),
        ["tray.reloadSettings"] = ("Reload settings", "Recarregar definições"),
        ["tray.openHistory"] = ("Open history", "Abrir histórico"),
        ["tray.modelsFolder"] = ("Models folder", "Pasta de modelos"),
        ["tray.exit"] = ("Exit", "Sair"),

        // Overlay
        ["overlay.listening"] = ("Listening…", "A ouvir…"),
        ["overlay.handsFree"] = ("Hands-free · tap to stop", "Mãos-livres · toca para parar"),
        ["overlay.transcribing"] = ("Transcribing…", "A transcrever…"),
        ["overlay.translating"] = ("Translating…", "A traduzir…"),
        ["overlay.cleaning"] = ("Cleaning up…", "A limpar texto…"),
        ["overlay.typing"] = ("Typing…", "A escrever…"),
        ["overlay.error"] = ("Error — see notification", "Erro — ver notificação"),
        ["overlay.downloading"] = ("Downloading model… {0}%", "A descarregar modelo… {0}%"),
        ["overlay.extracting"] = ("Unpacking model…", "A extrair o modelo…"),

        // AI and translation
        ["ai.on"] = ("AI clean-up on. Loading the model…", "Limpeza com IA ligada. A carregar o modelo…"),
        ["ai.off"] = ("AI clean-up off — rules only.", "Limpeza com IA desligada — só regras."),
        ["translation.off"] = ("Translation off — text comes out in the language you speak.", "Tradução desligada — o texto sai na língua em que falas."),
        ["translation.onAi"] = ("Translation to {0} on. Loading the AI model…", "Tradução para {0} ligada. A carregar o modelo de IA…"),
        ["translation.onWhisper"] = ("Translation to English on (done by Whisper).", "Tradução para inglês ligada (feita pelo Whisper)."),
        ["translation.implausible"] = ("The translation did not look right — the original text was typed.", "A tradução não parecia correta — foi escrito o texto original."),
        ["translation.timeout"] = ("Translation took too long — the original text was typed.", "A tradução demorou demasiado — foi escrito o texto original."),
        ["translation.failed"] = ("Could not translate ({0}). The original text was typed.", "Não foi possível traduzir ({0}). Foi escrito o texto original."),

        // Models
        ["model.missing.parakeet"] = ("Speech model missing: {0}", "Modelo de transcrição em falta: {0}"),
        ["model.nonAsciiPath"] = ("The speech engine cannot open paths with accented characters: {0}. Move the models to a folder without accents.",
            "O motor de transcrição não consegue abrir caminhos com acentos: {0}. Mova os modelos para uma pasta sem acentos."),
        ["model.missing.whisper"] =("Whisper model missing: {0}", "Modelo Whisper em falta: {0}"),
        ["model.missing.ai"] = ("AI model missing: {0}", "Modelo de IA em falta: {0}"),
        ["model.ask.title"] = ("Download model", "Descarregar modelo"),
        ["model.ask.parakeet"] = ("SpeechDesk needs the speech recognition model (NVIDIA Parakeet, about 0.7 GB) to work.\n\nDownload it now? It only happens once.", "O SpeechDesk precisa do modelo de transcrição (NVIDIA Parakeet, cerca de 0,7 GB) para funcionar.\n\nDescarregar agora? Só acontece uma vez."),
        ["model.ask.whisper"] = ("The Whisper engine is selected but its model (about 0.55 GB) is not installed.\n\nDownload it now?", "O motor Whisper está selecionado mas o modelo (cerca de 0,55 GB) não está instalado.\n\nDescarregar agora?"),
        ["model.ask.ai"] = ("AI clean-up and translation need the Qwen 2.5 language model (about 1 GB).\n\nDownload it now?", "A limpeza com IA e a tradução precisam do modelo de linguagem Qwen 2.5 (cerca de 1 GB).\n\nDescarregar agora?"),
        ["model.downloaded"] = ("Model ready: {0}", "Modelo pronto: {0}"),
        ["model.downloadFailed"] = ("Model download failed: {0}", "Falhou o download do modelo: {0}"),
        ["model.declined"] = ("Without the model SpeechDesk cannot transcribe. Use \"Download model\" in the menu when you are ready.", "Sem o modelo o SpeechDesk não consegue transcrever. Usa \"Descarregar modelo\" no menu quando quiseres."),
        ["tray.downloadModel"] = ("Download model", "Descarregar modelo"),

        // Errors
        ["error.cloudKey"] = ("Set the {0} environment variable with your API key.", "Define a variável de ambiente {0} com a chave da API."),
        ["error.sendInput"] = ("Could not type into the active app (it may be running as Administrator).", "Não foi possível escrever na app ativa (pode estar a correr como Administrador)."),
        ["error.hook"] = ("Could not install the keyboard hook.", "Falha ao instalar o hook de teclado."),
    };
}
