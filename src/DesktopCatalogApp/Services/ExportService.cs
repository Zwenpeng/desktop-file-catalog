using System.Net;
using System.Text;
using System.IO;
using DesktopCatalogApp.Models;

namespace DesktopCatalogApp.Services;

public static class ExportService
{
    public static async Task ExportCsvAsync(string outputPath, IEnumerable<FileRecord> files, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await writer.WriteLineAsync("文件名,完整路径,分类,扩展名,大小（字节）,修改时间,重复组,变化状态");
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new[]
            {
                file.Name, file.FullPath, file.Category, file.Extension, file.Length.ToString(),
                file.Modified.ToString("yyyy-MM-dd HH:mm:ss"), file.DuplicateGroup, file.Change
            };
            await writer.WriteLineAsync(string.Join(',', row.Select(CsvCell)));
        }
    }

    public static async Task ExportShareHtmlAsync(
        string outputPath,
        ScanResult result,
        DuplicateResult? duplicates,
        HistoryComparison? history,
        CancellationToken cancellationToken)
    {
        var categories = result.Files.GroupBy(file => file.Category)
            .Select(group => new { Name = group.Key, Count = group.Count(), Bytes = group.Sum(file => file.Length) })
            .OrderByDescending(item => item.Bytes)
            .ToList();
        var topFolders = result.Files.GroupBy(file => file.TopFolder)
            .Select(group => new { Name = group.Key, Count = group.Count(), Bytes = group.Sum(file => file.Length) })
            .OrderByDescending(item => item.Bytes)
            .Take(20)
            .ToList();

        var categoryRows = string.Join(Environment.NewLine, categories.Select(item =>
            $"<tr><td>{WebUtility.HtmlEncode(item.Name)}</td><td>{item.Count:N0}</td><td>{FileRecord.FormatBytes(item.Bytes)}</td></tr>"));
        var folderRows = string.Join(Environment.NewLine, topFolders.Select(item =>
            $"<tr><td>{WebUtility.HtmlEncode(item.Name)}</td><td>{item.Count:N0}</td><td>{FileRecord.FormatBytes(item.Bytes)}</td></tr>"));
        var duplicateText = duplicates is null
            ? "尚未执行精确查重"
            : $"{duplicates.GroupCount:N0} 组、{duplicates.FileCount:N0} 个文件，理论冗余 {FileRecord.FormatBytes(duplicates.RedundantBytes)}";
        var historyText = history switch
        {
            { IsIncomplete: true } => "本次扫描有读取错误，历史未更新，无法判断文件是否移除",
            null or { HasBaseline: false } => "首次扫描，暂无历史基线",
            _ => $"新增 {history.NewCount:N0}、修改 {history.ModifiedCount:N0}、移除 {history.RemovedCount:N0}"
        };

        var html = $$"""
            <!doctype html>
            <html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>文件目录摘要</title><style>
            body{font-family:"Segoe UI","Microsoft YaHei",sans-serif;margin:0;background:#f5f7fb;color:#152033}main{max-width:1000px;margin:auto;padding:36px 22px}
            h1{margin:0 0 8px}.muted{color:#64748b}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px;margin:24px 0}.card,section{background:white;border:1px solid #e2e8f0;border-radius:14px;padding:18px}.value{font-size:26px;font-weight:700;margin-top:8px}section{margin:14px 0}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:10px;border-bottom:1px solid #e2e8f0}th{color:#475569}footer{margin-top:24px;color:#64748b;font-size:13px}
            </style></head><body><main><h1>文件目录摘要</h1><p class="muted">生成时间：{{DateTime.Now:yyyy-MM-dd HH:mm}} · 分享安全版不包含完整文件路径和文件清单</p>
            <div class="cards"><div class="card">文件总数<div class="value">{{result.Files.Count:N0}}</div></div><div class="card">目录总数<div class="value">{{result.DirectoryCount:N0}}</div></div><div class="card">逻辑容量<div class="value">{{FileRecord.FormatBytes(result.TotalBytes)}}</div></div><div class="card">读取错误<div class="value">{{result.Errors.Count:N0}}</div></div></div>
            <section><h2>历史变化</h2><p>{{WebUtility.HtmlEncode(historyText)}}</p></section><section><h2>精确重复</h2><p>{{WebUtility.HtmlEncode(duplicateText)}}</p><p class="muted">重复容量仅是理论值，不代表可以直接删除。</p></section>
            <section><h2>文件类型</h2><table><thead><tr><th>分类</th><th>数量</th><th>容量</th></tr></thead><tbody>{{categoryRows}}</tbody></table></section>
            <section><h2>一级目录（前 20）</h2><table><thead><tr><th>目录名</th><th>数量</th><th>容量</th></tr></thead><tbody>{{folderRows}}</tbody></table></section>
            <footer>由“桌面文件管家”离线生成。扫描只读取文件元数据，报告未包含完整路径。</footer></main></body></html>
            """;
        await File.WriteAllTextAsync(outputPath, html, new UTF8Encoding(true), cancellationToken);
    }

    private static string CsvCell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
