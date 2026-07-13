using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Studio.Services;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>FFmpeg 控制台视图模型</summary>
public partial class FfmpegConsoleViewModel : ViewModelBase
{
    private readonly IFfmpegService _ffmpegService;

    [ObservableProperty]
    private String _command = "";

    [ObservableProperty]
    private String _output = "";

    [ObservableProperty]
    private Boolean _isRunning;

    [ObservableProperty]
    private String _statusText = "";

    [ObservableProperty]
    private String _ffmpegVersion = "";

    /// <summary>命令历史</summary>
    public ObservableCollection<String> CommandHistory { get; } = [];

    /// <summary>预设命令</summary>
    public ObservableCollection<String> PresetCommands { get; } =
    [
        "-version",
        "-encoders",
        "-decoders",
        "-hwaccels",
        "-formats",
        "-pix_fmts",
        "-codecs",
    ];

    /// <summary>实例化 FFmpeg 控制台视图模型</summary>
    public FfmpegConsoleViewModel() : this(new FfmpegService()) { }

    /// <summary>实例化 FFmpeg 控制台视图模型（含依赖注入）</summary>
    public FfmpegConsoleViewModel(IFfmpegService ffmpegService)
    {
        _ffmpegService = ffmpegService;
        _ffmpegVersion = _ffmpegService.GetVersion() ?? "未检测到 FFmpeg";
        StatusText = _ffmpegService.IsAvailable ? "FFmpeg 可用" : "FFmpeg 不可用";
    }

    /// <summary>执行命令</summary>
    [RelayCommand]
    private async Task Execute()
    {
        if (String.IsNullOrWhiteSpace(Command) || IsRunning) return;

        IsRunning = true;
        Output = "";
        StatusText = "正在执行...";

        try
        {
            var result = await _ffmpegService.ExecuteAsync(Command);
            Output = result;

            CommandHistory.Insert(0, Command);
            StatusText = $"执行完成（退出码 0）";
        }
        catch (Exception ex)
        {
            Output = $"错误: {ex.Message}";
            StatusText = "执行失败";
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>清除输出</summary>
    [RelayCommand]
    private void Clear()
    {
        Output = "";
        StatusText = "已清除";
    }

    /// <summary>使用预设命令</summary>
    [RelayCommand]
    private void UsePreset(String preset)
    {
        Command = preset;
    }
}
