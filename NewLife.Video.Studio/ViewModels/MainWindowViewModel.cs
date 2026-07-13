using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>导航项</summary>
public class NavItem
{
    public required String Title { get; set; }
    public required String Icon { get; set; }  // Unicode 图标
    public required Type ViewModelType { get; set; }
}

/// <summary>主窗口视图模型</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private NavItem? _selectedNav;

    /// <summary>导航项列表</summary>
    public ObservableCollection<NavItem> NavItems { get; } =
    [
        new() { Title = "播放器",  Icon = "\u25B6", ViewModelType = typeof(PlayerViewModel) },
        new() { Title = "媒体信息", Icon = "\u2139", ViewModelType = typeof(MediaInfoViewModel) },
        new() { Title = "设备管理", Icon = "\uD83D\uDCF7", ViewModelType = typeof(DeviceManagerViewModel) },
        new() { Title = "摄像头",  Icon = "\uD83D\uDCF7", ViewModelType = typeof(CameraCaptureViewModel) },
        new() { Title = "屏幕捕获", Icon = "\uD83D\uDDA5", ViewModelType = typeof(ScreenCaptureViewModel) },
        new() { Title = "格式转换", Icon = "\u21C4", ViewModelType = typeof(ConverterViewModel) },
        new() { Title = "帧处理",  Icon = "\uD83C\uDFA8", ViewModelType = typeof(ProcessingViewModel) },
        new() { Title = "ONVIF",   Icon = "\uD83D\uDD0D", ViewModelType = typeof(OnvifViewModel) },
        new() { Title = "流媒体",  Icon = "\uD83C\uDF0D", ViewModelType = typeof(StreamingViewModel) },
        new() { Title = "HLS",     Icon = "\uD83C\uDFB6", ViewModelType = typeof(HlsViewModel) },
        new() { Title = "批量",    Icon = "\u2696", ViewModelType = typeof(BatchViewModel) },
        new() { Title = "帧浏览",  Icon = "\uD83D\uDCF1", ViewModelType = typeof(FrameBrowserViewModel) },
        new() { Title = "运动检测",Icon = "\uD83C\uDFAF", ViewModelType = typeof(MotionDetectViewModel) },
        new() { Title = "导出",    Icon = "\u2B07", ViewModelType = typeof(ExportViewModel) },
        new() { Title = "FFmpeg",  Icon = "\u2699", ViewModelType = typeof(FfmpegConsoleViewModel) },
    ];

    /// <summary>当前窗口标题</summary>
    public String WindowTitle => "NewLife.Video.Studio";

    /// <summary>导航命令</summary>
    [RelayCommand]
    private void Navigate(NavItem? item)
    {
        if (item == null) return;
        SelectedNav = item;
        CurrentView = Activator.CreateInstance(item.ViewModelType) as ViewModelBase;
    }
}
