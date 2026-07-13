namespace NewLife.Video.Studio.Models;

/// <summary>转换选项</summary>
public class ConvertOptions
{
    /// <summary>输出宽度（0=保持原始）</summary>
    public Int32 Width { get; set; }

    /// <summary>输出高度（0=保持原始）</summary>
    public Int32 Height { get; set; }

    /// <summary>输出编码器名称（如 libx264、libx265、libvpx-vp9、libsvtav1）</summary>
    public String? Encoder { get; set; }

    /// <summary>码率（bps，0=自动）</summary>
    public Int64 Bitrate { get; set; }

    /// <summary>帧率（0=保持原始）</summary>
    public Double FrameRate { get; set; }

    /// <summary>额外的 FFmpeg 参数</summary>
    public String? ExtraArgs { get; set; }

    /// <summary>预设（如 fast、medium、slow）</summary>
    public String? Preset { get; set; }
}
