using NewLife.Buffers;

namespace NewLife.Video;

/// <summary>视频编码格式自动检测器。根据数据流起始字节识别编码类型</summary>
public static class VideoFormatDetector
{
    /// <summary>检测视频编码类型</summary>
    /// <param name="data">视频数据（至少前几个字节）</param>
    /// <returns>检测到的编码类型，Unknown 表示无法识别</returns>
    public static VideoTypes Detect(Byte[] data)
    {
        if (data == null || data.Length < 2) return VideoTypes.Unknown;

        // JPEG: SOI 标记 0xFF 0xD8
        if (data[0] == 0xFF && data[1] == 0xD8)
            return VideoTypes.MJPEG;

        // H.264/H.265: 起始码 0x00 0x00 0x00 0x01 或 0x00 0x00 0x01
        if (data.Length >= 4 && data[0] == 0x00 && data[1] == 0x00)
        {
            var offset = data[2] == 0x01 ? 3 : (data[2] == 0x00 && data[3] == 0x01 ? 4 : -1);
            if (offset > 0 && offset < data.Length)
            {
                var nalByte = data[offset];
                // H.264 NAL type (low 5 bits, range 1-9)
                var h264Type = nalByte & 0x1F;
                if (h264Type is >= 1 and <= 9)
                    return VideoTypes.H264;

                // H.265 NAL header (2 bytes, 大端)
                if (offset + 1 < data.Length)
                {
                    var nr = new SpanReader(data, offset) { IsLittleEndian = false };
                    var nalHeader = nr.ReadUInt16();
                    var forbiddenBit = (nalHeader >> 15) & 1;
                    var h265Type = (nalHeader >> 9) & 0x3F;
                    if (forbiddenBit == 0 && h265Type is >= 1 and <= 40)
                        return VideoTypes.H265;
                }
            }
        }

        // FLV: 签名 "FLV"
        if (data.Length >= 3 && data[0] == 'F' && data[1] == 'L' && data[2] == 'V')
            return VideoTypes.Unknown; // FLV 是容器不是编码

        // VP8: IVF 头 "DKIF" (VP8/VP9 常用 IVF 容器)
        if (data.Length >= 4 && data[0] == 'D' && data[1] == 'K' && data[2] == 'I' && data[3] == 'F')
            return VideoTypes.VP8;

        // VP8 裸流: 帧头 key_frame bit + version + show_frame + data_size
        if (data.Length >= 3)
        {
            var frameType = data[0] & 0x01;
            if (frameType == 0 && (data[0] & 0x0E) == 0) // key frame, version 0
            {
                // 可能是 VP8
                var showFrame = (data[0] >> 4) & 1;
                var size = (data[0] >> 5) | (data[1] << 3) | (data[2] << 11);
                if (size > 0 && size < 10 * 1024 * 1024)
                    return VideoTypes.VP8;
            }
        }

        return VideoTypes.Unknown;
    }

    /// <summary>从 NAL 类型确认 H.264</summary>
    private static VideoTypes DetectH264NalType(Byte[] data, Int32 offset)
    {
        if (offset >= data.Length) return VideoTypes.Unknown;
        var nalType = data[offset] & 0x1F;
        // H.264 NAL 类型范围：1-5(slice), 6(SEI), 7(SPS), 8(PPS), 9(AUD)
        if (nalType is >= 1 and <= 9)
            return VideoTypes.H264;
        return VideoTypes.Unknown;
    }

    /// <summary>检测数据是否为 H.264 编码</summary>
    public static Boolean IsH264(Byte[] data) => Detect(data) == VideoTypes.H264;

    /// <summary>检测数据是否为 H.265 编码</summary>
    public static Boolean IsH265(Byte[] data) => Detect(data) == VideoTypes.H265;

    /// <summary>检测数据是否为 MJPEG 编码</summary>
    public static Boolean IsMjpeg(Byte[] data) => Detect(data) == VideoTypes.MJPEG;
}
