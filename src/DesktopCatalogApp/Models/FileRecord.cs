using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DesktopCatalogApp.Models;

public sealed class FileRecord : INotifyPropertyChanged
{
    private string _duplicateGroup = string.Empty;
    private string _change = string.Empty;

    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required string RelativePath { get; init; }
    public required string DirectoryPath { get; init; }
    public required string Extension { get; init; }
    public required string Category { get; init; }
    public required string TopFolder { get; init; }
    public long Length { get; init; }
    public DateTime Modified { get; init; }
    public bool IsNoise { get; init; }

    public string SizeText => FormatBytes(Length);
    public string ModifiedText => Modified.ToString("yyyy-MM-dd HH:mm");
    public string DuplicateText => string.IsNullOrEmpty(DuplicateGroup) ? "—" : DuplicateGroup;
    public string ChangeText => string.IsNullOrEmpty(Change) ? "—" : Change;

    public string DuplicateGroup
    {
        get => _duplicateGroup;
        set
        {
            if (_duplicateGroup == value) return;
            _duplicateGroup = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DuplicateText));
        }
    }

    public string Change
    {
        get => _change;
        set
        {
            if (_change == value) return;
            _change = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ChangeText));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes:N0} B" : $"{value:N2} {units[unit]}";
    }
}

public sealed record ScanProgress(int FileCount, int DirectoryCount, long TotalBytes, string CurrentPath);

public sealed record ScanResult(
    string RootPath,
    List<FileRecord> Files,
    int DirectoryCount,
    int SkippedReparsePoints,
    List<string> Errors)
{
    public long TotalBytes => Files.Sum(file => file.Length);
}

public sealed record DuplicateProgress(int Completed, int Total, string CurrentFile);

public sealed record DuplicateResult(int GroupCount, int FileCount, long RedundantBytes, int ErrorCount);

public sealed record HistoryComparison(
    int NewCount,
    int ModifiedCount,
    int RemovedCount,
    bool HasBaseline,
    bool IsIncomplete = false);
