using NewLife.Video;
using NewLife.Video.Avi;
using NewLife.Video.Containers;
using NewLife.Video.Flv;
using NewLife.Video.FMp4;
using NewLife.Video.Mkv;
using NewLife.Video.Raw;
using NewLife.Video.Ts;
using Xunit;

namespace XUnitTest.Containers;

public class VideoContainerFactoryTests
{
    [Fact(DisplayName = "工厂创建不存在的文件抛异常")]
    public void CreateReader_NonExistentFile_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() =>
            VideoContainerFactory.CreateReader("Z:\\nonexistent\\file.yuv"));
    }

    [Fact(DisplayName = "工厂按.yuv扩展名创建RawVideoReader")]
    public void CreateReader_YuvExtension_CreatesRawReader()
    {
        var path = TestFile.GetTempPath(".yuv");
        try
        {
            // 创建一个小的测试文件
            var data = new Byte[640 * 480 * 3 / 2]; // 一个 YUV420P 帧
            File.WriteAllBytes(path, data);

            using var reader = VideoContainerFactory.CreateReader(path);

            Assert.IsType<RawVideoReader>(reader);
            Assert.True(reader.VideoWidth > 0);
            Assert.True(reader.VideoHeight > 0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact(DisplayName = "工厂按.nv12扩展名创建RawVideoReader")]
    public void CreateReader_Nv12Extension_CreatesRawReader()
    {
        var path = TestFile.GetTempPath(".nv12");
        try
        {
            var data = new Byte[640 * 480 * 3 / 2];
            File.WriteAllBytes(path, data);

            using var reader = VideoContainerFactory.CreateReader(path);

            Assert.IsType<RawVideoReader>(reader);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact(DisplayName = "工厂按.mp4扩展名创建Mp4Reader")]
    public void CreateReader_Mp4Extension_CreatesMp4Reader()
    {
        var path = TestFile.GetTempPath(".mp4");
        try
        {
            // 创建最小 MP4
            var mp4Data = CreateMinimalMp4Bytes();
            File.WriteAllBytes(path, mp4Data);

            using var reader = VideoContainerFactory.CreateReader(path);

            Assert.IsType<Mp4Reader>(reader);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    [Fact(DisplayName = "VideoContainerFactory.CreateWriter 路由正确")]
    public void CreateWriter_Routes()
    {
        var w1 = VideoContainerFactory.CreateWriter(TestFile.GetTempPath(".mp4"));
        Assert.IsType<Mp4Writer>(w1);
        w1.Dispose();

        var w2 = VideoContainerFactory.CreateWriter(TestFile.GetTempPath(".flv"));
        Assert.IsType<FlvWriter>(w2);
        w2.Dispose();

        var w3 = VideoContainerFactory.CreateWriter(TestFile.GetTempPath(".avi"));
        Assert.IsType<AviWriter>(w3);
        w3.Dispose();

        var w4 = VideoContainerFactory.CreateWriter(TestFile.GetTempPath(".ts"));
        Assert.IsType<TsWriter>(w4);
        w4.Dispose();

        var w5 = VideoContainerFactory.CreateWriter(TestFile.GetTempPath(".mkv"));
        Assert.IsType<MkvWriter>(w5);
        w5.Dispose();

        var w6 = VideoContainerFactory.CreateWriter(TestFile.GetTempPath(".webm"));
        Assert.IsType<MkvWriter>(w6);
        w6.Dispose();

        var w7 = VideoContainerFactory.CreateWriter(TestFile.GetTempPath(".yuv"));
        Assert.IsType<RawVideoWriter>(w7);
        w7.Dispose();
    }

    [Fact(DisplayName = "VideoContainerFactory 魔术字节检测FLV")]
    public void MagicBytes_DetectsFlv()
    {
        var path = TestFile.GetTempPath(".dat");
        try
        {
            // 最小合法 FLV: FLV(3) + version(1) + flags(1) + headerSize(4) = 9 bytes
            using (var fs = new FileStream(path, FileMode.Create))
            {
                fs.WriteByte((Byte)'F'); fs.WriteByte((Byte)'L'); fs.WriteByte((Byte)'V');
                fs.WriteByte(0x01); // version
                fs.WriteByte(0x05); // flags: video + audio
                WriteUInt32BE(fs, 9); // header size
                WriteUInt32BE(fs, 0); // previous tag size
            }
            var reader = VideoContainerFactory.CreateReader(path);
            Assert.IsType<FlvReader>(reader);
            reader.Dispose();
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    [Fact(DisplayName = "VideoContainerFactory 魔术字节检测AVI")]
    public void MagicBytes_DetectsAvi()
    {
        var path = TestFile.GetTempPath(".dat");
        try
        {
            // 最小合法 RIFF AVI: RIFF + size + "AVI "
            using (var fs = new FileStream(path, FileMode.Create))
            {
                fs.WriteByte((Byte)'R'); fs.WriteByte((Byte)'I'); fs.WriteByte((Byte)'F'); fs.WriteByte((Byte)'F');
                WriteUInt32LE(fs, 100); // file size placeholder
                fs.WriteByte((Byte)'A'); fs.WriteByte((Byte)'V'); fs.WriteByte((Byte)'I'); fs.WriteByte((Byte)' ');
                fs.Write(new Byte[100], 0, 100);
            }
            var reader = VideoContainerFactory.CreateReader(path);
            Assert.IsType<AviReader>(reader);
            reader.Dispose();
        }
        finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    }

    private static void WriteUInt32BE(FileStream fs, UInt32 v)
    {
        fs.WriteByte((Byte)(v >> 24)); fs.WriteByte((Byte)(v >> 16));
        fs.WriteByte((Byte)(v >> 8)); fs.WriteByte((Byte)v);
    }

    private static void WriteUInt32LE(FileStream fs, UInt32 v)
    {
        fs.WriteByte((Byte)v); fs.WriteByte((Byte)(v >> 8));
        fs.WriteByte((Byte)(v >> 16)); fs.WriteByte((Byte)(v >> 24));
    }

    /// <summary>创建最小 MP4 用于测试工厂路由</summary>
    private static Byte[] CreateMinimalMp4Bytes()
    {
        using var ms = new MemoryStream();
        // ftyp
        ms.Write(new Byte[] { 0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D, 0x00, 0x00, 0x00, 0x00, 0x69, 0x73, 0x6F, 0x6D });
        // moov (最小)
        ms.Write(new Byte[] { 0x00, 0x00, 0x00, 0x08, 0x6D, 0x6F, 0x6F, 0x76 });
        // mdat (空)
        ms.Write(new Byte[] { 0x00, 0x00, 0x00, 0x08, 0x6D, 0x64, 0x61, 0x74 });
        return ms.ToArray();
    }
}
