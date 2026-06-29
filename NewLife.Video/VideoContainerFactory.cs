using NewLife.Video.Containers;

namespace NewLife.Video;

/// <summary>视频容器工厂。根据文件扩展名和魔术字节自动选择读取器</summary>
/// <remarks>
/// 路由优先级：扩展名 → 魔术字节 → 回退 RawVideoReader。
/// 当前支持的格式：原始视频帧 (.yuv/.nv12/.rgb/.raw)、MP4 (.mp4)、FLV (.flv)。
/// </remarks>
public static class VideoContainerFactory
{
    /// <summary>创建视频容器读取器</summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>容器读取器</returns>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    /// <exception cref="NotSupportedException">格式不支持时抛出</exception>
    public static IVideoContainerReader CreateReader(String filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("视频文件不存在", filePath);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        // 按扩展名路由
        return ext switch
        {
            ".yuv" or ".nv12" or ".rgb" or ".raw" => CreateRawReader(filePath, ext),
            ".mp4" or ".m4v" => new Mp4Reader(filePath),
            ".flv" => new FlvReader(filePath),
            ".avi" => throw new NotSupportedException("AVI 容器读取器尚未实现"),
            ".ts" or ".mts" => throw new NotSupportedException("TS 容器读取器尚未实现"),
            ".mkv" or ".webm" => throw new NotSupportedException("MKV/WebM 容器读取器尚未实现"),
            _ => TryDetectByMagic(filePath),
        };
    }

    /// <summary>创建原始视频帧读取器</summary>
    private static IVideoContainerReader CreateRawReader(String filePath, String ext)
    {
        var pixelFormat = ext switch
        {
            ".nv12" => VideoTypes.NV12,
            ".rgb" => VideoTypes.RGB24,
            _ => VideoTypes.YUV420P,
        };

        // 根据文件大小推测分辨率（常见分辨率）
        var fileSize = new FileInfo(filePath).Length;
        var (width, height) = InferResolution(fileSize, pixelFormat);

        return new RawVideoReader(filePath, width, height, pixelFormat);
    }

    /// <summary>通过魔术字节检测格式</summary>
    private static IVideoContainerReader TryDetectByMagic(String filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var magic = new Byte[8];
        var read = fs.Read(magic, 0, magic.Length);

        if (read >= 4)
        {
            // MP4: ftyp 盒通常从偏移 4 开始
            if (read >= 8 && magic[4] == 'f' && magic[5] == 't' && magic[6] == 'y' && magic[7] == 'p')
                throw new NotSupportedException("MP4 容器读取器尚未实现");

            // FLV: "FLV" 魔术字节
            if (magic[0] == 'F' && magic[1] == 'L' && magic[2] == 'V')
                throw new NotSupportedException("FLV 容器读取器尚未实现");

            // RIFF (AVI)
            if (magic[0] == 'R' && magic[1] == 'I' && magic[2] == 'F' && magic[3] == 'F')
                throw new NotSupportedException("AVI 容器读取器尚未实现");
        }

        // 回退：当作原始 YUV420P 数据
        var fileSize = new FileInfo(filePath).Length;
        var (width, height) = InferResolution(fileSize, VideoTypes.YUV420P);
        return new RawVideoReader(filePath, width, height, VideoTypes.YUV420P);
    }

    /// <summary>根据文件大小推测视频分辨率</summary>
    private static (Int32 Width, Int32 Height) InferResolution(Int64 fileSize, VideoTypes pixelFormat)
    {
        var bytesPerFrame = VideoFrame.CalculateDataSize(1, 1, pixelFormat);
        var totalPixels = fileSize / bytesPerFrame;

        // 常见分辨率匹配
        var candidates = new (Int32 W, Int32 H)[]
        {
            (3840, 2160), // 4K
            (2560, 1440), // 2K
            (1920, 1080), // 1080p
            (1280, 720),  // 720p
            (704, 576),   // D1 PAL
            (640, 480),   // VGA
            (352, 288),   // CIF
            (320, 240),   // QVGA
        };

        foreach (var (w, h) in candidates)
        {
            if (w * h <= totalPixels)
                return (w, h);
        }

        // 默认 640x480
        return (640, 480);
    }
}
