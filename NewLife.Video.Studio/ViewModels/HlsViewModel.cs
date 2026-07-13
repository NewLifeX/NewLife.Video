using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Streaming;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>HLS 分段查看视图模型（M5-3）</summary>
public partial class HlsViewModel : ViewModelBase
{
    [ObservableProperty]
    private String _inputFile = "";

    [ObservableProperty]
    private String _outputDir = "";

    [ObservableProperty]
    private Int32 _segmentDuration = 4000;

    [ObservableProperty]
    private String _playlistContent = "";

    [ObservableProperty]
    private Int32 _segmentCount;

    [ObservableProperty]
    private Boolean _hasResult;

    [ObservableProperty]
    private String _statusText = "就绪";

    /// <summary>分段视频文件</summary>
    [RelayCommand]
    private async Task SegmentFile()
    {
        if (String.IsNullOrWhiteSpace(InputFile) || String.IsNullOrWhiteSpace(OutputDir))
        {
            StatusText = "请选择输入文件和输出目录";
            return;
        }

        try
        {
            StatusText = "正在生成 HLS 分段...";
            var segmenter = new HlsSegmenter(OutputDir, "segment", SegmentDuration);

            await Task.Run(() =>
            {
                // 使用 FFmpeg 读取并分段
                var invoker = new NewLife.Video.FFmpeg.FFmpegInvoker();
                var result = invoker.Execute(
                    $"-i \"{InputFile}\" -c copy -f hls -hls_time {SegmentDuration / 1000.0:F2} " +
                    $"-hls_list_size 0 \"{Path.Combine(OutputDir, "index.m3u8")}\"");

                if (result.Success)
                {
                    // 读取生成的 m3u8
                    var m3u8Path = Path.Combine(OutputDir, "index.m3u8");
                    if (File.Exists(m3u8Path))
                    {
                        PlaylistContent = File.ReadAllText(m3u8Path);
                        SegmentCount = Directory.GetFiles(OutputDir, "segment*.ts").Length;
                        HasResult = true;
                    }
                }
                else
                {
                    StatusText = $"分段失败: {result.Error}";
                }
            });

            StatusText = $"HLS 分段完成，共 {SegmentCount} 个分片";
        }
        catch (Exception ex)
        {
            StatusText = $"分段失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SelectInputFile() { /* TODO */ }

    [RelayCommand]
    private void SelectOutputDir() { /* TODO */ }
}
