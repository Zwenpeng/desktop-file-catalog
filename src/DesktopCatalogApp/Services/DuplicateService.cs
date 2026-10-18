using System.Security.Cryptography;
using System.IO;
using DesktopCatalogApp.Models;

namespace DesktopCatalogApp.Services;

public sealed class DuplicateService
{
    public Task<DuplicateResult> ComputeAsync(
        IReadOnlyList<FileRecord> files,
        IProgress<DuplicateProgress>? progress,
        CancellationToken cancellationToken) =>
        Task.Run(async () => await ComputeCoreAsync(files, progress, cancellationToken), cancellationToken);

    private static async Task<DuplicateResult> ComputeCoreAsync(
        IReadOnlyList<FileRecord> files,
        IProgress<DuplicateProgress>? progress,
        CancellationToken cancellationToken)
    {
        foreach (var file in files) file.DuplicateGroup = string.Empty;

        var candidates = files
            .GroupBy(file => file.Length)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .ToList();

        var hashGroups = new Dictionary<(long Length, string Hash), List<FileRecord>>();
        var completed = 0;
        var errors = 0;

        foreach (var file in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file.FullPath);
                var scannedModifiedUtc = file.Modified.ToUniversalTime();
                if (info.Length != file.Length || info.LastWriteTimeUtc != scannedModifiedUtc)
                    throw new IOException("File changed since the scan.");

                await using var stream = new FileStream(
                    file.FullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    1024 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));

                info.Refresh();
                if (info.Length != file.Length || info.LastWriteTimeUtc != scannedModifiedUtc)
                    throw new IOException("File changed during duplicate checking.");

                var key = (file.Length, hash);
                if (!hashGroups.TryGetValue(key, out var group))
                {
                    group = [];
                    hashGroups[key] = group;
                }
                group.Add(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
            {
                errors++;
            }

            completed++;
            if (completed % 10 == 0 || completed == candidates.Count)
                progress?.Report(new DuplicateProgress(completed, candidates.Count, file.Name));
        }

        var exactGroups = hashGroups.Values
            .Where(group => group.Count > 1)
            .OrderByDescending(group => group[0].Length * (group.Count - 1L))
            .ToList();

        long redundantBytes = 0;
        var index = 1;
        foreach (var group in exactGroups)
        {
            var label = $"重复组 {index}";
            foreach (var file in group) file.DuplicateGroup = label;
            redundantBytes += group[0].Length * (group.Count - 1L);
            index++;
        }

        return new DuplicateResult(exactGroups.Count, exactGroups.Sum(group => group.Count), redundantBytes, errors);
    }
}
