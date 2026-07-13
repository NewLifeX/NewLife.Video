using NewLife.Video.Codecs;
using NewLife.Video.Containers;

namespace NewLife.Video.Processing;

/// <summary>视频转码器。提供一键式转码管线，内部编排容器读取→解码→帧处理→编码→容器写入</summary>
public static class VideoTranscoder
{
    public static Boolean Transcode(String inputPath, String outputPath, TranscodeOptions options = null, Action<Int32> progress = null)
    {
        if (String.IsNullOrEmpty(inputPath)) throw new ArgumentNullException(nameof(inputPath));
        if (String.IsNullOrEmpty(outputPath)) throw new ArgumentNullException(nameof(outputPath));
        if (!File.Exists(inputPath)) throw new FileNotFoundException("输入文件不存在", inputPath);

        options ??= new TranscodeOptions();
        progress?.Invoke(0);

        using var reader = VideoContainerFactory.CreateReader(inputPath);
        using var writer = VideoContainerFactory.CreateWriter(outputPath);

        try
        {
            if (reader == null) throw new NotSupportedException($"不支持的输入格式：{Path.GetExtension(inputPath)}");

            var codecType = reader.VideoCodecType;
            if (options.EncoderName != null)
            {
                codecType = options.EncoderName.Contains("265") || options.EncoderName.Contains("hevc") ?
                    VideoTypes.H265 : VideoTypes.H264;
            }

            var outWidth = options.Width > 0 ? options.Width : reader.VideoWidth;
            var outHeight = options.Height > 0 ? options.Height : reader.VideoHeight;
            var outRate = options.FrameRate > 0 ? options.FrameRate : reader.FrameRate;
            writer.WriteMetadata(outWidth, outHeight, codecType, outRate);

            var frameCount = 0;
            var totalFrames = reader.TotalFrames > 0 ? reader.TotalFrames : 1;

            var decoder = CreateDecoder(reader.VideoCodecType);
            var encoder = CreateEncoder(codecType, outWidth, outHeight, options);

            while (true)
            {
                var packet = reader.ReadFrame();
                if (packet == null) break;

                VideoFrame[] frames = null;
                if (decoder != null)
                {
                    frames = decoder.Decode(packet);
                }
                else
                {
                    // MJPEG 等纯托管格式直接使用帧数据
                    var rawData = packet.Data.TryGetArray(out var seg) && seg.Offset == 0
                        ? seg.Array : GetBytes(packet.Data);
                    frames = new VideoFrame[]
                    {
                        new VideoFrame
                        {
                            Data = rawData,
                            Width = reader.VideoWidth,
                            Height = reader.VideoHeight,
                            PixelFormat = VideoTypes.RGB24,
                        }
                    };
                }
                if (frames == null || frames.Length == 0) continue;

                if (frameCount == 0) codecType = packet.CodecType;

                if (options.Width > 0 && options.Height > 0 &&
                    (frames[0].Width != options.Width || frames[0].Height != options.Height))
                {
                    var resizer = new FrameResizer(options.Width, options.Height);
                    for (var i = 0; i < frames.Length; i++)
                        frames[i] = resizer.ProcessFrame(frames[i]);
                }

                var encoded = encoder.Encode(frames);
                if (encoded?.Data != null && encoded.Data.Total > 0)
                {
                    writer.WriteFrame(encoded);
                    frameCount++;
                }

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

    public static Task<Boolean> TranscodeAsync(String inputPath, String outputPath, TranscodeOptions options = null, Action<Int32> progress = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Transcode(inputPath, outputPath, options, progress), cancellationToken);
    }

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

    private static IVideoCodec CreateDecoder(VideoTypes codecType)
    {
        return codecType switch
        {
            VideoTypes.H264 => new FFmpegH264Decoder(),
            VideoTypes.H265 => new FFmpegH265Decoder(),
            VideoTypes.VP8 => new FFmpegVP8Decoder(),
            VideoTypes.VP9 => new FFmpegVP9Decoder(),
            _ => null,
        };
    }

    private static Byte[] GetBytes(NewLife.Data.IPacket packet)
    {
        if (packet.TryGetArray(out var seg))
        {
            var data = new Byte[seg.Count];
            Array.Copy(seg.Array, seg.Offset, data, 0, seg.Count);
            return data;
        }
        return [];
    }
}

/// <summary>转码选项</summary>
public class TranscodeOptions
{
    public Int32 Width { get; set; }
    public Int32 Height { get; set; }
    public Int32 Bitrate { get; set; }
    public Int32 FrameRate { get; set; }
    public String EncoderName { get; set; }
    public String ExtraArgs { get; set; }
}
