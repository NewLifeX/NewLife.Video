namespace NewLife.Video.Studio.Models;

/// <summary>媒体文件信息</summary>
public class MediaFileInfo
{
    /// <summary>文件名</summary>
    public String FileName { get; set; } = "";

    /// <summary>文件路径</summary>
    public String FilePath { get; set; } = "";

    /// <summary>文件大小</summary>
    public Int64 FileSize { get; set; }

    /// <summary>文件大小可读格式</summary>
    public String FileSizeDisplay => FileSize switch
    {
        < 1024 => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{FileSize / (1024.0 * 1024):F1} MB",
        _ => $"{FileSize / (1024.0 * 1024 * 1024):F2} GB"
    };

    /// <summary>容器格式</summary>
    public String Format { get; set; } = "";

    /// <summary>容器格式详细名称</summary>
    public String? FormatLong { get; set; }

    /// <summary>时长（秒）</summary>
    public Double Duration { get; set; }

    /// <summary>时长可读格式</summary>
    public String DurationDisplay
    {
        get
        {
            if (Duration <= 0) return "未知";
            var ts = TimeSpan.FromSeconds(Duration);
            return ts.Hours > 0
                ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
    }

    /// <summary>总码率（bps）</summary>
    public Int64 BitRate { get; set; }

    /// <summary>码率可读格式</summary>
    public String BitRateDisplay => BitRate > 0 ? $"{BitRate / 1000.0:F0} kbps" : "未知";

    /// <summary>视频编码</summary>
    public String VideoCodec { get; set; } = "";

    /// <summary>视频编码详细名称</summary>
    public String? VideoCodecLong { get; set; }

    /// <summary>宽度</summary>
    public Int32 Width { get; set; }

    /// <summary>高度</summary>
    public Int32 Height { get; set; }

    /// <summary>分辨率描述</summary>
    public String ResolutionDisplay => Width > 0 && Height > 0 ? $"{Width}×{Height}" : "未知";

    /// <summary>帧率</summary>
    public Double FrameRate { get; set; }

    /// <summary>帧率描述</summary>
    public String FrameRateDisplay => FrameRate > 0 ? $"{FrameRate:F2} fps" : "未知";

    /// <summary>像素格式</summary>
    public String? PixelFormat { get; set; }

    /// <summary>是否隔行扫描</summary>
    public Boolean IsInterlaced { get; set; }

    /// <summary>音频编码</summary>
    public String? AudioCodec { get; set; }

    /// <summary>音频采样率</summary>
    public Int32 AudioSampleRate { get; set; }

    /// <summary>音频声道数</summary>
    public Int32 AudioChannels { get; set; }

    /// <summary>音频描述</summary>
    public String AudioDisplay
    {
        get
        {
            if (String.IsNullOrEmpty(AudioCodec)) return "无音频";
            var parts = new List<String> { AudioCodec };
            if (AudioSampleRate > 0) parts.Add($"{AudioSampleRate / 1000.0:F0} kHz");
            if (AudioChannels > 0) parts.Add(AudioChannels switch
            {
                1 => "单声道",
                2 => "立体声",
                _ => $"{AudioChannels} 声道"
            });
            return String.Join(", ", parts);
        }
    }
}
