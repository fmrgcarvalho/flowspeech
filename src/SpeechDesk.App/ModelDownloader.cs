using System.Diagnostics;
using SpeechDesk.Core.Settings;

namespace SpeechDesk.App;

/// <summary>
/// Downloads models on first use, so the installed app works without running any script.
/// Archives (.tar.bz2) are unpacked with the tar.exe that ships with Windows 10/11.
/// </summary>
public sealed class ModelDownloader(HttpClient http)
{
    public async Task DownloadAsync(ModelSpec spec, AppSettings settings, IProgress<int>? progress, CancellationToken ct = default)
    {
        Directory.CreateDirectory(SettingsStore.ModelsDir);
        var fileName = Path.GetFileName(new Uri(spec.Url).LocalPath);
        var target = spec.IsArchive
            ? Path.Combine(SettingsStore.ModelsDir, fileName)
            : spec.InstalledMarker(settings);
        var partial = target + ".part";

        using (var response = await http.GetAsync(spec.Url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using (var destination = File.Create(partial))
            {
                var buffer = new byte[1 << 20];
                long read = 0;
                var lastReported = -1;
                int n;
                while ((n = await source.ReadAsync(buffer, ct)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, n), ct);
                    read += n;
                    if (total is > 0)
                    {
                        var percent = (int)(read * 100 / total.Value);
                        if (percent != lastReported) progress?.Report(lastReported = percent);
                    }
                }
            }
        }

        File.Move(partial, target, overwrite: true);

        if (spec.IsArchive)
        {
            progress?.Report(-1); // unpacking: no percentage available
            await ExtractAsync(target, SettingsStore.ModelsDir, ct);
            File.Delete(target);
        }

        if (!spec.IsInstalled(settings))
            throw new InvalidOperationException($"{spec.DisplayName}: {spec.InstalledMarker(settings)}");
    }

    private static async Task ExtractAsync(string archive, string destination, CancellationToken ct)
    {
        var tar = Path.Combine(Environment.SystemDirectory, "tar.exe");
        var psi = new ProcessStartInfo(File.Exists(tar) ? tar : "tar")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-xjf");
        psi.ArgumentList.Add(archive);
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(destination);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("tar.exe");
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"tar.exe ({process.ExitCode}): {error}");
    }
}
