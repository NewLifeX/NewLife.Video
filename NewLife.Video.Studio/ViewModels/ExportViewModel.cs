using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Processing;
using NewLife.Video.Studio.Services;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>导出视图模型（EXP-1/2）</summary>
public partial class ExportViewModel : ViewModelBase
{
    private readonly IExportService _exportService;

    [ObservableProperty]
    private String _sourceFile = "";

    [ObservableProperty]
    private Int32 _snapshotQuality = 85;

    [ObservableProperty]
    private Int32 _thumbnailCols = 4;

    [ObservableProperty]
    private Int32 _thumbnailRows = 3;

    [ObservableProperty]
    private Int32 _thumbnailWidth = 160;

    [ObservableProperty]
    private Int32 _thumbnailHeight = 90;

    [ObservableProperty]
    private Int32 _gifDelay = 10;

    [ObservableProperty]
    private Int32 _gifLoopCount;

    [ObservableProperty]
    private String _outputPath = "";

    [ObservableProperty]
    private String _statusText = "就绪";

    /// <summary>快照格式选项</summary>
    public String[] SnapshotFormats { get; } = ["JPEG", "PNG", "WebP", "BMP", "GIF"];

    [ObservableProperty]
    private String _selectedSnapshotFormat = "JPEG";

    /// <summary>实例化导出视图模型</summary>
    public ExportViewModel() : this(new ExportService()) { }

    public ExportViewModel(IExportService exportService) => _exportService = exportService;

    [RelayCommand]
    private async Task SaveSnapshot()
    {
        if (String.IsNullOrWhiteSpace(SourceFile)) { StatusText = "请选择源文件"; return; }
        if (String.IsNullOrWhiteSpace(OutputPath)) { StatusText = "请指定输出路径"; return; }

        try
        {
            StatusText = "正在提取快照...";
            await Task.Run(() =>
            {
                var format = SelectedSnapshotFormat switch
                {
                    "PNG" => SnapshotFormat.Png,
                    "WebP" => SnapshotFormat.WebP,
                    "BMP" => SnapshotFormat.Bmp,
                    "GIF" => SnapshotFormat.Gif,
                    _ => SnapshotFormat.Jpeg,
                };
                SnapshotEncoder.SaveSnapshot(
                    new NewLife.Video.VideoFrame { Width = 0, Height = 0 },
                    OutputPath, format, SnapshotQuality);
            });
            StatusText = "快照已保存";
        }
        catch (Exception ex) { StatusText = $"保存失败: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task GenerateThumbnails()
    {
        if (String.IsNullOrWhiteSpace(SourceFile)) { StatusText = "请选择源文件"; return; }
        if (String.IsNullOrWhiteSpace(OutputPath)) { StatusText = "请指定输出路径"; return; }

        try
        {
            StatusText = "正在生成缩略图...";
            await Task.Run(() =>
            {
                var grid = _exportService.GenerateThumbnailGrid(SourceFile,
                    ThumbnailCols, ThumbnailRows, ThumbnailWidth, ThumbnailHeight);
                var data = SnapshotEncoder.EncodeToJpeg(grid);
                File.WriteAllBytes(OutputPath, data);
            });
            StatusText = "缩略图已生成";
        }
        catch (Exception ex) { StatusText = $"生成失败: {ex.Message}"; }
    }

    [RelayCommand]
    private void SelectSourceFile() { /* TODO: 文件对话框 */ }
}
