using System.Diagnostics;
using System.IO;
using DesktopCatalogApp.Models;

namespace DesktopCatalogApp.Services;

public sealed class ScanService
{
    public Task<ScanResult> ScanAsync(
        string rootPath,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken) =>
        Task.Run(() => Scan(rootPath, progress, cancellationToken), cancellationToken);

    private static ScanResult Scan(
        string rootPath,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var files = new List<FileRecord>();
        var errors = new List<string>();
        var folders = new Stack<string>();
        var directoryCount = 0;
        var skippedReparsePoints = 0;
        long totalBytes = 0;
        var timer = Stopwatch.StartNew();
        folders.Push(root);

        while (folders.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = folders.Pop();
            directoryCount++;

            try
            {
                foreach (var filePath in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        // File links can point outside the selected root, just like directory links.
                        if (File.GetAttributes(filePath).HasFlag(FileAttributes.ReparsePoint))
                        {
                            skippedReparsePoints++;
                            continue;
                        }

                        var info = new FileInfo(filePath);
                        var relative = Path.GetRelativePath(root, info.FullName);
                        var relativeDirectory = Path.GetDirectoryName(relative) ?? string.Empty;
                        var topFolder = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                        if (string.Equals(topFolder, info.Name, StringComparison.OrdinalIgnoreCase)) topFolder = "（根目录）";

                        files.Add(new FileRecord
                        {
                            Name = info.Name,
                            FullPath = info.FullName,
                            RelativePath = relative,
                            DirectoryPath = relativeDirectory,
                            Extension = info.Extension.ToLowerInvariant(),
                            Category = CategoryService.GetCategory(info.Extension),
                            TopFolder = topFolder,
                            Length = info.Length,
                            Modified = info.LastWriteTime,
                            IsNoise = CategoryService.IsNoisePath(info.FullName)
                        });
                        totalBytes += info.Length;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                    {
                        if (errors.Count < 200) errors.Add($"{filePath}：{ex.Message}");
                    }

                    if (timer.ElapsedMilliseconds >= 200)
                    {
                        progress?.Report(new ScanProgress(files.Count, directoryCount, totalBytes, current));
                        timer.Restart();
                    }
                }

                foreach (var directoryPath in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var attributes = File.GetAttributes(directoryPath);
                        if (attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            skippedReparsePoints++;
                            continue;
                        }

                        folders.Push(directoryPath);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                    {
                        if (errors.Count < 200) errors.Add($"{directoryPath}：{ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                if (errors.Count < 200) errors.Add($"{current}：{ex.Message}");
            }
        }

        progress?.Report(new ScanProgress(files.Count, directoryCount, totalBytes, root));
        return new ScanResult(root, files, directoryCount, skippedReparsePoints, errors);
    }
}
