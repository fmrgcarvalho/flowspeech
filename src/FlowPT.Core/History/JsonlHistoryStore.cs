using System.Text.Json;
using FlowPT.Core.Abstractions;
using FlowPT.Core.Models;

namespace FlowPT.Core.History;

/// <summary>Histórico em JSON Lines (um registo por linha), local em %AppData%\FlowPT.</summary>
public sealed class JsonlHistoryStore(string path) : IHistoryStore
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task AppendAsync(HistoryEntry entry, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.AppendAllTextAsync(path, JsonSerializer.Serialize(entry) + Environment.NewLine, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int count, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return [];
        var lines = await File.ReadAllLinesAsync(path, ct);
        return lines
            .Reverse()
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Take(count)
            .Select(l => JsonSerializer.Deserialize<HistoryEntry>(l)!)
            .ToList();
    }
}
