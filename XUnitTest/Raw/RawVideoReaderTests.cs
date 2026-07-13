using NewLife.Video;
using NewLife.Video.Raw;
using Xunit;

namespace XUnitTest.Raw;

public class RawVideoReaderTests
{
    [Fact(DisplayName = "RawVideoReader读取YUV420P帧正确")]
    public void ReadFrame_YUV420P_ReadsCorrectly()
    {
        var width = 320;
        var height = 240;
        var frameSize = VideoFrame.CalculateDataSize(width, height, VideoTypes.YUV420P);
        var totalFrames = 3;
        var path = Path.GetTempFileName() + ".yuv";
        try
        {
            // 创建 3 帧测试数据
            var data = new Byte[frameSize * totalFrames];
            for (var f = 0; f < totalFrames; f++)
            {
                // 每帧填充不同的 Y 值以便区分
                for (var i = 0; i < width * height; i++)
                    data[f * frameSize + i] = (Byte)(100 + f * 10);
            }
            File.WriteAllBytes(path, data);

            using var reader = new RawVideoReader(path, width, height, VideoTypes.YUV420P);

            Assert.Equal(totalFrames, reader.TotalFrames);
            Assert.Equal(width, reader.VideoWidth);
            Assert.Equal(height, reader.VideoHeight);

            // 读取第一帧
            var frame1 = reader.ReadFrame();
            Assert.NotNull(frame1);
            Assert.Equal(VideoTypes.YUV420P, frame1.CodecType);
            Assert.Equal(0, frame1.FrameIndex);

            // 读取第二帧
            var frame2 = reader.ReadFrame();
            Assert.NotNull(frame2);
            Assert.Equal(1, frame2.FrameIndex);

            // 读取第三帧
            var frame3 = reader.ReadFrame();
            Assert.NotNull(frame3);
            Assert.Equal(2, frame3.FrameIndex);

            // 流末尾返回 null
            Assert.Null(reader.ReadFrame());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact(DisplayName = "RawVideoReader SeekFrame 正确跳转")]
    public void SeekFrame_JumpsCorrectly()
    {
        var width = 64;
        var height = 48;
        var frameSize = VideoFrame.CalculateDataSize(width, height, VideoTypes.YUV420P);
        var totalFrames = 5;
        var path = Path.GetTempFileName() + ".yuv";
        try
        {
            var data = new Byte[frameSize * totalFrames];
            // 每帧标记不同的首字节
            for (var f = 0; f < totalFrames; f++)
                data[f * frameSize] = (Byte)(f + 1);
            File.WriteAllBytes(path, data);

            using var reader = new RawVideoReader(path, width, height, VideoTypes.YUV420P);

            // 跳转到第 3 帧
            Assert.True(reader.SeekFrame(2));
            var frame = reader.ReadFrame();
            Assert.NotNull(frame);
            Assert.Equal(2, frame.FrameIndex);

            // 跳转到越界帧失败
            Assert.False(reader.SeekFrame(totalFrames + 1));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact(DisplayName = "RawVideoReader总帧数计算正确")]
    public void TotalFrames_CorrectCount()
    {
        var width = 128;
        var height = 96;
        var frameSize = VideoFrame.CalculateDataSize(width, height, VideoTypes.YUV420P);
        var totalFrames = 10;
        var path = Path.GetTempFileName() + ".yuv";
        try
        {
            var data = new Byte[frameSize * totalFrames];
            File.WriteAllBytes(path, data);

            using var reader = new RawVideoReader(path, width, height, VideoTypes.YUV420P);

            Assert.Equal(totalFrames, reader.TotalFrames);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact(DisplayName = "RawVideoReader时长计算正确")]
    public void Duration_CorrectMilliseconds()
    {
        var width = 64;
        var height = 48;
        var frameSize = VideoFrame.CalculateDataSize(width, height, VideoTypes.YUV420P);
        var totalFrames = 25; // 1 秒 @ 25fps
        var path = Path.GetTempFileName() + ".yuv";
        try
        {
            var data = new Byte[frameSize * totalFrames];
            File.WriteAllBytes(path, data);

            using var reader = new RawVideoReader(path, width, height, VideoTypes.YUV420P);

            Assert.Equal(1000, reader.Duration); // 25 帧 / 25fps = 1 秒 = 1000ms
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact(DisplayName = "RawVideoReader释放后可安全Dispose")]
    public void Dispose_CanDisposeTwice()
    {
        var path = Path.GetTempFileName() + ".yuv";
        try
        {
            var data = new Byte[100];
            File.WriteAllBytes(path, data);

            var reader = new RawVideoReader(path, 4, 4, VideoTypes.YUV420P);
            reader.Dispose();
            reader.Dispose(); // 不应抛异常
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
