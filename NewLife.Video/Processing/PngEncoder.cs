using NewLife.Data;
using System.IO.Compression;

namespace NewLife.Video.Processing;

/// <summary>纯托管 PNG 编码器。将 RGB24 帧数据编码为 PNG 格式，零外部依赖</summary>
/// <remarks>
/// 利用 .NET 内置 DeflateStream 实现压缩，自研 CRC32 查表算法兼容 .NET 4.5/netstandard2.0。
/// 支持 RGB24 输入，使用 Paeth 滤波优化压缩率。
/// </remarks>
public static class PngEncoder
{
    /// <summary>将 RGB24 像素数据编码为 PNG 文件字节数组</summary>
    /// <param name="rgbData">RGB24 像素数据（每像素 3 字节）</param>
    /// <param name="width">图像宽度（像素）</param>
    /// <param name="height">图像高度（像素）</param>
    /// <returns>PNG 文件字节数组</returns>
    public static Byte[] Encode(Byte[] rgbData, Int32 width, Int32 height)
    {
        if (rgbData == null) throw new ArgumentNullException(nameof(rgbData));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (rgbData.Length < width * height * 3) throw new ArgumentException(L.T("Insufficient data length."), nameof(rgbData));

        // 构建过滤后的行数据（每行 1 字节 filter + width*3 字节像素）
        var rawData = FilterRows(rgbData, width, height);

        // Deflate 压缩
        Byte[] compressedData;
        using (var ms = new MemoryStream())
        {
            using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, true))
            {
                deflate.Write(rawData, 0, rawData.Length);
            }
            compressedData = ms.ToArray();
        }

        using (var ms = new MemoryStream())
        {
            // PNG 签名
            ms.Write(PngSignature, 0, 8);

            // IHDR 块
            WriteChunk(ms, ChunkTypeIHDR, writer =>
            {
                WriteBE32(writer, width);
                WriteBE32(writer, height);
                writer.WriteByte(8);      // bit depth
                writer.WriteByte(2);      // color type: RGB
                writer.WriteByte(0);      // compression
                writer.WriteByte(0);      // filter
                writer.WriteByte(0);      // interlace
            });

            // IDAT 块
            WriteChunk(ms, ChunkTypeIDAT, writer =>
            {
                writer.Write(compressedData, 0, compressedData.Length);
            });

            // IEND 块
            WriteChunk(ms, ChunkTypeIEND, _ => { });

            return ms.ToArray();
        }
    }

    /// <summary>将 VideoFrame 编码为 PNG 字节数组</summary>
    /// <param name="frame">视频帧（需为 RGB24 格式）</param>
    /// <returns>PNG 字节数组</returns>
    public static Byte[] EncodeFrame(VideoFrame frame)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));

        // 确保数据为 RGB24
        var rgbData = EnsureRgb24(frame);
        return Encode(rgbData, frame.Width, frame.Height);
    }

    #region 辅助

    private static readonly Byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly Byte[] ChunkTypeIHDR = [73, 72, 68, 82]; // "IHDR"
    private static readonly Byte[] ChunkTypeIDAT = [73, 68, 65, 84]; // "IDAT"
    private static readonly Byte[] ChunkTypeIEND = [73, 69, 78, 68]; // "IEND"

    /// <summary>对每行应用 Paeth 滤波</summary>
    private static Byte[] FilterRows(Byte[] rgbData, Int32 width, Int32 height)
    {
        var rowSize = width * 3;
        var result = new Byte[(rowSize + 1) * height];
        var prevRow = new Byte[rowSize]; // 上一行（初始化为 0）

        for (var y = 0; y < height; y++)
        {
            var srcOffset = y * rowSize;
            var dstOffset = y * (rowSize + 1);
            result[dstOffset] = 4; // Paeth 滤波
            dstOffset++;

            for (var x = 0; x < rowSize; x++)
            {
                var raw = rgbData[srcOffset + x];
                var a = x >= 3 ? rgbData[srcOffset + x - 3] : (Byte)0;
                var b = prevRow[x];
                var c = x >= 3 ? prevRow[x - 3] : (Byte)0;
                var p = raw - PaethPredictor(a, b, c);
                result[dstOffset + x] = (Byte)(p & 0xFF);
            }

            // 保存当前行作为下一行的上一行
            Array.Copy(rgbData, srcOffset, prevRow, 0, rowSize);
        }

        return result;
    }

    private static Byte PaethPredictor(Int32 a, Int32 b, Int32 c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return (Byte)a;
        if (pb <= pc) return (Byte)b;
        return (Byte)c;
    }

    /// <summary>写入 PNG 块（Length + Type + Data + CRC）</summary>
    private static void WriteChunk(MemoryStream ms, Byte[] chunkType, Action<MemoryStream> writeData)
    {
        // 先写入临时流计算 CRC
        using (var dataMs = new MemoryStream())
        {
            dataMs.Write(chunkType, 0, 4);
            writeData(dataMs);

            var dataBytes = dataMs.ToArray();
            var dataLen = dataBytes.Length - 4; // 减去 type 的 4 字节

            // Length (4 bytes big-endian)
            WriteBE32(ms, dataLen);

            // Type + Data
            ms.Write(dataBytes, 0, dataBytes.Length);

            // CRC32 (Type + Data)
            var crc = Crc32(dataBytes, 0, dataBytes.Length);
            WriteBE32(ms, crc);
        }
    }

    #endregion

    #region CRC32

    private static UInt32[] _crcTable;

    private static UInt32[] GetCrcTable()
    {
        if (_crcTable != null) return _crcTable;

        var table = new UInt32[256];
        for (var n = 0; n < 256; n++)
        {
            var c = (UInt32)n;
            for (var k = 0; k < 8; k++)
            {
                if ((c & 1) == 1)
                    c = 0xEDB88320 ^ (c >> 1);
                else
                    c >>= 1;
            }
            table[n] = c;
        }
        _crcTable = table;
        return table;
    }

    private static UInt32 Crc32(Byte[] data, Int32 offset, Int32 length)
    {
        var table = GetCrcTable();
        var crc = 0xFFFFFFFFu;
        var end = offset + length;
        for (var i = offset; i < end; i++)
        {
            crc = table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFFu;
    }

    #endregion

    #region 二进制写入

    private static void WriteBE32(MemoryStream ms, Int32 value)
    {
        ms.WriteByte((Byte)((value >> 24) & 0xFF));
        ms.WriteByte((Byte)((value >> 16) & 0xFF));
        ms.WriteByte((Byte)((value >> 8) & 0xFF));
        ms.WriteByte((Byte)(value & 0xFF));
    }

    private static void WriteBE32(MemoryStream ms, UInt32 value)
    {
        ms.WriteByte((Byte)((value >> 24) & 0xFF));
        ms.WriteByte((Byte)((value >> 16) & 0xFF));
        ms.WriteByte((Byte)((value >> 8) & 0xFF));
        ms.WriteByte((Byte)(value & 0xFF));
    }

    #endregion

    #region 像素格式转换

    private static Byte[] EnsureRgb24(VideoFrame frame)
    {
        if (frame.PixelFormat == VideoTypes.RGB24)
        {
            var data = new Byte[frame.Data.Length];
            Array.Copy(frame.Data, data, frame.Data.Length);
            return data;
        }

        // 通过 PixelFormatConverter 转为 RGB24
        var rgb = frame.PixelFormat switch
        {
            VideoTypes.YUV420P => PixelFormatConverter.YUV420PToRGB24(frame.Data, frame.Width, frame.Height),
            VideoTypes.NV12 => PixelFormatConverter.NV12ToRGB24(frame.Data, frame.Width, frame.Height),
            VideoTypes.NV21 => PixelFormatConverter.NV21ToRGB24(frame.Data, frame.Width, frame.Height),
            VideoTypes.BGRA32 => PixelFormatConverter.BGRA32ToRGB24(frame.Data, frame.Width, frame.Height),
            _ => PixelFormatConverter.YUV420PToRGB24(frame.Data, frame.Width, frame.Height),
        };
        return rgb ?? [];
    }

    #endregion
}
