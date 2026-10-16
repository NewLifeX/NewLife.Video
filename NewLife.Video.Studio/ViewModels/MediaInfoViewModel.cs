using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Studio.Helpers;
using NewLife.Video.Studio.Models;
using NewLife.Video.Studio.Services;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>媒体信息查看视图模型</summary>
public partial class MediaInfoViewModel : ViewModelBase
{
    private readonly IMediaInfoService _mediaInfoService;

    [ObservableProperty]
    private String _filePath = "";

    [ObservableProperty]
    private Boolean _isLoading;

    [ObservableProperty]
    private Boolean _hasInfo;

    [ObservableProperty]
    private MediaFileInfo? _mediaInfo;

    [ObservableProperty]
    private String _statusText = "就绪";

    /// <summary>最近分析的文件</summary>
    public ObservableCollection<String> RecentFiles { get; } = [];

    /// <summary>属性展示列表</summary>
    public ObservableCollection<PropertyItem> Properties { get; } = [];

    /// <summary>实例化媒体信息视图模型</summary>
    public MediaInfoViewModel() : this(new MediaInfoService()) { }

    /// <summary>实例化媒体信息视图模型（含依赖注入）</summary>
    public MediaInfoViewModel(IMediaInfoService mediaInfoService)
    {
        _mediaInfoService = mediaInfoService;
    }

    /// <summary>浏览文件</summary>
    [RelayCommand]
    private async Task BrowseFile()
    {
        var path = await FileDialogHelper.PickFileAsync("选择视频文件", FileDialogHelper.VideoFileType);
        if (String.IsNullOrEmpty(path)) return;

        FilePath = path;
        await Analyze();
    }

    /// <summary>分析文件</summary>
    [RelayCommand]
    private async Task Analyze()
    {
        if (String.IsNullOrWhiteSpace(FilePath)) return;
        if (!File.Exists(FilePath))
        {
            StatusText = "文件不存在";
            return;
        }

        IsLoading = true;
        StatusText = "正在分析...";
        Properties.Clear();
        HasInfo = false;

        try
        {
            var info = await _mediaInfoService.GetInfoAsync(FilePath);
            if (info == null)
            {
                StatusText = "无法分析文件（请确认 FFmpeg 已安装）";
                return;
            }

            MediaInfo = info;
            HasInfo = true;
            StatusText = "分析完成";
            BuildProperties(info);

            if (!RecentFiles.Contains(FilePath))
                RecentFiles.Insert(0, FilePath);
        }
        catch (Exception ex)
        {
            StatusText = $"分析失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildProperties(MediaFileInfo info)
    {
        Properties.Add(new PropertyItem("文件名", info.FileName));
        Properties.Add(new PropertyItem("文件路径", info.FilePath));
        Properties.Add(new PropertyItem("文件大小", info.FileSizeDisplay));
        Properties.Add(new PropertyItem("容器格式", info.Format));
        Properties.Add(new PropertyItem("时长", info.DurationDisplay));
        Properties.Add(new PropertyItem("总码率", info.BitRateDisplay));
        Properties.Add(new PropertyItem("", ""));
        Properties.Add(new PropertyItem("— 视频流 —", ""));
        Properties.Add(new PropertyItem("编码", $"{info.VideoCodec} ({info.VideoCodecLong})"));
        Properties.Add(new PropertyItem("分辨率", info.ResolutionDisplay));
        Properties.Add(new PropertyItem("帧率", info.FrameRateDisplay));
        Properties.Add(new PropertyItem("像素格式", info.PixelFormat ?? "未知"));
        Properties.Add(new PropertyItem("隔行扫描", info.IsInterlaced ? "是" : "否"));
        Properties.Add(new PropertyItem("", ""));
        Properties.Add(new PropertyItem("— 音频流 —", ""));
        Properties.Add(new PropertyItem("信息", info.AudioDisplay));
    }
}

/// <summary>属性条目</summary>
public class PropertyItem
{
    public String Key { get; }
    public String Value { get; }

    public Boolean IsSeparator => String.IsNullOrEmpty(Key) && String.IsNullOrEmpty(Value);

    public PropertyItem(String key, String value)
    {
        Key = key;
        Value = value;
    }
}
