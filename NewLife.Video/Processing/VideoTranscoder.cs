using NewLife.Video.Codecs;
using NewLife.Video.Containers;

namespace NewLife.Video.Processing;

/// <summary>视频转码器。提供一键式转码管线，内部编排容器读取→解码→帧处理→编码→容器写入</summary>
/// <remarks>
/// 高级 API，简化常见转码场景：
/// <code>
/// // 格式转换：将 AVI 转换为 MP4
/// VideoTranscoder.Transcode("input.avi", "output.mp4");
///
/// // 带缩放转码
/// VideoTranscoder.Transcode("input.mkv", "output.mp4", new TranscodeOptions
/// {
///     Width = 1280,
///     Height = 720,
///     Bitrate = 3000,
///     FrameRate = 30,
/// });
///
/// // 自定义编码器
/// VideoTranscoder.Transcode("input.ts", "output.h264", new TranscodeOptions
/// {
///     EncoderName = "h264_nvenc",
///     ExtraArgs = "-preset p4 -tune hq",
/// });
/// </code>
/// </remarks>
public static class VideoTranscoder
{
    /// <summary>一键转码。自动检测输入格式，输出到目标文件</summary>
    /// <param name="inputPath">输入文件路径</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="options">转码选项（可空，使用默认参数）</param>
    /// <param name="progress">进度回调（0-100）</param>
    /// <returns>是否成功</returns>
    public static Boolean Transcode(String inputPath, String outputPath, TranscodeOptions options = null, Action<Int32> progress = null)
    {
        if (String.IsNullOrEmpty(inputPath)) throw new ArgumentNullException(nameof(inputPath));
        if (String.IsNullOrEmpty(outputPath)) throw new ArgumentNullException(nameof(outputPath));
        if (!File.Exists(inputPath)) throw new FileNotFoundException("输入文件不存在", inputPath);

        options ??= new TranscodeOptions();
        progress?.Invoke(0);

        try
        {
            // 1. 创建读取器
            using var reader = VideoContainerFactory.CreateReader(inputPath);
            if (reader == null) throw new NotSupportedException($"不支持的输入格式：{Path.GetExtension(inputPath)}");

            // 2. 创建写入器
            var outputExt = Path.GetExtension(outputPath)?.ToLowerInvariant();
            using var writer = VideoContainerFactory.CreateWriter(outputPath);

            // 根据透传选项决定编解码器类型
            var codecType = reader.VideoCodecType;
            if (options.EncoderName != null)
            {
                // 当指定硬件编码器时，确认对应类型
                codecType = options.EncoderName.Contains("265") || options.EncoderName.Contains("hevc") ?
                    VideoTypes.H265 : VideoTypes.H264;
            }

            // 3. 写入元数据
            var outWidth = options.Width > 0 ? options.Width : reader.VideoWidth;
            var outHeight = options.Height > 0 ? options.Height : reader.VideoHeight;
            var outRate = options.FrameRate > 0 ? options.FrameRate : reader.FrameRate;
            writer.WriteMetadata(outWidth, outHeight, codecType, outRate);

            // 4. 读取并转码帧
            var frameCount = 0;
            var totalFrames = reader.TotalFrames > 0 ? reader.TotalFrames : 1;

            // 创建编码器
            var encoder = CreateEncoder(codecType, outWidth, outHeight, options);

            while (true)
            {
                var packet = reader.ReadFrame();
                if (packet == null) break;

                // 解码
                var frames = encoder.Decode(packet);

                if (frameCount == 0)
                {
                    // 记录输入的编解码器类型
                    codecType = packet.CodecType;
                }

                if (frames != null && frames.Length > 0)
                {
                    // 帧处理（缩放/裁剪等）
                    if (options.Width > 0 && options.Height > 0 &&
                        (frames[0].Width != options.Width || frames[0].Height != options.Height))
                    {
                        var resizer = new FrameResizer(options.Width, options.Height);
                        for (var i = 0; i < frames.Length; i++)
                            frames[i] = resizer.ProcessFrame(frames[i]);
                    }

                    // 重新编码
                    var encoded = encoder.Encode(frames);
                    if (encoded?.Data != null && encoded.Data.Total > 0)
                    {
                        writer.WriteFrame(encoded);
                        frameCount++;
                    }
                }

                // 进度回调
                if (totalFrames > 0 && frameCount % Math.Max(1, totalFrames / 100) == 0)
                {
                    progress?.Invoke(Math.Min(99, (Int32)((Double)frameCount / totalFrames * 100)));
                }
            }

            writer.Flush();
            progress?.Invoke(100);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>异步一键转码</summary>
    /// <param name="inputPath">输入文件路径</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="options">转码选项</param>
    /// <param name="progress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    public static Task<Boolean> TranscodeAsync(String inputPath, String outputPath, TranscodeOptions options = null, Action<Int32> progress = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Transcode(inputPath, outputPath, options, progress), cancellationToken);
    }

    /// <summary>根据选项创建编解码器实例</summary>
    private static IVideoCodec CreateEncoder(VideoTypes codecType, Int32 width, Int32 height, TranscodeOptions options)
    {
        var bitrate = options.Bitrate > 0 ? options.Bitrate : 2000;
        var frameRate = options.FrameRate > 0 ? options.FrameRate : 25;
        var encoderName = options.EncoderName;
        var extraArgs = options.ExtraArgs;

        return codecType switch
        {
            VideoTypes.H264 => new FFmpegH264Encoder(width, height, bitrate, frameRate, null, encoderName, extraArgs),
            VideoTypes.H265 => new FFmpegH265Encoder(width, height, bitrate, frameRate, null, encoderName, extraArgs),
            _ => new FFmpegH264Encoder(width, height, bitrate, frameRate, null, encoderName, extraArgs),
        };
    }
}

/// <summary>转码选项</summary>
public class TranscodeOptions
{
    /// <summary>输出视频宽度（0 表示保持原始宽度）</summary>
    public Int32 Width { get; set; }

    /// <summary>输出视频高度（0 表示保持原始高度）</summary>
    public Int32 Height { get; set; }

    /// <summary>输出视频码率（kbps，0 表示默认 2000）</summary>
    public Int32 Bitrate { get; set; }

    /// <summary>输出帧率（0 表示保持原始帧率）</summary>
    public Int32 FrameRate { get; set; }

    /// <summary>编码器名称（如 libx264/h264_nvenc/hevc_nvenc 等，null 表示自动选择）</summary>
    public String EncoderName { get; set; }

    /// <summary>额外 FFmpeg 编码参数，如 "-preset medium -profile:v high"</summary>
    public String ExtraArgs { get; set; }
}
