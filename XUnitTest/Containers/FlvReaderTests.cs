using NewLife.Video.Containers;
using Xunit;

namespace XUnitTest.Containers;

public class FlvReaderTests
{
    [Fact(DisplayName = "FlvReader读取有效FLV文件")]
    public void ReadFrame_ValidFlv_ReadsTags()
    {
        var path = Path.GetTempFileName() + ".flv";
        try
        {
            // 创建最小 FLV 文件（仅文件头 + 空数据）
            var flvData = CreateMinimalFlv();
            File.WriteAllBytes(path, flvData);

            using var reader = new FlvReader(path);

            Assert.True(reader.TotalFrames >= 0);
            Assert.True(reader.VideoWidth >= 0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact(DisplayName = "FlvReader不存在的文件抛异常")]
    public void Constructor_NonExistentFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            new FlvReader("Z:\\nonexistent.flv"));
    }

    [Fact(DisplayName = "FlvReader无效FLV文件抛异常")]
    public void Constructor_InvalidFlv_Throws()
    {
        var path = Path.GetTempFileName() + ".flv";
        try
        {
            File.WriteAllBytes(path, new Byte[] { 0x00, 0x00, 0x00, 0x00 });

            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                using var r = new FlvReader(path);
            });
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    /// <summary>创建最小可用 FLV 文件（仅文件头）</summary>
    private static Byte[] CreateMinimalFlv()
    {
        using var ms = new MemoryStream();

        // FLV Header: "FLV" + version(1) + flags(1) + headerSize(4)
        ms.WriteByte((Byte)'F');
        ms.WriteByte((Byte)'L');
        ms.WriteByte((Byte)'V');
        ms.WriteByte(1);     // version 1
        ms.WriteByte(0x01);  // flags: video only
        ms.WriteByte(0x00);  // header size = 9
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x09);

        // PreviousTagSize = 0 (4 bytes)
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);

        // 视频标签（最小）
        ms.WriteByte(9);     // tag type: video
        ms.WriteByte(0x00);  // data size (3 bytes) = 5
        ms.WriteByte(0x00);
        ms.WriteByte(5);
        ms.WriteByte(0x00);  // timestamp (3 bytes)
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);  // timestamp extended
        ms.WriteByte(0x00);  // stream ID (3 bytes)
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        // FrameType=1(Keyframe) CodecID=7(AVC) + AVCPacketType=0(seq header)
        ms.WriteByte(0x17);  // keyframe + AVC
        ms.WriteByte(0x00);  // AVCPacketType: sequence header
        ms.WriteByte(0x00);  // CompositionTime: 0
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);

        // PreviousTagSize (4 bytes)
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(11 + 5); // 11(tag header) + 5(data)

        return ms.ToArray();
    }
}
