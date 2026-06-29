namespace NewLife.Video.Containers;

/// <summary>视频容器读取器接口</summary>
public interface IVideoContainerReader : IDisposable
{
    /// <summary>视频编码类型</summary>
    VideoTypes VideoCodecType { get; }

    /// <summary>视频宽度</summary>
    Int32 VideoWidth { get; }

    /// <summary>视频高度</summary>
    Int32 VideoHeight { get; }

    /// <summary>帧率（每秒帧数）</summary>
    Double FrameRate { get; }

    /// <summary>总帧数（-1 表示未知）</summary>
    Int64 TotalFrames { get; }

    /// <summary>时长（毫秒，-1 表示未知）</summary>
    Int64 Duration { get; }

    /// <summary>读取下一帧编码数据</summary>
    /// <returns>编码数据包，流末尾返回 null</returns>
    VPacket ReadFrame();

    /// <summary>跳转到指定帧</summary>
    /// <param name="frameIndex">帧索引（从 0 开始）</param>
    /// <returns>是否成功跳转</returns>
    Boolean SeekFrame(Int64 frameIndex);
}
