namespace NewLife.Video;

/// <summary>视频帧（解码后的原始像素数据）</summary>
public class VideoFrame
{
    /// <summary>宽度（像素）</summary>
    public Int32 Width { get; set; }

    /// <summary>高度（像素）</summary>
    public Int32 Height { get; set; }

    /// <summary>像素格式</summary>
    public VideoTypes PixelFormat { get; set; }

    /// <summary>帧数据（原始像素缓冲区）</summary>
    public Byte[] Data { get; set; }

    /// <summary>Y 平面偏移（仅平面格式有效）</summary>
    public Int32 YOffset { get; set; }

    /// <summary>U 平面偏移（仅平面格式有效）</summary>
    public Int32 UOffset { get; set; }

    /// <summary>V 平面偏移（仅平面格式有效）</summary>
    public Int32 VOffset { get; set; }

    /// <summary>Y 行跨度（字节）</summary>
    public Int32 YStride { get; set; }

    /// <summary>UV 行跨度（字节）</summary>
    public Int32 UVStride { get; set; }

    /// <summary>显示时间戳（毫秒）</summary>
    public Int64 Timestamp { get; set; }

    /// <summary>是否为关键帧</summary>
    public Boolean IsKeyFrame { get; set; }

    /// <summary>帧序号</summary>
    public Int64 FrameIndex { get; set; }

    /// <summary>获取 Y 平面数据</summary>
    /// <returns>Y 平面 Span</returns>
    public Span<Byte> GetYPlane() => Data.AsSpan(YOffset, Width * Height);

    /// <summary>获取 U 平面数据</summary>
    /// <param name="chromaWidth">色度宽度（默认 Width/2）</param>
    /// <param name="chromaHeight">色度高度（默认 Height/2）</param>
    /// <returns>U 平面 Span</returns>
    public Span<Byte> GetUPlane(Int32 chromaWidth = 0, Int32 chromaHeight = 0)
    {
        if (chromaWidth <= 0) chromaWidth = Width / 2;
        if (chromaHeight <= 0) chromaHeight = Height / 2;
        return Data.AsSpan(UOffset, chromaWidth * chromaHeight);
    }

    /// <summary>获取 V 平面数据</summary>
    /// <param name="chromaWidth">色度宽度（默认 Width/2）</param>
    /// <param name="chromaHeight">色度高度（默认 Height/2）</param>
    /// <returns>V 平面 Span</returns>
    public Span<Byte> GetVPlane(Int32 chromaWidth = 0, Int32 chromaHeight = 0)
    {
        if (chromaWidth <= 0) chromaWidth = Width / 2;
        if (chromaHeight <= 0) chromaHeight = Height / 2;
        return Data.AsSpan(VOffset, chromaWidth * chromaHeight);
    }

    /// <summary>创建指定尺寸和格式的空帧</summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="pixelFormat">像素格式</param>
    /// <returns>空视频帧</returns>
    public static VideoFrame Create(Int32 width, Int32 height, VideoTypes pixelFormat)
    {
        var frame = new VideoFrame
        {
            Width = width,
            Height = height,
            PixelFormat = pixelFormat,
        };

        var dataSize = CalculateDataSize(width, height, pixelFormat);
        frame.Data = new Byte[dataSize];

        // 计算平面偏移
        switch (pixelFormat)
        {
            case VideoTypes.YUV420P:
                frame.YOffset = 0;
                frame.UOffset = width * height;
                frame.VOffset = frame.UOffset + (width / 2) * (height / 2);
                frame.YStride = width;
                frame.UVStride = width / 2;
                break;
            case VideoTypes.NV12:
            case VideoTypes.NV21:
                frame.YOffset = 0;
                frame.UOffset = width * height;
                frame.VOffset = frame.UOffset; // NV12 共用 UV 平面
                frame.YStride = width;
                frame.UVStride = width;
                break;
            default:
                frame.YStride = width;
                frame.UVStride = width / 2;
                break;
        }

        return frame;
    }

    /// <summary>计算指定格式所需的字节数</summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="pixelFormat">像素格式</param>
    /// <returns>字节数</returns>
    public static Int32 CalculateDataSize(Int32 width, Int32 height, VideoTypes pixelFormat)
    {
        return pixelFormat switch
        {
            VideoTypes.YUV420P or VideoTypes.NV12 or VideoTypes.NV21 => width * height + 2 * (width / 2) * (height / 2),
            VideoTypes.YUV422P => width * height + 2 * (width / 2) * height,
            VideoTypes.YUV444P => width * height * 3,
            VideoTypes.RGB24 or VideoTypes.BGR24 => width * height * 3,
            VideoTypes.BGRA32 or VideoTypes.RGBA32 => width * height * 4,
            VideoTypes.GRAY8 => width * height,
            _ => width * height * 3, // 默认按 RGB 计算
        };
    }
}
