using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Studio.Models;
using NewLife.Video.Studio.Services;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>格式转换视图模型（M3-1/2）</summary>
public partial class ConverterViewModel : ViewModelBase
{
    private readonly IConverterService _converterService;

    [ObservableProperty]
    private String _inputPath = "";

    [ObservableProperty]
    private String _outputPath = "";

    [ObservableProperty]
    private String _selectedOutputFormat = ".mp4";

    [ObservableProperty]
    private Int32 _outputWidth;

    [ObservableProperty]
    private Int32 _outputHeight;

    [ObservableProperty]
    private Int32 _outputBitrate;

    [ObservableProperty]
    private String _selectedEncoder = "";

    [ObservableProperty]
    private Int32 _progress;

    [ObservableProperty]
    private Boolean _isConverting;

    [ObservableProperty]
    private String _statusText = "就绪";

    /// <summary>支持的输出格式</summary>
    public ObservableCollection<String> OutputFormats { get; } = [];

    /// <summary>常用的编码器选项</summary>
    public ObservableCollection<String> EncoderOptions { get; } =
    [
        "",                    // 自动
        "libx264",             // H.264 软件
        "libx265",             // H.265 软件
        "libvpx-vp9",          // VP9
        "libsvtav1",           // AV1 (SVT)
        "h264_nvenc",          // H.264 NVIDIA
        "hevc_nvenc",          // H.265 NVIDIA
        "h264_qsv",            // H.264 Intel QSV
        "hevc_qsv",            // H.265 Intel QSV
    ];

    /// <summary>实例化转换器视图模型</summary>
    public ConverterViewModel() : this(new ConverterService()) { }

    /// <summary>实例化转换器视图模型（含依赖注入）</summary>
    public ConverterViewModel(IConverterService converterService)
    {
        _converterService = converterService;

        // 加载支持的格式
        var formats = _converterService.GetSupportedFormats();
        foreach (var fmt in formats)
        {
            OutputFormats.Add(fmt);
        }
        if (OutputFormats.Count > 0) SelectedOutputFormat = OutputFormats[0];
    }

    /// <summary>开始转换</summary>
    [RelayCommand]
    private async Task Convert()
    {
        if (String.IsNullOrWhiteSpace(InputPath) || String.IsNullOrWhiteSpace(OutputPath))
        {
            StatusText = "请选择输入和输出文件";
            return;
        }

        IsConverting = true;
        Progress = 0;
        StatusText = "正在转换...";

        try
        {
            var options = new ConvertOptions
            {
                Width = OutputWidth,
                Height = OutputHeight,
                Bitrate = OutputBitrate,
                Encoder = String.IsNullOrEmpty(SelectedEncoder) ? null : SelectedEncoder,
            };

            await _converterService.ConvertAsync(InputPath, OutputPath, options, p =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Progress = p);
            });

            StatusText = "转换完成";
        }
        catch (Exception ex)
        {
            StatusText = $"转换失败: {ex.Message}";
        }
        finally
        {
            IsConverting = false;
        }
    }

    /// <summary>选择输入文件</summary>
    [RelayCommand]
    private void BrowseInput() { /* TODO: 文件对话框 */ }

    /// <summary>自动生成输出路径</summary>
    partial void OnInputPathChanged(String value)
    {
        if (String.IsNullOrEmpty(value) || !String.IsNullOrEmpty(OutputPath)) return;
        var dir = Path.GetDirectoryName(value) ?? "";
        var name = Path.GetFileNameWithoutExtension(value);
        OutputPath = Path.Combine(dir, $"{name}_converted{SelectedOutputFormat}");
    }

    partial void OnSelectedOutputFormatChanged(String value)
    {
        if (String.IsNullOrEmpty(InputPath) || String.IsNullOrEmpty(value)) return;
        var dir = Path.GetDirectoryName(InputPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(InputPath);
        OutputPath = Path.Combine(dir, $"{name}_converted{value}");
    }
}
