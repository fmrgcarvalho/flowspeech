using System.Text;
using FlowSpeech.Core.Abstractions;
using FlowSpeech.Core.Models;
using FlowSpeech.Core.Settings;
using FlowSpeech.Core.Text;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace FlowSpeech.AiCleanup;

/// <summary>
/// Limpeza e tradução opcionais com um modelo de linguagem pequeno a correr dentro da app (llama.cpp via LLamaSharp).
/// Por omissão: Qwen 2.5 1.5B Instruct (GGUF Q4_K_M, ~1 GB).
/// Nunca bloqueia a ditação: se estiver desligada, sem modelo, lenta ou com erro, devolve o texto recebido.
/// </summary>
public sealed class LocalAiFormatter(Func<FlowSettings> settings) : ITextFormatter, IDisposable
{
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private LLamaWeights? _weights;
    private ModelParams? _params;
    private string? _loadedPath;

    public string Name => "ai-local";

    /// <summary>Erro do último carregamento (ex.: modelo em falta), para mostrar na tray.</summary>
    public event EventHandler<string>? LoadFailed;

    public bool IsEnabled => settings().Formatting.Ai.Enabled;

    /// <summary>Avisos não fatais (ex.: tradução falhou e foi escrito o original).</summary>
    public event EventHandler<string>? Warning;

    public async Task<string> FormatAsync(string text, DictationContext context, CancellationToken ct = default)
    {
        var s = settings();
        var translating = context.TranslateTo is not null;
        if ((!s.Formatting.Ai.Enabled && !translating) || string.IsNullOrWhiteSpace(text)) return text;

        var seconds = translating ? s.Translation.TimeoutSeconds : s.Formatting.Ai.TimeoutSeconds;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, seconds)));

        try
        {
            var (weights, parameters) = await EnsureLoadedAsync(s.Formatting.Ai, timeout.Token);

            var executor = new StatelessExecutor(weights, parameters);
            var inference = new InferenceParams
            {
                MaxTokens = AiCleanupPrompt.MaxTokensFor(text),
                AntiPrompts = [AiCleanupPrompt.StopToken],
                SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.1f }
            };

            var prompt = AiCleanupPrompt.Build(text, context.Style, context.TranslateTo);
            var output = new StringBuilder();
            await foreach (var token in executor.InferAsync(prompt, inference, timeout.Token))
                output.Append(token);

            var cleaned = AiCleanupPrompt.Clean(output.ToString());
            if (AiCleanupPrompt.IsPlausibleRewrite(text, cleaned)) return cleaned!;
            if (translating) Warning?.Invoke(this, "A tradução não parecia correta — foi escrito o texto original.");
            return text;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Erro ou timeout → texto sem IA. Na tradução avisa-se, para não passar português despercebido.
            if (translating)
                Warning?.Invoke(this, ex is OperationCanceledException
                    ? "A tradução demorou demasiado — foi escrito o texto original."
                    : $"Não foi possível traduzir ({ex.Message}). Foi escrito o texto original.");
            return text;
        }
    }

    /// <summary>Carrega o modelo em background (no arranque ou quando é ligado no menu).</summary>
    public async Task WarmUpAsync(bool force = false)
    {
        var ai = settings().Formatting.Ai;
        if (!ai.Enabled && !force) return;
        try
        {
            await EnsureLoadedAsync(ai, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LoadFailed?.Invoke(this, ex.Message);
        }
    }

    private async Task<(LLamaWeights, ModelParams)> EnsureLoadedAsync(AiCleanupSettings ai, CancellationToken ct)
    {
        var path = SettingsStore.ResolveModelPath(ai.ModelPath);
        if (_weights is not null && _params is not null && _loadedPath == path) return (_weights, _params);

        await _loadLock.WaitAsync(ct);
        try
        {
            if (_weights is not null && _params is not null && _loadedPath == path) return (_weights, _params);

            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Modelo de IA em falta. Corre scripts/download-model.ps1 -Engine ai. Ficheiro: {path}", path);

            _weights?.Dispose();
            _params = new ModelParams(path)
            {
                ContextSize = 2048,
                GpuLayerCount = ai.GpuLayers
            };
            // Carregar fora da thread de UI; o modelo de 1,5B demora 1–3 s.
            _weights = await Task.Run(() => LLamaWeights.LoadFromFile(_params), CancellationToken.None);
            _loadedPath = path;
            return (_weights, _params);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public void Dispose()
    {
        _weights?.Dispose();
        _weights = null;
    }
}
