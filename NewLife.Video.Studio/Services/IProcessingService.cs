using NewLife.Video.Processing;
using NewLife.Video.Studio.Models;

namespace NewLife.Video.Studio.Services;

/// <summary>帧处理管线服务接口（M4-1）</summary>
public interface IProcessingService
{
    /// <summary>处理单帧图像</summary>
    /// <param name="frame">输入帧</param>
    /// <param name="options">处理选项</param>
    /// <returns>处理后的帧</returns>
    VideoFrame ProcessFrame(VideoFrame frame, ProcessingOptions options);

    /// <summary>获取可用的处理器列表</summary>
    IList<String> GetAvailableProcessors();
}
