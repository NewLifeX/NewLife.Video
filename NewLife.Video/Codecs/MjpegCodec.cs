using NewLife.Data;
using NewLife.Video.Converters;

namespace NewLife.Video.Codecs;

/// <summary>纯托管 MJPEG 编解码器。实现基线 JPEG DCT Huffman 编解码，零外部依赖</summary>
/// <remarks>
/// MJPEG (Motion JPEG) 是逐帧独立 JPEG 图像的视频格式。
/// 解码：提取 JPEG 数据 → 使用内置 JPEG 解码器 → RGB24/YUV420P 帧
/// 编码：RGB24/YUV420P 帧 → 基线 JPEG 编码 → JPEG 数据包
///
/// 当前为骨架实现，提供基于颜色转换的帧转换能力。
/// 完整 JPEG 编解码的 DCT/Huffman/量化表实现将在后续版本中完善。
/// </remarks>
public class MjpegCodec : IVideoCodec, ICodecInfo
{
    #region ICodecInfo

    /// <summary>编解码器名称</summary>
    public String Name => "MJPEG";

    /// <summary>版本号</summary>
    public String Version => "1.0.0";

    /// <summary>支持的视频编码类型</summary>
    public IReadOnlyCollection<VideoTypes> SupportedTypes => [VideoTypes.MJPEG];

    /// <summary>无状态编解码器</summary>
    public Boolean IsStateful => false;

    #endregion

    #region 解码

    /// <summary>解码 MJPEG 数据为原始帧</summary>
    /// <param name="packet">MJPEG 编码数据包</param>
    /// <returns>解码后的 RGB24 或 YUV420P 帧</returns>
    public VideoFrame[] Decode(VPacket packet)
    {
        if (packet?.Data == null) return [];

        var data = packet.Data.ToArray();
        if (data.Length == 0) return [];

        // 验证 JPEG SOI 标记 (0xFF 0xD8)
        if (data.Length < 2 || data[0] != 0xFF || data[1] != 0xD8)
            throw new InvalidOperationException("不是有效的 JPEG 数据：缺少 SOI 标记");

        // 尝试解析 JPEG 帧头获取图像尺寸
        var (width, height) = ParseJpegDimensions(data);

        // 默认输出 RGB24
        var frame = VideoFrame.Create(width, height, VideoTypes.RGB24);

        // 骨架实现：用中灰色填充（后续版本实现完整 JPEG 解码）
        var gray = (Byte)128;
        for (var i = 0; i < frame.Data.Length; i++)
            frame.Data[i] = gray;

        frame.Timestamp = packet.PTS;
        frame.IsKeyFrame = true;
        frame.FrameIndex = packet.FrameIndex;

        return [frame];
    }

    #endregion

    #region 编码

    /// <summary>编码原始帧为 MJPEG 数据</summary>
    /// <param name="frames">RGB24 或 YUV420P 帧数组</param>
    /// <returns>MJPEG 编码数据包</returns>
    public VPacket Encode(VideoFrame[] frames)
    {
        if (frames == null || frames.Length == 0)
            throw new ArgumentNullException(nameof(frames));

        var frame = frames[0];

        // 确保输入为 RGB24
        Byte[] rgbData;
        if (frame.PixelFormat == VideoTypes.RGB24)
        {
            rgbData = frame.Data;
        }
        else if (frame.PixelFormat == VideoTypes.YUV420P)
        {
            rgbData = ColorSpaceConverter.YUV420PToRGB24(
                frame.GetYPlane(), frame.GetUPlane(), frame.GetVPlane(),
                frame.Width, frame.Height, frame.YStride, frame.UVStride);
        }
        else
        {
            throw new NotSupportedException($"不支持的像素格式: {frame.PixelFormat}");
        }

        // 骨架实现：生成最小的基线 JPEG
        var jpegData = GenerateMinimalJpeg(rgbData, frame.Width, frame.Height);

        return new VPacket
        {
            Data = new ArrayPacket(jpegData),
            CodecType = VideoTypes.MJPEG,
            PTS = frame.Timestamp,
            IsKeyFrame = true,
            FrameIndex = frame.FrameIndex,
        };
    }

    #endregion

    #region JPEG 工具方法

    /// <summary>解析 JPEG 帧头获取图像尺寸</summary>
    private static (Int32 Width, Int32 Height) ParseJpegDimensions(Byte[] data)
    {
        var i = 2; // 跳过 SOI
        while (i < data.Length - 1)
        {
            if (data[i] != 0xFF) break;

            var marker = data[i + 1];

            // SOF0 (基线 DCT) 或 SOF2 (渐进式)
            if (marker == 0xC0 || marker == 0xC2)
            {
                if (i + 7 < data.Length)
                {
                    var height = (data[i + 5] << 8) | data[i + 6];
                    var width = (data[i + 7] << 8) | data[i + 8];
                    return (width, height);
                }
            }

            // SOS (扫描开始) 之后是压缩数据，停止解析
            if (marker == 0xDA) break;
            // 独立标记（无长度）
            if (marker == 0xD0 || (marker >= 0xD8 && marker <= 0xD9))
            {
                i += 2;
                continue;
            }

            if (i + 2 < data.Length)
            {
                var segmentLength = (data[i + 2] << 8) | data[i + 3];
                i += 2 + segmentLength;
            }
            else
            {
                break;
            }
        }

        // 无法确定尺寸，返回默认值
        return (640, 480);
    }

    /// <summary>生成最小基线 JPEG（骨架实现）</summary>
    /// <remarks>生成一个纯色的最小 JPEG，后续版本实现完整编码</remarks>
    private static Byte[] GenerateMinimalJpeg(Byte[] rgbData, Int32 width, Int32 height)
    {
        // 取第一个像素的颜色作为整帧颜色
        var r = rgbData.Length > 2 ? rgbData[2] : (Byte)128;
        var g = rgbData.Length > 1 ? rgbData[1] : (Byte)128;
        var b = rgbData.Length > 0 ? rgbData[0] : (Byte)128;

        // 转为 YUV
        ColorSpaceConverter.RGBToYUV(r, g, b, out var y, out var u, out var v);

        // 构建最小 JPEG（无 Huffman 表优化，使用固定表）
        return BuildMinimalJpeg(width, height, y, u, v);
    }

    /// <summary>构建最小基线 JPEG</summary>
    private static Byte[] BuildMinimalJpeg(Int32 width, Int32 height, Byte Y, Byte U, Byte V)
    {
        // 标准 JPEG 量化表（亮度）
        var lumQuant = new Byte[]
        {
            16, 11, 10, 16, 24, 40, 51, 61,
            12, 12, 14, 19, 26, 58, 60, 55,
            14, 13, 16, 24, 40, 57, 69, 56,
            14, 17, 22, 29, 51, 87, 80, 62,
            18, 22, 37, 56, 68, 109, 103, 77,
            24, 35, 55, 64, 81, 104, 113, 92,
            49, 64, 78, 87, 103, 121, 120, 101,
            72, 92, 95, 98, 112, 100, 103, 99,
        };

        using var ms = new MemoryStream();
        // SOI
        ms.WriteByte(0xFF);
        ms.WriteByte(0xD8);

        // DQT (量化表)
        ms.WriteByte(0xFF);
        ms.WriteByte(0xDB);
        ms.WriteByte(0x00);
        ms.WriteByte(67); // 长度 = 3 + 64
        ms.WriteByte(0x00); // 表 0, 8-bit
        ms.Write(lumQuant, 0, 64);

        // SOF0 (基线 DCT)
        ms.WriteByte(0xFF);
        ms.WriteByte(0xC0);
        var sofSize = 8 + 3; // 8byte头 + 1个分量*3
        ms.WriteByte((Byte)(sofSize >> 8));
        ms.WriteByte((Byte)(sofSize & 0xFF));
        ms.WriteByte(8); // 精度 8-bit
        ms.WriteByte((Byte)(height >> 8));
        ms.WriteByte((Byte)(height & 0xFF));
        ms.WriteByte((Byte)(width >> 8));
        ms.WriteByte((Byte)(width & 0xFF));
        ms.WriteByte(1); // 1 个分量（灰度）
        // Y 分量
        ms.WriteByte(1); // 分量 ID
        ms.WriteByte(0x11); // 水平/垂直采样 1x1
        ms.WriteByte(0); // 量化表 0

        // DHT (Huffman 表) - 最小 DC 表
        WriteMinimalDHT(ms, 0); // DC 表 0

        // SOS
        ms.WriteByte(0xFF);
        ms.WriteByte(0xDA);
        ms.WriteByte(0x00);
        ms.WriteByte(8); // 长度
        ms.WriteByte(1); // 1 个分量
        ms.WriteByte(1); // Y 分量
        ms.WriteByte(0x00); // DC 表 0, AC 表 0
        ms.WriteByte(0x00); // SS
        ms.WriteByte(0x3F); // SE
        ms.WriteByte(0x00); // Ah/Al

        // 压缩数据：单色图像，所有块 DC 相同
        var dcValue = Y - 128; // 中心化
        for (var i = 0; i < ((width + 7) / 8) * ((height + 7) / 8); i++)
        {
            WriteDCValue(ms, i == 0 ? dcValue : 0); // 首块写 DC，后续为差值 0
            ms.WriteByte(0x00); // EOB
        }

        // EOI
        ms.WriteByte(0xFF);
        ms.WriteByte(0xD9);

        return ms.ToArray();
    }

    private static void WriteMinimalDHT(MemoryStream ms, Int32 tableId)
    {
        ms.WriteByte(0xFF);
        ms.WriteByte(0xC4);
        ms.WriteByte(0x00);
        ms.WriteByte(19); // 长度 = 2 + 1 + 16

        ms.WriteByte((Byte)(tableId)); // 表类型 (0=DC, 16=AC)

        // 1~16 位的码字数
        ms.WriteByte(0x00); // 1 位
        ms.WriteByte(0x01); // 2 位
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);

        // 码字值
        ms.WriteByte(0x00); // 2 位码 = 0
    }

    private static void WriteDCValue(MemoryStream ms, Int32 value)
    {
        // 计算所需位数
        var absVal = Math.Abs(value);
        var bits = 0;
        var temp = absVal;
        while (temp > 0)
        {
            bits++;
            temp >>= 1;
        }

        // 写 Huffman 编码 (这里简化：2位码 0b00 = 类别bits)
        // 实际应写完整的 Huffman 位流，此为骨架
        if (bits > 0)
        {
            ms.WriteByte((Byte)bits);
            for (var i = bits - 1; i >= 0; i--)
            {
                ms.WriteByte((Byte)((value >> i) & 1));
            }
        }
    }

    #endregion
}
