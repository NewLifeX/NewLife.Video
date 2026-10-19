using NewLife.Video.Codecs;

namespace NewLife.Video.Processing;

/// <summary>帧序列生成视频。将图像序列或 VideoFrame 序列编码为视频文件</summary>
public class FrameSequenceEncoder
{
    private readonly Int32 _width;
    private readonly Int32 _height;
    private readonly Double _frameRate;
    private readonly VideoTypes _codecType;

    /// <summary>创建帧序列编码器</summary>
    /// <param name="width">视频宽度</param>
    /// <param name="height">视频高度</param>
    /// <param name="frameRate">输出帧率</param>
    /// <param name="codecType">目标编码类型</param>
    public FrameSequenceEncoder(Int32 width, Int32 height, Double frameRate = 25.0, VideoTypes codecType = VideoTypes.H264)
    {
        _width = width;
        _height = height;
        _frameRate = frameRate;
        _codecType = codecType;
    }

    /// <summary>将 VideoFrame 序列编码为视频数据包</summary>
    /// <param name="frames">帧序列</param>
    /// <returns>编码后的视频数据包</returns>
    public VPacket Encode(IEnumerable<VideoFrame> frames)
    {
        if (frames == null) throw new ArgumentNullException(nameof(frames));

        var frameList = frames.ToList();
        if (frameList.Count == 0) return new VPacket();

        // 确保所有帧尺寸匹配
        var resizer = new FrameResizer(_width, _height);
        var yuvFrames = new List<VideoFrame>();

        foreach (var f in frameList)
        {
            var resized = resizer.ProcessFrame(f);

            // 转为 YUV420P（编码器要求）
            Byte[] yuvData;
            if (resized.PixelFormat == VideoTypes.YUV420P)
            {
                yuvData = resized.Data;
            }
            else if (resized.PixelFormat == VideoTypes.RGB24)
            {
                var (y, u, v) = Converters.ColorSpaceConverter.RGB24ToYUV420P(resized.Data, resized.Width, resized.Height);
                var frame = VideoFrame.Create(_width, _height, VideoTypes.YUV420P);
                Array.Copy(y, 0, frame.Data, frame.YOffset, y.Length);
                Array.Copy(u, 0, frame.Data, frame.UOffset, u.Length);
                Array.Copy(v, 0, frame.Data, frame.VOffset, v.Length);
                yuvData = frame.Data;
            }
            else
            {
                continue;
            }

            var yuvFrame = VideoFrame.Create(_width, _height, VideoTypes.YUV420P);
            Array.Copy(yuvData, yuvFrame.Data, Math.Min(yuvData.Length, yuvFrame.Data.Length));
            yuvFrame.Timestamp = f.Timestamp;
            yuvFrame.FrameIndex = f.FrameIndex;
            yuvFrames.Add(yuvFrame);
        }

        if (yuvFrames.Count == 0) return new VPacket();

        // 调用相应编码器
        IVideoCodec encoder = _codecType switch
        {
            VideoTypes.H264 => new FFmpegH264Encoder(_width, _height, frameRate: (Int32)_frameRate),
            VideoTypes.H265 => new FFmpegH265Encoder(_width, _height, frameRate: (Int32)_frameRate),
            VideoTypes.MJPEG => new MjpegCodec(),
            _ => throw new NotSupportedException(L.T("Unsupported codec type: {0}.", _codecType)),
        };

        var packet = encoder.Encode(yuvFrames.ToArray());

        if (encoder is IDisposable d) d.Dispose();

        return packet;
    }

    /// <summary>从图像文件路径序列生成视频</summary>
    /// <param name="imagePaths">图像文件路径序列</param>
    /// <returns>编码后的视频数据包</returns>
    public VPacket EncodeFromFiles(IEnumerable<String> imagePaths)
    {
        var paths = imagePaths.ToList();
        var frames = new List<VideoFrame>();
        var frameIdx = 0L;

        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;

            var imgData = File.ReadAllBytes(path);
            var frame = new VideoFrame
            {
                Width = _width,
                Height = _height,
                PixelFormat = VideoTypes.RGB24,
                Data = imgData,
                Timestamp = (Int64)(frameIdx / _frameRate * 1000),
                FrameIndex = frameIdx,
            };
            frames.Add(frame);
            frameIdx++;
        }

        return Encode(frames);
    }
}
