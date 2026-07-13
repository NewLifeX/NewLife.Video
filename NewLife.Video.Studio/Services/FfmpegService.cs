using NewLife.Video.FFmpeg;

namespace NewLife.Video.Studio.Services;

/// <summary>FFmpeg 命令行服务</summary>
public class FfmpegService : IFfmpegService
{
    private readonly FFmpegInvoker _invoker;

    /// <summary>FFmpeg 是否可用</summary>
    public Boolean IsAvailable => _invoker.IsAvailable();

    /// <summary>ffprobe 是否可用</summary>
    public Boolean IsProbeAvailable => _invoker.IsProbeAvailable();

    /// <summary>实例化 FFmpeg 服务</summary>
    public FfmpegService() => _invoker = new FFmpegInvoker();

    /// <summary>执行 ffmpeg 命令</summary>
    public async Task<String> ExecuteAsync(String arguments, CancellationToken cancellationToken = default)
    {
        var result = await Task.Run(() => _invoker.Execute(arguments), cancellationToken);
        return result.Output;
    }

    /// <summary>获取 FFmpeg 版本信息</summary>
    public String? GetVersion()
    {
        try
        {
            var result = _invoker.Execute("-version");
            if (result == null || !result.Success) return null;
            var output = result.Output ?? "";
            // 取第一行作为版本信息
            var firstLine = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            return firstLine.Length > 0 ? firstLine[0] : output.Trim();
        }
        catch
        {
            return null;
        }
    }
}
