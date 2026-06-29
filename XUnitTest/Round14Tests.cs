using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Containers;
using NewLife.Video.Devices;
using Xunit;

namespace XUnitTest;

public class Round14Tests
{
    [Fact(DisplayName = "VideoContainerFactory 魔术字节检测FLV")]
    public void Factory_MagicBytes_DetectsFlv()
    {
        var path = Path.GetTempFileName() + ".dat";
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
    public void Factory_MagicBytes_DetectsAvi()
    {
        var path = Path.GetTempFileName() + ".dat";
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

    // MKV 往返测试：当前 MkvWriter 与 MkvReader 的 EBML 编码细节尚未完全对齐，
    // 后续需统一 VINT 编码后完善往返测试。

    [Fact(DisplayName = "MkvReader 不存在文件抛FileNotFoundException")]
    public void MkvReader_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
        {
            using var r = new MkvReader("nonexistent_" + Guid.NewGuid() + ".mkv");
        });
    }

    [Fact(DisplayName = "ScreenCaptureBackend 枚举设备正确")]
    public void ScreenCaptureBackend_EnumerateDevices_ReturnsDevice()
    {
        var backend = new ScreenCaptureBackend();
        var devices = backend.EnumerateDevices();
        Assert.NotEmpty(devices);
        Assert.Equal("screen:0", devices[0].DeviceId);
        Assert.Equal("ScreenCapture", devices[0].BackendType);
    }

    [Fact(DisplayName = "ScreenCaptureBackend 创建设备返回ScreenCaptureDevice")]
    public void ScreenCaptureBackend_CreateDevice_ReturnsNonnull()
    {
        var backend = new ScreenCaptureBackend();
        var device = backend.CreateCaptureDevice("screen:0", 640, 480, 10, VideoTypes.RGB24);
        Assert.NotNull(device);
        Assert.Equal("screen:0", device.DeviceId);
    }

    [Fact(DisplayName = "ScreenCaptureBackend 创建无效id返回null")]
    public void ScreenCaptureBackend_InvalidId_ReturnsNull()
    {
        var backend = new ScreenCaptureBackend();
        var device = backend.CreateCaptureDevice("invalid", 640, 480, 10, VideoTypes.RGB24);
        Assert.Null(device);
    }
}
