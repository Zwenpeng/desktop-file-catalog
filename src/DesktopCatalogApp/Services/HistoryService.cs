using System.IO.Compression;
using System.IO;
using System.Text.Json;
using DesktopCatalogApp.Models;

namespace DesktopCatalogApp.Services;

public sealed class HistoryService
{
    private readonly string _snapshotDirectory = Path.Combine(AppContext.BaseDirectory, "Data", "snapshots");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public async Task<HistoryComparison> CompareAndSaveAsync(ScanResult result, CancellationToken cancellationToken)
    {
        // A partial scan cannot establish a trustworthy baseline or prove that a file was removed.
        if (result.Errors.Count > 0)
            return new HistoryComparison(0, 0, 0, false, IsIncomplete: true);

        Directory.CreateDirectory(_snapshotDirectory);
        var previous = await LoadLatestForRootAsync(result.RootPath, cancellationToken);
        var comparison = Compare(result.Files, previous);
        await SaveAsync(result, cancellationToken);
        PruneOldSnapshots(result.RootPath, keep: 10);
        return comparison;
    }

    private async Task<Snapshot?> LoadLatestForRootAsync(string rootPath, CancellationToken cancellationToken)
    {
        foreach (var path in Directory.EnumerateFiles(_snapshotDirectory, "*.json.gz")
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var file = File.OpenRead(path);
                await using var gzip = new GZipStream(file, CompressionMode.Decompress);
                var snapshot = await JsonSerializer.DeserializeAsync<Snapshot>(gzip, JsonOptions, cancellationToken);
                if (snapshot is not null && string.Equals(snapshot.RootPath, rootPath, StringComparison.OrdinalIgnoreCase))
                    return snapshot;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
            {
                // A damaged old snapshot must not block a fresh read-only scan.
            }
        }

        return null;
    }

    private static HistoryComparison Compare(List<FileRecord> files, Snapshot? previous)
    {
        if (previous is null) return new HistoryComparison(0, 0, 0, false);

        var old = previous.Items.ToDictionary(item => item.RelativePath, StringComparer.OrdinalIgnoreCase);
        var currentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var newCount = 0;
        var modifiedCount = 0;

        foreach (var file in files)
        {
            currentPaths.Add(file.RelativePath);
            if (!old.TryGetValue(file.RelativePath, out var oldFile))
            {
                file.Change = "新增";
                newCount++;
            }
            else if (oldFile.Length != file.Length || oldFile.ModifiedUtcTicks != file.Modified.ToUniversalTime().Ticks)
            {
                file.Change = "修改";
                modifiedCount++;
            }
        }

        var removedCount = old.Keys.Count(path => !currentPaths.Contains(path));
        return new HistoryComparison(newCount, modifiedCount, removedCount, true);
    }

    private async Task SaveAsync(ScanResult result, CancellationToken cancellationToken)
    {
        var snapshot = new Snapshot(
            result.RootPath,
            DateTime.UtcNow,
            result.Files.Select(file => new SnapshotItem(
                file.RelativePath,
                file.Length,
                file.Modified.ToUniversalTime().Ticks)).ToList());

        var safeRoot = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(result.RootPath)))[..12];
        var nonce = Guid.NewGuid().ToString("N")[..6];
        var path = Path.Combine(_snapshotDirectory, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{safeRoot}_{nonce}.json.gz");
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true);
        await using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
        await JsonSerializer.SerializeAsync(gzip, snapshot, JsonOptions, cancellationToken);
    }

    private void PruneOldSnapshots(string rootPath, int keep)
    {
        var matching = new List<(string Path, DateTime Created)>();
        foreach (var path in Directory.EnumerateFiles(_snapshotDirectory, "*.json.gz"))
        {
            try
            {
                using var file = File.OpenRead(path);
                using var gzip = new GZipStream(file, CompressionMode.Decompress);
                var snapshot = JsonSerializer.Deserialize<Snapshot>(gzip, JsonOptions);
                if (snapshot is not null && string.Equals(snapshot.RootPath, rootPath, StringComparison.OrdinalIgnoreCase))
                    matching.Add((path, snapshot.CreatedUtc));
            }
            catch
            {
                // Ignore unreadable files; never delete an unverified snapshot.
            }
        }

        foreach (var item in matching.OrderByDescending(item => item.Created).Skip(keep))
        {
            try { File.Delete(item.Path); } catch { /* Best-effort pruning of this app's own data only. */ }
        }
    }

    private sealed record Snapshot(string RootPath, DateTime CreatedUtc, List<SnapshotItem> Items);
    private sealed record SnapshotItem(string RelativePath, long Length, long ModifiedUtcTicks);
}
