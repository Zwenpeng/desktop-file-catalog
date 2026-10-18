namespace DesktopCatalogApp.Services;

public static class CategoryService
{
    private static readonly HashSet<string> Documents = new(StringComparer.OrdinalIgnoreCase)
    { ".doc", ".docx", ".pdf", ".txt", ".md", ".rtf", ".odt", ".wps", ".tex" };

    private static readonly HashSet<string> Spreadsheets = new(StringComparer.OrdinalIgnoreCase)
    { ".xls", ".xlsx", ".xlsm", ".csv", ".tsv", ".ods" };

    private static readonly HashSet<string> Presentations = new(StringComparer.OrdinalIgnoreCase)
    { ".ppt", ".pptx", ".pptm", ".odp" };

    private static readonly HashSet<string> Images = new(StringComparer.OrdinalIgnoreCase)
    { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".tif", ".tiff", ".heic", ".psd", ".ai" };

    private static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase)
    { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v" };

    private static readonly HashSet<string> Audio = new(StringComparer.OrdinalIgnoreCase)
    { ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".wma" };

    private static readonly HashSet<string> Archives = new(StringComparer.OrdinalIgnoreCase)
    { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso", ".img" };

    private static readonly HashSet<string> Code = new(StringComparer.OrdinalIgnoreCase)
    { ".cs", ".csproj", ".sln", ".slnx", ".py", ".ipynb", ".js", ".ts", ".tsx", ".jsx", ".html", ".css", ".scss", ".json", ".xml", ".yaml", ".yml", ".toml", ".sql", ".r", ".m", ".java", ".cpp", ".c", ".h", ".ps1", ".cmd", ".bat", ".sh" };

    private static readonly HashSet<string> Installers = new(StringComparer.OrdinalIgnoreCase)
    { ".exe", ".msi", ".msix", ".appx", ".apk", ".dmg", ".pkg" };

    private static readonly string[] NoiseSegments =
    [
        "\\.git\\", "\\node_modules\\", "\\bin\\", "\\obj\\", "\\__pycache__\\",
        "\\.venv\\", "\\venv\\", "\\dist\\", "\\build\\", "\\.vs\\", "\\.idea\\"
    ];

    public static string GetCategory(string extension)
    {
        if (Documents.Contains(extension)) return "文档/论文";
        if (Spreadsheets.Contains(extension)) return "表格/数据";
        if (Presentations.Contains(extension)) return "演示文稿";
        if (Images.Contains(extension)) return "图片/设计";
        if (Videos.Contains(extension)) return "视频";
        if (Audio.Contains(extension)) return "音频";
        if (Archives.Contains(extension)) return "压缩包/镜像";
        if (Code.Contains(extension)) return "代码/配置";
        if (Installers.Contains(extension)) return "程序/安装包";
        return string.IsNullOrWhiteSpace(extension) ? "无扩展名" : "其他";
    }

    public static bool IsNoisePath(string fullPath)
    {
        var normalized = $"\\{fullPath.Trim('\\')}\\";
        return NoiseSegments.Any(segment => normalized.Contains(segment, StringComparison.OrdinalIgnoreCase));
    }
}
