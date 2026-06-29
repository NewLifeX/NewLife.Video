namespace NewLife.Video.Processing;

/// <summary>视频处理器接口。对 VideoFrame 进行变换</summary>
public interface IVideoProcessor
{
    /// <summary>支持的输入像素格式</summary>
    VideoTypes[] SupportedInputFormats { get; }

    /// <summary>输出像素格式</summary>
    VideoTypes OutputFormat { get; }

    /// <summary>处理视频帧</summary>
    /// <param name="frame">输入帧</param>
    /// <returns>处理后的帧</returns>
    VideoFrame ProcessFrame(VideoFrame frame);
}
