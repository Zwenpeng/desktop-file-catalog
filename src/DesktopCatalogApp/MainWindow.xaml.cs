using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using DesktopCatalogApp.Models;
using DesktopCatalogApp.Services;
using Microsoft.Win32;

namespace DesktopCatalogApp;

public partial class MainWindow : Window
{
    private readonly ScanService _scanService = new();
    private readonly DuplicateService _duplicateService = new();
    private readonly HistoryService _historyService = new();
    private CancellationTokenSource? _taskCancellation;
    private ScanResult? _scanResult;
    private DuplicateResult? _duplicateResult;
    private HistoryComparison? _historyComparison;
    private ListCollectionView? _view;
    private bool _darkMode;

    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PathBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        CategoryFilter.ItemsSource = new[] { "全部分类" };
        CategoryFilter.SelectedIndex = 0;
        DuplicateFilter.ItemsSource = new[] { "全部状态", "内容完全相同", "未标记重复" };
        DuplicateFilter.SelectedIndex = 0;
        SearchBox.ToolTip = "输入多个关键词时需全部匹配，例如：课程 报告";
        Keyboard.Focus(PathBox);
    }

    private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择要只读扫描的文件夹",
            InitialDirectory = Directory.Exists(PathBox.Text) ? PathBox.Text : null,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true) PathBox.Text = dialog.FolderName;
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        var requestedPath = PathBox.Text.Trim().Trim('"');
        if (!Directory.Exists(requestedPath))
        {
            MessageBox.Show(this, "请选择一个实际存在的文件夹。", "路径无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _taskCancellation?.Cancel();
        _taskCancellation = new CancellationTokenSource();
        var token = _taskCancellation.Token;
        SetBusy(true, indeterminate: true);
        ResetResultsForScan();
        StatusText.Text = "正在只读扫描文件元数据……";

        var progress = new Progress<ScanProgress>(value =>
        {
            FileCountText.Text = value.FileCount.ToString("N0");
            DirectoryCountText.Text = value.DirectoryCount.ToString("N0");
            TotalBytesText.Text = FileRecord.FormatBytes(value.TotalBytes);
            StatusText.Text = $"正在扫描：{value.FileCount:N0} 个文件 · {ShortenPath(value.CurrentPath)}";
        });

        try
        {
            _scanResult = await _scanService.ScanAsync(requestedPath, progress, token);
            _historyComparison = await _historyService.CompareAndSaveAsync(_scanResult, token);
            BindResults(_scanResult);
            UpdateSummary();
            StatusText.Text = $"扫描完成：{_scanResult.Files.Count:N0} 个文件；只读取了名称、路径、大小和时间等元数据。";
            if (_scanResult.SkippedReparsePoints > 0)
                StatusText.Text += $" 已跳过 {_scanResult.SkippedReparsePoints:N0} 个文件或目录联接/重解析点。";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "任务已取消，未修改任何被扫描文件。";
        }
        catch (Exception ex)
        {
            StatusText.Text = "扫描失败。";
            MessageBox.Show(this, ex.Message, "扫描失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false, indeterminate: false);
            _taskCancellation.Dispose();
            _taskCancellation = null;
        }
    }

    private async void DuplicateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_scanResult is null || _scanResult.Files.Count == 0) return;

        _taskCancellation = new CancellationTokenSource();
        var token = _taskCancellation.Token;
        SetBusy(true, indeterminate: false);
        TaskProgress.Minimum = 0;
        TaskProgress.Maximum = 1;
        TaskProgress.Value = 0;
        StatusText.Text = "正在筛选同大小文件并计算 SHA-256……大文件较多时需要一些时间。";

        var progress = new Progress<DuplicateProgress>(value =>
        {
            TaskProgress.Maximum = Math.Max(1, value.Total);
            TaskProgress.Value = value.Completed;
            StatusText.Text = $"精确查重：{value.Completed:N0} / {value.Total:N0} · {value.CurrentFile}";
        });

        try
        {
            _duplicateResult = await _duplicateService.ComputeAsync(_scanResult.Files, progress, token);
            _view?.Refresh();
            UpdateSummary();
            StatusText.Text = $"查重完成：{_duplicateResult.GroupCount:N0} 组、{_duplicateResult.FileCount:N0} 个内容完全相同的文件。理论冗余不是删除建议。";
            if (_duplicateResult.ErrorCount > 0)
                StatusText.Text += $" 有 {_duplicateResult.ErrorCount:N0} 个候选文件未能核对，结果不完整。";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "查重已取消；已计算的临时标记不会删除或改动任何文件。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "查重失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false, indeterminate: false);
            _taskCancellation.Dispose();
            _taskCancellation = null;
        }
    }

    private void BindResults(ScanResult result)
    {
        _view = (ListCollectionView)CollectionViewSource.GetDefaultView(result.Files);
        _view.Filter = FilterFile;
        _view.SortDescriptions.Clear();
        _view.SortDescriptions.Add(new SortDescription(nameof(FileRecord.Modified), ListSortDirection.Descending));
        FileGrid.ItemsSource = _view;

        var categories = new[] { "全部分类" }
            .Concat(result.Files.Select(file => file.Category).Distinct().OrderBy(value => value))
            .ToArray();
        CategoryFilter.ItemsSource = categories;
        CategoryFilter.SelectedIndex = 0;
        DuplicateFilter.SelectedIndex = 0;
        SearchBox.Clear();
        HideNoiseCheck.IsChecked = false;
        RefreshVisibleCount();
    }

    private bool FilterFile(object item)
    {
        if (item is not FileRecord file) return false;

        var terms = (SearchBox?.Text ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length > 0)
        {
            var searchable = file.Name + " " + file.RelativePath;
            if (terms.Any(term => !searchable.Contains(term, StringComparison.OrdinalIgnoreCase))) return false;
        }

        var selectedCategory = CategoryFilter?.SelectedItem as string;
        if (!string.IsNullOrEmpty(selectedCategory) && selectedCategory != "全部分类" && file.Category != selectedCategory)
            return false;

        var duplicateFilter = DuplicateFilter?.SelectedItem as string;
        if (duplicateFilter == "内容完全相同" && string.IsNullOrEmpty(file.DuplicateGroup)) return false;
        if (duplicateFilter == "未标记重复" && !string.IsNullOrEmpty(file.DuplicateGroup)) return false;
        if (HideNoiseCheck?.IsChecked == true && file.IsNoise) return false;
        return true;
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_view is null) return;
        _view.Refresh();
        RefreshVisibleCount();
    }

    private void RefreshVisibleCount()
    {
        if (_view is null || _scanResult is null)
        {
            VisibleCountText.Text = "尚未扫描";
            return;
        }

        VisibleCountText.Text = $"显示 {_view.Count:N0} / {_scanResult.Files.Count:N0}";
    }

    private void UpdateSummary()
    {
        if (_scanResult is null) return;
        FileCountText.Text = _scanResult.Files.Count.ToString("N0");
        DirectoryCountText.Text = _scanResult.DirectoryCount.ToString("N0");
        TotalBytesText.Text = FileRecord.FormatBytes(_scanResult.TotalBytes);
        DuplicateGroupText.Text = _duplicateResult?.GroupCount.ToString("N0") ?? "—";
        RedundantBytesText.Text = _duplicateResult is null ? "—" : FileRecord.FormatBytes(_duplicateResult.RedundantBytes);
        HistoryText.Text = _historyComparison switch
        {
            { IsIncomplete: true } => "读取不完整，历史未更新",
            null or { HasBaseline: false } => "首次建立基线",
            _ => $"+{_historyComparison.NewCount:N0}  ~{_historyComparison.ModifiedCount:N0}  -{_historyComparison.RemovedCount:N0}"
        };
        ShowErrorsButton.Visibility = _scanResult.Errors.Count > 0 || _scanResult.SkippedReparsePoints > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        DuplicateButton.IsEnabled = _scanResult.Files.Count > 0;
        ExportCsvButton.IsEnabled = _scanResult.Files.Count > 0;
        ExportShareButton.IsEnabled = true;
    }

    private void ResetResultsForScan()
    {
        _scanResult = null;
        _duplicateResult = null;
        _historyComparison = null;
        _view = null;
        FileGrid.ItemsSource = null;
        FileCountText.Text = "0";
        DirectoryCountText.Text = "0";
        TotalBytesText.Text = "0 B";
        DuplicateGroupText.Text = "—";
        RedundantBytesText.Text = "—";
        HistoryText.Text = "—";
        VisibleCountText.Text = "正在扫描";
        ShowErrorsButton.Visibility = Visibility.Collapsed;
    }

    private void SetBusy(bool busy, bool indeterminate)
    {
        ScanButton.IsEnabled = !busy;
        DuplicateButton.IsEnabled = !busy && _scanResult?.Files.Count > 0;
        ExportCsvButton.IsEnabled = !busy && _scanResult?.Files.Count > 0;
        ExportShareButton.IsEnabled = !busy && _scanResult is not null;
        CancelButton.IsEnabled = busy;
        TaskProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        TaskProgress.IsIndeterminate = busy && indeterminate;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _taskCancellation?.Cancel();

    private async void ExportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        if (_scanResult is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "导出完整文件清单",
            Filter = "CSV 文件 (*.csv)|*.csv",
            FileName = $"文件目录_完整清单_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            await ExportService.ExportCsvAsync(dialog.FileName, _scanResult.Files, CancellationToken.None);
            StatusText.Text = $"已导出全部 {_scanResult.Files.Count:N0} 条扫描记录：{dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExportShareButton_Click(object sender, RoutedEventArgs e)
    {
        if (_scanResult is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "导出可分享摘要",
            Filter = "网页文件 (*.html)|*.html",
            FileName = $"文件目录_分享摘要_{DateTime.Now:yyyyMMdd_HHmm}.html"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            await ExportService.ExportShareHtmlAsync(dialog.FileName, _scanResult, _duplicateResult, _historyComparison, CancellationToken.None);
            StatusText.Text = $"已导出隐私友好的分享摘要：{dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowErrorsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_scanResult is null) return;
        var lines = new List<string>
        {
            $"跳过的文件或目录联接/重解析点：{_scanResult.SkippedReparsePoints:N0}",
            $"无法读取的路径：{_scanResult.Errors.Count:N0}",
            "",
            "这些路径没有计入结果；应用没有尝试修改权限。"
        };
        if (_scanResult.Errors.Count > 0)
        {
            lines.Add("");
            lines.AddRange(_scanResult.Errors.Take(60));
            if (_scanResult.Errors.Count > 60) lines.Add("……其余错误已省略。 ");
        }
        MessageBox.Show(this, string.Join(Environment.NewLine, lines), "扫描边界", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void FileGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileGrid.SelectedItem is FileRecord file) OpenFile(file);
    }

    private void OpenFileMenu_Click(object sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is FileRecord file) OpenFile(file);
    }

    private void OpenFolderMenu_Click(object sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is not FileRecord file) return;
        try
        {
            var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            start.ArgumentList.Add($"/select,{file.FullPath}");
            Process.Start(start);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "无法打开文件夹", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyPathMenu_Click(object sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is FileRecord file) Clipboard.SetText(file.FullPath);
    }

    private void OpenFile(FileRecord file)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file.FullPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "无法打开文件", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        const string text = "1. 选择文件夹后点击“开始扫描”。扫描只读取名称、路径、大小、时间等元数据。\n\n" +
                            "2. “精确查重”会读取候选文件内容并计算 SHA-256，但不会删除或修改文件。\n\n" +
                            "3. “完整 CSV”含本机完整路径，只适合自己分析；“分享摘要”不含逐文件清单和完整路径，更适合发给他人。\n\n" +
                            "4. 每次扫描会在应用旁的 Data 文件夹保存压缩历史快照，用于比较新增、修改和移除。最多保留同一目录的 10 次记录。\n\n" +
                            "5. 应用主动跳过文件和目录联接/重解析点，避免超出所选目录的扫描范围或重复计数。";
        MessageBox.Show(this, text, "使用说明", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _darkMode = !_darkMode;
        if (SystemParameters.HighContrast)
        {
            MessageBox.Show(this, "系统正在使用高对比度模式，应用会优先尊重系统配色。", "主题", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetBrush("AppBackgroundBrush", _darkMode ? "#0F172A" : "#F4F7FB");
        SetBrush("SurfaceBrush", _darkMode ? "#172033" : "#FFFFFF");
        SetBrush("SurfaceAltBrush", _darkMode ? "#1E293B" : "#F8FAFC");
        SetBrush("TextBrush", _darkMode ? "#F1F5F9" : "#172033");
        SetBrush("MutedTextBrush", _darkMode ? "#A8B3C7" : "#64748B");
        SetBrush("BorderBrush", _darkMode ? "#334155" : "#DCE3ED");
        ThemeButton.Content = _darkMode ? "浅色模式" : "深色模式";
    }

    private static void SetBrush(string key, string hex) =>
        Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

    private static string ShortenPath(string path) =>
        path.Length <= 80 ? path : "…" + path[^77..];

    private void MainWindow_Closing(object? sender, CancelEventArgs e) => _taskCancellation?.Cancel();
}
