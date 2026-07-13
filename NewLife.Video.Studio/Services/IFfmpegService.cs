namespace NewLife.Video.Studio.Services;

/// <summary>FFmpeg 命令行服务接口</summary>
public interface IFfmpegService
{
    /// <summary>FFmpeg 是否可用</summary>
    Boolean IsAvailable { get; }

    /// <summary>ffprobe 是否可用</summary>
    Boolean IsProbeAvailable { get; }

    /// <summary>执行 ffmpeg 命令</summary>
    /// <param name="arguments">命令行参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>标准输出内容</returns>
    Task<String> ExecuteAsync(String arguments, CancellationToken cancellationToken = default);

    /// <summary>获取 FFmpeg 版本信息</summary>
    String? GetVersion();
}
