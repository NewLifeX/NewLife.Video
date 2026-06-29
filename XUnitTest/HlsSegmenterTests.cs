using NewLife.Video;
using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest;

public class HlsSegmenterTests
{
    [Fact(DisplayName = "HlsSegmenter 初始化正确")]
    public void Constructor_InitializesCorrectly()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hls_" + Guid.NewGuid());
        try
        {
            var seg = new HlsSegmenter(dir, "test", 2000);
            Assert.Equal(dir, seg.OutputDir);
            Assert.Equal(2, seg.TargetDurationSeconds);
            Assert.True(Directory.Exists(dir));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact(DisplayName = "HlsSegmenter 添加数据并生成播放列表")]
    public void AddData_GeneratesSegments()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hls2_" + Guid.NewGuid());
        try
        {
            var seg = new HlsSegmenter(dir, "stream", 1000);

            // 添加两段数据（第二段标记为关键帧触发新分段）
            seg.AddData(new Byte[100], 0, true);
            seg.AddData(new Byte[200], 500, false);
            seg.AddData(new Byte[100], 1200, true); // 超过1秒，触发新分段

            var playlist = seg.GeneratePlaylist();
            Assert.Contains("#EXTM3U", playlist);
            Assert.Contains("#EXTINF:", playlist);
            Assert.Contains(".ts", playlist);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact(DisplayName = "HlsSegmenter 写入播放列表文件")]
    public void WritePlaylist_CreatesFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hls3_" + Guid.NewGuid());
        try
        {
            var seg = new HlsSegmenter(dir, "live");
            seg.AddData(new Byte[50], 0, true);

            var path = seg.WritePlaylist();
            Assert.True(File.Exists(path));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact(DisplayName = "HlsSegmenter Cleanup 清理旧分段")]
    public void Cleanup_RemovesOldSegments()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hls4_" + Guid.NewGuid());
        try
        {
            var seg = new HlsSegmenter(dir, "s", 500);
            for (var i = 0; i < 5; i++)
                seg.AddData(new Byte[10], i * 600, true);
            seg.GeneratePlaylist();

            Assert.True(seg.Segments.Count >= 3);
            seg.Cleanup(2);
            Assert.Equal(2, seg.Segments.Count);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
