using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using FlowPT.Core.Abstractions;
using FlowPT.Core.Audio;
using FlowPT.Core.Models;
using FlowPT.Core.Settings;

namespace FlowPT.Transcription;

/// <summary>
/// Adaptador para APIs compatíveis com OpenAI /v1/audio/transcriptions (OpenAI, Groq, Azure, servidores locais).
/// Existe para comparação de qualidade/latência; está desativado por omissão ("Engine": "local").
/// </summary>
public sealed class CloudSpeechToText(HttpClient http, CloudSettings settings) : ISpeechToText
{
    public string Name => $"cloud:{settings.Model}";

    public async Task<Transcript> TranscribeAsync(AudioClip clip, TranscriptionOptions options, CancellationToken ct = default)
    {
        var apiKey = Environment.GetEnvironmentVariable(settings.ApiKeyEnvVar)
            ?? throw new InvalidOperationException($"Define a variável de ambiente {settings.ApiKeyEnvVar} com a chave da API.");

        var sw = Stopwatch.StartNew();
        using var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent(WavEncoder.ToWav(clip));
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audio, "file", "dictation.wav");
        form.Add(new StringContent(settings.Model), "model");
        if (options.Language is { Length: > 0 } lang && lang != "auto")
            form.Add(new StringContent(lang), "language");
        if (!string.IsNullOrWhiteSpace(options.Prompt))
            form.Add(new StringContent(options.Prompt), "prompt");

        using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint) { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var text = doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
        return new Transcript(text.Trim(), options.Language, sw.Elapsed);
    }
}
