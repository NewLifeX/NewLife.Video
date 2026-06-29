using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>AVI (RIFF) 容器写入器。生成标准 AVI 文件结构</summary>
/// <remarks>
/// RIFF('AVI ' → LIST('hdrl' → 'avih' + LIST('strl' → 'strh' + 'strf')) → LIST('movi' → '00dc'...) → 'idx1')
/// 内存缓冲帧数据，Dispose 时一次性写入完整文件。
/// </remarks>
public class AviWriter : IVideoContainerWriter
{
    private readonly String _filePath;
    private readonly List<Byte[]> _frames = [];
    private Int32 _width;
    private Int32 _height;
    private VideoTypes _codecType;
    private Double _frameRate;
    private Int32 _maxFrameSize;
    private Boolean _disposed;

    public AviWriter(String filePath) => _filePath = filePath;

    public void WriteMetadata(Int32 width, Int32 height, VideoTypes codecType, Double frameRate)
    {
        _width = width;
        _height = height;
        _codecType = codecType;
        _frameRate = frameRate;
    }

    public void WriteFrame(VPacket packet)
    {
        if (packet?.Data == null) return;
        var data = packet.Data.ReadBytes();
        _frames.Add(data);
        if (data.Length > _maxFrameSize) _maxFrameSize = data.Length;
    }

    public void Flush() { }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        WriteFile();
    }

    private void WriteFile()
    {
        using var fs = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        var riffStart = fs.Position;

        // RIFF 头占位
        fs.WriteByte((Byte)'R'); fs.WriteByte((Byte)'I'); fs.WriteByte((Byte)'F'); fs.WriteByte((Byte)'F');
        WriteLE(fs, 0u); // 文件大小占位
        fs.WriteByte((Byte)'A'); fs.WriteByte((Byte)'V'); fs.WriteByte((Byte)'I'); fs.WriteByte((Byte)' ');

        WriteHdrlList(fs);
        WriteMoviList(fs);
        WriteIdx1(fs);

        // 回填 RIFF 大小
        var end = fs.Position;
        fs.Seek(riffStart + 4, SeekOrigin.Begin);
        WriteLE(fs, (UInt32)(end - riffStart - 8));
    }

    #region hdrl

    private void WriteHdrlList(FileStream fs)
    {
        // LIST 'hdrl'
        var listStart = fs.Position;
        fs.WriteByte((Byte)'L'); fs.WriteByte((Byte)'I'); fs.WriteByte((Byte)'S'); fs.WriteByte((Byte)'T');
        WriteLE(fs, 0u); // 大小占位
        fs.WriteByte((Byte)'h'); fs.WriteByte((Byte)'d'); fs.WriteByte((Byte)'r'); fs.WriteByte((Byte)'l');

        WriteAvih(fs);
        WriteStrlList(fs);

        // 回填 LIST 大小
        var end = fs.Position;
        fs.Seek(listStart + 4, SeekOrigin.Begin);
        WriteLE(fs, (UInt32)(end - listStart - 8));
        fs.Seek(end, SeekOrigin.Begin);
    }

    private void WriteAvih(FileStream fs)
    {
        var start = fs.Position;
        fs.WriteByte((Byte)'a'); fs.WriteByte((Byte)'v'); fs.WriteByte((Byte)'i'); fs.WriteByte((Byte)'h');
        WriteLE(fs, 56u); // avih 固定 56 字节

        var scale = _frameRate > 0 ? (UInt32)(1_000_000 / _frameRate) : 40_000u;
        WriteLE(fs, scale);                    // dwMicroSecPerFrame
        var bytesPerSec = _frameRate > 0 ? (UInt32)(_maxFrameSize * _frameRate) : (UInt32)_maxFrameSize;
        WriteLE(fs, bytesPerSec);              // dwMaxBytesPerSec
        WriteLE(fs, 0u);                       // dwPaddingGranularity
        WriteLE(fs, 0x00000810u);              // dwFlags: AVIF_HASINDEX | AVIF_MUSTUSEINDEX
        WriteLE(fs, (UInt32)_frames.Count);    // dwTotalFrames
        WriteLE(fs, 0u);                       // dwInitialFrames
        WriteLE(fs, 1u);                       // dwStreams
        WriteLE(fs, (UInt32)_maxFrameSize);    // dwSuggestedBufferSize
        WriteLE(fs, (UInt32)_width);           // dwWidth
        WriteLE(fs, (UInt32)_height);          // dwHeight
        WriteLE(fs, 0u); WriteLE(fs, 0u); WriteLE(fs, 0u); WriteLE(fs, 0u); // dwReserved[4]
    }

    private void WriteStrlList(FileStream fs)
    {
        var listStart = fs.Position;
        fs.WriteByte((Byte)'L'); fs.WriteByte((Byte)'I'); fs.WriteByte((Byte)'S'); fs.WriteByte((Byte)'T');
        WriteLE(fs, 0u);
        fs.WriteByte((Byte)'s'); fs.WriteByte((Byte)'t'); fs.WriteByte((Byte)'r'); fs.WriteByte((Byte)'l');

        WriteStrh(fs);
        WriteStrf(fs);

        var end = fs.Position;
        fs.Seek(listStart + 4, SeekOrigin.Begin);
        WriteLE(fs, (UInt32)(end - listStart - 8));
        fs.Seek(end, SeekOrigin.Begin);
    }

    private void WriteStrh(FileStream fs)
    {
        fs.WriteByte((Byte)'s'); fs.WriteByte((Byte)'t'); fs.WriteByte((Byte)'r'); fs.WriteByte((Byte)'h');
        WriteLE(fs, 64u); // strh 固定 64 字节（含 16 字节 rcFrame）

        WriteFourCC(fs, "vids");               // fccType
        var fcc = GetCodecFourCC();
        WriteFourCC(fs, fcc);                  // fccHandler
        WriteLE(fs, 0u);                       // dwFlags
        WriteLE(fs, (UInt16)0);                // wPriority
        WriteLE(fs, (UInt16)0);                // wLanguage
        WriteLE(fs, 0u);                       // dwInitialFrames
        WriteLE(fs, scale());                  // dwScale
        var dwRate = _frameRate > 0 ? (UInt32)(1_000_000) : 1_000_000u;
        WriteLE(fs, dwRate);                   // dwRate
        WriteLE(fs, 0u);                       // dwStart
        WriteLE(fs, (UInt32)_frames.Count);    // dwLength
        WriteLE(fs, (UInt32)_maxFrameSize);    // dwSuggestedBufferSize
        WriteLE(fs, 0xFFFFFFFFu);              // dwQuality (-1 = default)
        WriteLE(fs, 0u);                       // dwSampleSize
        // rcFrame: left, top, right, bottom (16 bytes)
        WriteLE(fs, (UInt16)0);                // left
        WriteLE(fs, (UInt16)0);                // top
        WriteLE(fs, (UInt16)_width);           // right
        WriteLE(fs, (UInt16)_height);          // bottom
    }

    private void WriteStrf(FileStream fs)
    {
        fs.WriteByte((Byte)'s'); fs.WriteByte((Byte)'t'); fs.WriteByte((Byte)'r'); fs.WriteByte((Byte)'f');

        if (_codecType == VideoTypes.MJPEG || _codecType == VideoTypes.H264 || _codecType == VideoTypes.H265)
        {
            // BITMAPINFOHEADER (40 bytes)
            WriteLE(fs, 40u);                      // biSize
            WriteLE(fs, (UInt32)_width);
            WriteLE(fs, (UInt32)_height);
            WriteLE(fs, (UInt16)1);                // biPlanes
            WriteLE(fs, (UInt16)24);               // biBitCount = 24
            var fcc = GetCodecFourCC();
            WriteFourCC(fs, fcc);
            WriteLE(fs, (UInt32)(_width * _height * 3)); // biSizeImage
            WriteLE(fs, 0u); WriteLE(fs, 0u);      // biXPelsPerMeter, biYPelsPerMeter
            WriteLE(fs, 0u);                        // biClrUsed
            WriteLE(fs, 0u);                        // biClrImportant
        }
        else
        {
            // 未压缩 RGB
            WriteLE(fs, 40u);
            WriteLE(fs, (UInt32)_width);
            WriteLE(fs, (UInt32)_height);
            WriteLE(fs, (UInt16)1);
            WriteLE(fs, (UInt16)24);
            WriteLE(fs, 0u); // BI_RGB
            WriteLE(fs, (UInt32)(_width * _height * 3));
            WriteLE(fs, 0u); WriteLE(fs, 0u);
            WriteLE(fs, 0u); WriteLE(fs, 0u);
        }
    }

    private String GetCodecFourCC()
    {
        return _codecType switch
        {
            VideoTypes.MJPEG => "MJPG",
            VideoTypes.H264 => "H264",
            VideoTypes.H265 => "HEVC",
            _ => "DIB ",
        };
    }

    private UInt32 scale() => _frameRate > 0 ? (UInt32)(1_000_000 / _frameRate) : 40_000u;

    #endregion

    #region movi + idx1

    private void WriteMoviList(FileStream fs)
    {
        var listStart = fs.Position;
        fs.WriteByte((Byte)'L'); fs.WriteByte((Byte)'I'); fs.WriteByte((Byte)'S'); fs.WriteByte((Byte)'T');
        WriteLE(fs, 0u);
        fs.WriteByte((Byte)'m'); fs.WriteByte((Byte)'o'); fs.WriteByte((Byte)'v'); fs.WriteByte((Byte)'i');

        foreach (var frame in _frames)
        {
            fs.WriteByte((Byte)'0'); fs.WriteByte((Byte)'0'); fs.WriteByte((Byte)'d'); fs.WriteByte((Byte)'c');
            WriteLE(fs, (UInt32)frame.Length);
            fs.Write(frame, 0, frame.Length);
            // 对齐到 WORD 边界
            if (frame.Length % 2 != 0) fs.WriteByte(0);
        }

        var end = fs.Position;
        fs.Seek(listStart + 4, SeekOrigin.Begin);
        WriteLE(fs, (UInt32)(end - listStart - 8));
        fs.Seek(end, SeekOrigin.Begin);
    }

    private void WriteIdx1(FileStream fs)
    {
        fs.WriteByte((Byte)'i'); fs.WriteByte((Byte)'d'); fs.WriteByte((Byte)'x'); fs.WriteByte((Byte)'1');
        WriteLE(fs, (UInt32)(_frames.Count * 16)); // 每条 16 字节

        UInt32 offset = 4; // movi 中第一个 00dc 的偏移从 LIST 'movi' 的 data 开始
        foreach (var frame in _frames)
        {
            // chunk id
            fs.WriteByte((Byte)'0'); fs.WriteByte((Byte)'0'); fs.WriteByte((Byte)'d'); fs.WriteByte((Byte)'c');
            WriteLE(fs, 0x10u); // flags: AVIIF_KEYFRAME
            WriteLE(fs, offset); // 偏移
            WriteLE(fs, (UInt32)frame.Length);
            offset += (UInt32)(8 + frame.Length + (frame.Length % 2));
        }
    }

    #endregion

    #region 工具

    private static void WriteLE(FileStream fs, UInt32 v)
    {
        fs.WriteByte((Byte)v);
        fs.WriteByte((Byte)(v >> 8));
        fs.WriteByte((Byte)(v >> 16));
        fs.WriteByte((Byte)(v >> 24));
    }

    private static void WriteLE(FileStream fs, UInt16 v)
    {
        fs.WriteByte((Byte)v);
        fs.WriteByte((Byte)(v >> 8));
    }

    private static void WriteFourCC(FileStream fs, String s)
    {
        for (var i = 0; i < 4; i++)
            fs.WriteByte(i < s.Length ? (Byte)s[i] : (Byte)' ');
    }

    #endregion
}

