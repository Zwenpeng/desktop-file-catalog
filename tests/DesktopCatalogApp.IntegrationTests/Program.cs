using System.Text;
using DesktopCatalogApp.Services;

var root = Path.Combine(Path.GetTempPath(), $"DesktopCatalogAppTest_{Guid.NewGuid():N}");
Directory.CreateDirectory(Path.Combine(root, "资料"));

try
{
    await File.WriteAllTextAsync(Path.Combine(root, "A.txt"), "same-content", Encoding.UTF8);
    await File.WriteAllTextAsync(Path.Combine(root, "B.txt"), "same-content", Encoding.UTF8);
    await File.WriteAllTextAsync(Path.Combine(root, "C.md"), "first", Encoding.UTF8);
    await File.WriteAllTextAsync(Path.Combine(root, "资料", "D.csv"), "x,y\n1,2", Encoding.UTF8);

    var scanner = new ScanService();
    var first = await scanner.ScanAsync(root, progress: null, CancellationToken.None);
    Assert(first.Files.Count == 4, $"首次扫描文件数应为 4，实际 {first.Files.Count}");
    Assert(first.DirectoryCount == 2, $"目录数应为 2，实际 {first.DirectoryCount}");
    Assert(first.Errors.Count == 0, "首次扫描不应有读取错误");

    var history = new HistoryService();
    var firstHistory = await history.CompareAndSaveAsync(first, CancellationToken.None);
    Assert(!firstHistory.HasBaseline, "首次扫描不应已有历史基线");

    var incomplete = first with { Files = [], Errors = ["模拟读取错误"] };
    var incompleteHistory = await history.CompareAndSaveAsync(incomplete, CancellationToken.None);
    Assert(incompleteHistory.IsIncomplete, "读取不完整时应标记历史不可靠");
    Assert(incompleteHistory.RemovedCount == 0, "读取不完整时不应报告文件移除");

    var duplicates = await new DuplicateService().ComputeAsync(first.Files, progress: null, CancellationToken.None);
    Assert(duplicates.GroupCount == 1, $"重复组应为 1，实际 {duplicates.GroupCount}");
    Assert(duplicates.FileCount == 2, $"重复文件数应为 2，实际 {duplicates.FileCount}");

    var staleRoot = Path.Combine(root, "临时查重验证");
    Directory.CreateDirectory(staleRoot);
    await File.WriteAllTextAsync(Path.Combine(staleRoot, "X.txt"), "same", Encoding.UTF8);
    await File.WriteAllTextAsync(Path.Combine(staleRoot, "Y.txt"), "same", Encoding.UTF8);
    var staleScan = await scanner.ScanAsync(staleRoot, progress: null, CancellationToken.None);
    await File.WriteAllTextAsync(Path.Combine(staleRoot, "X.txt"), "different-length", Encoding.UTF8);
    var staleResult = await new DuplicateService().ComputeAsync(staleScan.Files, progress: null, CancellationToken.None);
    Assert(staleResult.GroupCount == 0 && staleResult.ErrorCount > 0,
        "文件在扫描后变化时不应报告为精确重复");

    var linkRoot = Path.Combine(staleRoot, "链接边界验证");
    Directory.CreateDirectory(linkRoot);
    var linkPath = Path.Combine(linkRoot, "link.txt");
    if (TryCreateFileLink(linkPath, Path.Combine(staleRoot, "Y.txt")))
    {
        var linkScan = await scanner.ScanAsync(linkRoot, progress: null, CancellationToken.None);
        Assert(linkScan.Files.Count == 0 && linkScan.SkippedReparsePoints == 1,
            "扫描应跳过文件符号链接，避免读取范围外的目标");

        File.Delete(Path.Combine(staleRoot, "X.txt"));
        if (TryCreateFileLink(Path.Combine(staleRoot, "X.txt"), Path.Combine(staleRoot, "Y.txt")))
        {
            var replacedResult = await new DuplicateService().ComputeAsync(staleScan.Files, progress: null, CancellationToken.None);
            Assert(replacedResult.GroupCount == 0 && replacedResult.ErrorCount > 0,
                "扫描后被替换为链接的文件不应参与查重");
            Console.WriteLine("PASS 文件符号链接：扫描跳过，扫描后替换不参与查重");
        }
    }
    Directory.Delete(staleRoot, recursive: true);

    await File.WriteAllTextAsync(Path.Combine(root, "C.md"), "first-but-now-longer", Encoding.UTF8);
    File.Delete(Path.Combine(root, "资料", "D.csv"));
    await File.WriteAllTextAsync(Path.Combine(root, "E.pdf"), "new", Encoding.UTF8);

    var second = await scanner.ScanAsync(root, progress: null, CancellationToken.None);
    var secondHistory = await history.CompareAndSaveAsync(second, CancellationToken.None);
    Assert(secondHistory.HasBaseline, "第二次扫描应找到历史基线");
    Assert(!secondHistory.IsIncomplete, "完整扫描应保留正常历史状态");
    Assert(secondHistory.NewCount == 1, $"新增应为 1，实际 {secondHistory.NewCount}");
    Assert(secondHistory.ModifiedCount == 1, $"修改应为 1，实际 {secondHistory.ModifiedCount}");
    Assert(secondHistory.RemovedCount == 1, $"移除应为 1，实际 {secondHistory.RemovedCount}");

    var csv = Path.Combine(root, "full.csv");
    var html = Path.Combine(root, "share.html");
    await ExportService.ExportCsvAsync(csv, second.Files, CancellationToken.None);
    await ExportService.ExportShareHtmlAsync(html, second, duplicates, secondHistory, CancellationToken.None);
    var csvText = await File.ReadAllTextAsync(csv);
    var htmlText = await File.ReadAllTextAsync(html);
    Assert(csvText.Contains(root, StringComparison.OrdinalIgnoreCase), "完整 CSV 应包含完整路径");
    Assert((await File.ReadAllLinesAsync(csv)).Length == second.Files.Count + 1,
        "完整 CSV 应包含全部扫描记录和一行表头");
    Assert(!htmlText.Contains(root, StringComparison.OrdinalIgnoreCase), "分享摘要不应包含扫描根路径");
    Assert(!htmlText.Contains("A.txt", StringComparison.Ordinal), "分享摘要不应包含逐文件名称");
    Assert(htmlText.Contains("分享安全版不包含完整文件路径", StringComparison.Ordinal), "分享摘要应包含隐私说明");
    var incompleteHtml = Path.Combine(root, "share-incomplete.html");
    await ExportService.ExportShareHtmlAsync(incompleteHtml, second, staleResult, secondHistory, CancellationToken.None);
    Assert((await File.ReadAllTextAsync(incompleteHtml)).Contains("候选文件未能核对，结果不完整", StringComparison.Ordinal),
        "查重不完整时分享摘要应提示读取错误");

    Console.WriteLine("PASS 扫描：4 个文件、2 个目录");
    Console.WriteLine("PASS 查重：1 组、2 个完全相同文件");
    Console.WriteLine("PASS 历史：新增 1、修改 1、移除 1");
    Console.WriteLine("PASS 导出：CSV 含完整路径，分享 HTML 不含完整路径");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static bool TryCreateFileLink(string linkPath, string targetPath)
{
    try
    {
        File.CreateSymbolicLink(linkPath, targetPath);
        return true;
    }
    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
    {
        Console.WriteLine($"SKIP 文件符号链接验证：当前环境不允许创建链接（{ex.GetType().Name}）");
        return false;
    }
}
