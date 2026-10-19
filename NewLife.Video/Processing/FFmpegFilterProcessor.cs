using NewLife.Video.FFmpeg;

namespace NewLife.Video.Processing;

/// <summary>FFmpeg 滤镜链处理器。将 FFmpeg libavfilter 滤镜能力封装为 IVideoProcessor</summary>
/// <remarks>
/// 通过管道调用 ffmpeg 的 -vf/-filter_complex 滤镜链处理视频帧。
/// 支持所有 FFmpeg 内置滤镜：scale/crop/rotate/hue/unsharp/hflip/vflip/drawtext 等。
/// 每次处理通过独立的 ffmpeg 进程完成，适合非实时批处理场景。
/// 高频实时场景建议使用纯托管处理器（FrameResizer/ColorAdjuster 等）。
/// <para>使用示例：
/// <code>
/// var filter = new FFmpegFilterProcessor("scale=1280:720,hue=s=0.5");
/// var output = filter.ProcessFrame(inputFrame);
/// </code>
/// </para>
/// </remarks>
public class FFmpegFilterProcessor : IVideoProcessor
{
    private readonly FFmpegInvoker _invoker;
    private readonly String _filterString;
    private readonly String _vcodec;

    /// <summary>支持的输入像素格式（YUV420P，ffmpeg 可自动转换其他格式）</summary>
    public VideoTypes[] SupportedInputFormats => [VideoTypes.YUV420P, VideoTypes.NV12, VideoTypes.RGB24, VideoTypes.BGRA32];

    /// <summary>输出像素格式</summary>
    public VideoTypes OutputFormat => VideoTypes.YUV420P;

    /// <summary>创建 FFmpeg 滤镜处理器</summary>
    /// <param name="filterString">FFmpeg 滤镜链字符串，如 "scale=1280:720,format=yuv420p"</param>
    /// <param name="ffmpegPath">FFmpeg 路径（null 时自动查找）</param>
    /// <param name="vcodec">输出像素格式名（默认 yuv420p）</param>
    public FFmpegFilterProcessor(String filterString, String ffmpegPath = null, String vcodec = "yuv420p")
    {
        _filterString = filterString ?? throw new ArgumentNullException(nameof(filterString));
        _invoker = new FFmpegInvoker(ffmpegPath);
        _vcodec = vcodec ?? "yuv420p";
    }

    /// <summary>处理视频帧</summary>
    /// <param name="frame">输入帧</param>
    /// <returns>经滤镜链处理后的 YUV420P 帧</returns>
    public VideoFrame ProcessFrame(VideoFrame frame)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));

        if (!_invoker.IsAvailable())
            throw new InvalidOperationException(L.T("FFmpeg is unavailable; cannot apply filter processing. Ensure ffmpeg is available in PATH."));

        var width = frame.Width;
        var height = frame.Height;

        // 将输入帧转为 YUV420P（如果不是的话需要先转）
        var inputData = GetYUV420PData(frame);

        // 构建 ffmpeg 命令：rawvideo 输入 → 滤镜链 → rawvideo 输出
        var args = $"-f rawvideo -pix_fmt yuv420p -s {width}x{height} -i pipe:0 -vf \"{_filterString}\" -f rawvideo -pix_fmt {_vcodec} pipe:1";

        Byte[] outputData;
        try
        {
            outputData = _invoker.ExecutePipeAsync(args, inputData).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(L.T("FFmpeg filter processing failed: {0}. {1}", _filterString, ex.Message), ex);
        }

        if (outputData == null || outputData.Length == 0)
            throw new InvalidOperationException(L.T("FFmpeg filter processing failed (empty output): {0}.", _filterString));

        // 推断输出尺寸（滤镜可能改变分辨率）
        var outWidth = width;
        var outHeight = height;
        var (newW, newH) = TryParseScaleFromFilter();
        if (newW > 0 && newH > 0)
        {
            outWidth = newW;
            outHeight = newH;
        }
        else if (outputData.Length != width * height * 3 / 2)
        {
            // 从数据量反推
            (outWidth, outHeight) = GuessResolution(outputData.Length);
        }

        var result = VideoFrame.Create(outWidth, outHeight, VideoTypes.YUV420P);
        var copyLen = Math.Min(outputData.Length, result.Data.Length);
        Array.Copy(outputData, 0, result.Data, 0, copyLen);

        result.Timestamp = frame.Timestamp;
        result.FrameIndex = frame.FrameIndex;

        return result;
    }

    #region 辅助

    /// <summary>将任意帧转为 YUV420P 字节数据</summary>
    private static Byte[] GetYUV420PData(VideoFrame frame)
    {
        if (frame.PixelFormat == VideoTypes.YUV420P)
        {
            var data = new Byte[frame.Data.Length];
            Array.Copy(frame.Data, data, frame.Data.Length);
            return data;
        }

        // 非 YUV420P 格式，通过 ffmpeg 临时转换一次
        var pixFmt = frame.PixelFormat switch
        {
            VideoTypes.NV12 => "nv12",
            VideoTypes.RGB24 => "rgb24",
            VideoTypes.BGRA32 => "bgra",
            _ => "yuv420p",
        };

        var invoker = new FFmpegInvoker();
        if (!invoker.IsAvailable()) return [];

        try
        {
            var args = $"-f rawvideo -pix_fmt {pixFmt} -s {frame.Width}x{frame.Height} -i pipe:0 -f rawvideo -pix_fmt yuv420p pipe:1";
            var rawData = new Byte[frame.Data.Length];
            Array.Copy(frame.Data, rawData, frame.Data.Length);
            return invoker.ExecutePipeAsync(args, rawData).GetAwaiter().GetResult() ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>尝试从滤镜字符串中解析 scale 参数，获取输出分辨率</summary>
    private (Int32 W, Int32 H) TryParseScaleFromFilter()
    {
        // 匹配 scale=W:H 或 scale=w=W:h=H 模式
        var match = System.Text.RegularExpressions.Regex.Match(_filterString, @"scale=(?:w=)?(\d+)[:x](?:h=)?(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
            return (Int32.Parse(match.Groups[1].Value), Int32.Parse(match.Groups[2].Value));

        return (0, 0);
    }

    private static (Int32, Int32) GuessResolution(Int32 rawSize)
    {
        foreach (var (w, h) in new[] { (3840, 2160), (1920, 1080), (1280, 720), (960, 540), (640, 480), (352, 288) })
            if (rawSize % (w * h * 3 / 2) == 0) return (w, h);
        return (640, 480);
    }

    #endregion
}
