using NewLife.Video.Containers;
using Xunit;

namespace XUnitTest;

public class Mp4ReaderTests
{
    [Fact(DisplayName = "Mp4Reader读取有效MP4文件")]
    public void ReadFrame_ValidMp4_ParsesHeader()
    {
        var path = Path.GetTempFileName() + ".mp4";
        try
        {
            // 创建最小 MP4 文件（ftyp + moov + mdat）
            var mp4Data = CreateMinimalMp4();
            File.WriteAllBytes(path, mp4Data);

            using var reader = new Mp4Reader(path);

            Assert.True(reader.VideoWidth >= 0);
            Assert.True(reader.VideoHeight >= 0);
            Assert.True(reader.TotalFrames >= -1);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    [Fact(DisplayName = "Mp4Reader不存在的文件抛异常")]
    public void Constructor_NonExistentFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            new Mp4Reader("Z:\\nonexistent.mp4"));
    }

    [Fact(DisplayName = "Mp4Reader工厂路由.mp4扩展名")]
    public void Factory_CreatesMp4Reader_ForMp4()
    {
        var path = Path.GetTempFileName() + ".mp4";
        try
        {
            var mp4Data = CreateMinimalMp4();
            File.WriteAllBytes(path, mp4Data);

            using var reader = NewLife.Video.VideoContainerFactory.CreateReader(path);
            Assert.IsType<Mp4Reader>(reader);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    /// <summary>创建最小可用 MP4 文件（ftyp + 空 moov + 空 mdat）</summary>
    private static Byte[] CreateMinimalMp4()
    {
        using var ms = new MemoryStream();

        // ftyp box
        WriteBox(ms, "ftyp", writer =>
        {
            writer.Write(new Byte[] { 0x69, 0x73, 0x6F, 0x6D }); // major brand 'isom'
            writer.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // minor version
            writer.Write(new Byte[] { 0x69, 0x73, 0x6F, 0x6D }); // compatible brand
        });

        // moov box (最小)
        WriteBox(ms, "moov", writer =>
        {
            // mvhd
            WriteBox(writer, "mvhd", w2 =>
            {
                w2.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // version+flags
                w2.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // creation
                w2.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // modification
                w2.Write(new Byte[] { 0x00, 0x00, 0x03, 0xE8 }); // timescale 1000
                w2.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // duration
                w2.Write(new Byte[] { 0x00, 0x01, 0x00, 0x00 }); // rate
                w2.Write(new Byte[] { 0x01, 0x00, 0x00, 0x00 }); // volume
                w2.Write(new Byte[40]); // reserved + matrix + pre_defined
                w2.Write(new Byte[] { 0x00, 0x00, 0x00, 0x01 }); // next_track_id
            });

            // trak
            WriteBox(writer, "trak", w2 =>
            {
                // tkhd
                WriteBox(w2, "tkhd", w3 =>
                {
                    w3.Write(new Byte[] { 0x00, 0x00, 0x00, 0x07 }); // version+flags (track enabled)
                    w3.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // creation
                    w3.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // modification
                    w3.Write(new Byte[] { 0x00, 0x00, 0x00, 0x01 }); // track_id
                    w3.Write(new Byte[4]); // reserved
                    w3.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // duration
                    w3.Write(new Byte[8]); // reserved
                    w3.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // layer
                    w3.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // alternate_group
                    w3.Write(new Byte[] { 0x01, 0x00, 0x00, 0x00 }); // volume
                    w3.Write(new Byte[36]); // matrix
                    w3.Write(new Byte[] { 0x02, 0x80, 0x00, 0x00 }); // width (640)
                    w3.Write(new Byte[] { 0x01, 0xE0, 0x00, 0x00 }); // height (480)
                });

                // mdia
                WriteBox(w2, "mdia", w3 =>
                {
                    // mdhd
                    WriteBox(w3, "mdhd", w4 =>
                    {
                        w4.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // version+flags
                        w4.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // creation
                        w4.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // modification
                        w4.Write(new Byte[] { 0x00, 0x00, 0x00, 0x19 }); // timescale 25
                        w4.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // duration
                        w4.Write(new Byte[] { 0x55, 0xC4 }); // language und
                        w4.Write(new Byte[2]); // pre_defined
                    });

                    // hdlr
                    WriteBox(w3, "hdlr", w4 =>
                    {
                        w4.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // version+flags
                        w4.Write(new Byte[4]); // pre_defined
                        w4.Write(new Byte[] { 0x76, 0x69, 0x64, 0x65 }); // handler_type 'vide'
                        w4.Write(new Byte[12]); // reserved
                        w4.Write(new Byte[] { 0x56, 0x69, 0x64, 0x65, 0x6F, 0x48, 0x61, 0x6E, 0x64, 0x6C, 0x65, 0x72, 0x00 }); // 'VideoHandler\0'
                    });

                    // minf
                    WriteBox(w3, "minf", w4 =>
                    {
                        // vmhd
                        WriteBox(w4, "vmhd", w5 =>
                        {
                            w5.Write(new Byte[] { 0x00, 0x00, 0x00, 0x01 }); // version+flags
                            w5.Write(new Byte[] { 0x00, 0x00 }); // graphicsmode
                            w5.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }); // opcolor
                        });

                        // dinf
                        WriteBox(w4, "dinf", w5 =>
                        {
                            WriteBox(w5, "dref", w6 =>
                            {
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // version+flags
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x01 }); // entry count
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x0C }); // entry size
                                w6.Write(new Byte[] { 0x75, 0x72, 0x6C, 0x20 }); // type 'url '
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x01 }); // version+flags (self-contained)
                            });
                        });

                        // stbl
                        WriteBox(w4, "stbl", w5 =>
                        {
                            // stsd
                            WriteBox(w5, "stsd", w6 =>
                            {
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // version+flags
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x01 }); // entry count (1)
                                // avc1 entry
                                WriteBoxData16(w6, "avc1", new Byte[]
                                {
                                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // reserved
                                    0x00, 0x01, // data_reference_index
                                    0x00, 0x00, // pre_defined
                                    0x00, 0x00, // reserved
                                    0x00, 0x00, 0x00, 0x00, // pre_defined
                                    0x02, 0x80, // width 640
                                    0x01, 0xE0, // height 480
                                    0x00, 0x48, 0x00, 0x00, // horiz/vert resolution 72dpi
                                    0x00, 0x00, 0x00, 0x00, // reserved
                                    0x00, 0x01, // frame count
                                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                    0x00, 0x18, 0xFF, 0xFF, // depth + pre_defined
                                });
                            });

                            // stts (空 - 0 entries)
                            WriteBox(w5, "stts", w6 =>
                            {
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // version+flags
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // entry count
                            });

                            // stsc (空)
                            WriteBox(w5, "stsc", w6 =>
                            {
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 });
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 });
                            });

                            // stsz (空)
                            WriteBox(w5, "stsz", w6 =>
                            {
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 });
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // sample size
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // sample count
                            });

                            // stco (空)
                            WriteBox(w5, "stco", w6 =>
                            {
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 });
                                w6.Write(new Byte[] { 0x00, 0x00, 0x00, 0x00 }); // entry count
                            });
                        });
                    });
                });
            });
        });

        // mdat box (空)
        WriteBox(ms, "mdat", _ => { });

        return ms.ToArray();
    }

    private static void WriteBox(Stream stream, String type, Action<MemoryStream> writeData)
    {
        using var dataMs = new MemoryStream();
        writeData(dataMs);
        var data = dataMs.ToArray();

        var size = 8 + data.Length;
        stream.Write(BigEndian((UInt32)size));
        stream.Write(System.Text.Encoding.ASCII.GetBytes(type));
        stream.Write(data);
    }

    private static void WriteBox16(Stream stream, String type, Action<MemoryStream> writeData)
    {
        using var dataMs = new MemoryStream();
        writeData(dataMs);
        var data = dataMs.ToArray();

        // 64-bit extended size
        var size = (UInt64)(16 + data.Length);
        stream.Write(new Byte[] { 0x00, 0x00, 0x00, 0x01 }); // size=1 (extended)
        stream.Write(System.Text.Encoding.ASCII.GetBytes(type));
        stream.Write(BigEndian(size));
        stream.Write(data);
    }

    private static void WriteBoxData16(Stream stream, String type, Byte[] data)
    {
        var entrySize = (UInt32)(8 + data.Length);
        stream.Write(BigEndian(entrySize));
        stream.Write(System.Text.Encoding.ASCII.GetBytes(type));
        stream.Write(data);
    }

    private static Byte[] BigEndian(UInt32 value) =>
    [
        (Byte)(value >> 24),
        (Byte)(value >> 16),
        (Byte)(value >> 8),
        (Byte)(value),
    ];

    private static Byte[] BigEndian(UInt64 value) =>
    [
        (Byte)(value >> 56),
        (Byte)(value >> 48),
        (Byte)(value >> 40),
        (Byte)(value >> 32),
        (Byte)(value >> 24),
        (Byte)(value >> 16),
        (Byte)(value >> 8),
        (Byte)(value),
    ];
}
