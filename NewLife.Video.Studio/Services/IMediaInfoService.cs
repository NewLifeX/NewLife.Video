using NewLife.Video.Studio.Models;

namespace NewLife.Video.Studio.Services;

/// <summary>媒体信息查询服务接口</summary>
public interface IMediaInfoService
{
    /// <summary>获取媒体文件信息</summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>媒体信息</returns>
    Task<MediaFileInfo?> GetInfoAsync(String filePath);

    /// <summary>检查 FFmpeg/ffprobe 是否可用</summary>
    Boolean IsAvailable { get; }
}
