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
/// Limpeza opcional com um modelo de linguagem pequeno a correr dentro da app (llama.cpp via LLamaSharp).
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

    public async Task<string> FormatAsync(string text, DictationContext context, CancellationToken ct = default)
    {
        var ai = settings().Formatting.Ai;
        if (!ai.Enabled || string.IsNullOrWhiteSpace(text)) return text;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, ai.TimeoutSeconds)));

        try
        {
            var (weights, parameters) = await EnsureLoadedAsync(ai, timeout.Token);

            var executor = new StatelessExecutor(weights, parameters);
            var inference = new InferenceParams
            {
                MaxTokens = AiCleanupPrompt.MaxTokensFor(text),
                AntiPrompts = [AiCleanupPrompt.StopToken],
                SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.1f }
            };

            var output = new StringBuilder();
            await foreach (var token in executor.InferAsync(AiCleanupPrompt.Build(text, context.Style), inference, timeout.Token))
                output.Append(token);

            var cleaned = AiCleanupPrompt.Clean(output.ToString());
            return AiCleanupPrompt.IsPlausibleRewrite(text, cleaned) ? cleaned! : text;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return text; // timeout ou erro → texto sem IA
        }
    }

    /// <summary>Carrega o modelo em background (no arranque ou quando é ligado no menu).</summary>
    public async Task WarmUpAsync()
    {
        var ai = settings().Formatting.Ai;
        if (!ai.Enabled) return;
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
