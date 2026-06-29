using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest;

public class JitterBufferTests
{
    [Fact(DisplayName = "JitterBuffer初始为空")]
    public void InitialState_Empty()
    {
        var buffer = new JitterBuffer(200);

        Assert.Equal(0, buffer.Count);
        Assert.Null(buffer.Dequeue(0));
    }

    [Fact(DisplayName = "JitterBuffer按时序输出帧")]
    public void EnqueueDequeue_InOrder()
    {
        var buffer = new JitterBuffer(100);

        buffer.Enqueue(new VPacket { PTS = 0 });
        buffer.Enqueue(new VPacket { PTS = 30 });
        buffer.Enqueue(new VPacket { PTS = 60 });

        Assert.Equal(3, buffer.Count);

        // 时间未到，不应出队
        Assert.Null(buffer.Dequeue(0));
        Assert.Null(buffer.Dequeue(50));

        // 时间到达第1帧播放时间
        var frame = buffer.Dequeue(100);
        Assert.NotNull(frame);
        Assert.Equal(0, frame.PTS);
        Assert.Equal(2, buffer.Count);

        // 第2帧到达
        frame = buffer.Dequeue(130);
        Assert.NotNull(frame);
        Assert.Equal(30, frame.PTS);

        // 第3帧到达
        frame = buffer.Dequeue(160);
        Assert.NotNull(frame);
        Assert.Equal(60, frame.PTS);

        Assert.Equal(0, buffer.Count);
    }

    [Fact(DisplayName = "JitterBuffer乱序输入按PTS排序输出")]
    public void Enqueue_OutOfOrder_SortedByPts()
    {
        var buffer = new JitterBuffer(100);

        buffer.Enqueue(new VPacket { PTS = 60, Data = new ArrayPacket(new Byte[] { 3 }) });
        buffer.Enqueue(new VPacket { PTS = 0, Data = new ArrayPacket(new Byte[] { 1 }) });
        buffer.Enqueue(new VPacket { PTS = 30, Data = new ArrayPacket(new Byte[] { 2 }) });

        var frame1 = buffer.Dequeue(100);
        Assert.Equal(0, frame1.PTS);

        var frame2 = buffer.Dequeue(130);
        Assert.Equal(30, frame2.PTS);

        var frame3 = buffer.Dequeue(160);
        Assert.Equal(60, frame3.PTS);
    }

    [Fact(DisplayName = "JitterBuffer延迟参数生效")]
    public void Delay_PreventsEarlyDequeue()
    {
        var buffer = new JitterBuffer(500); // 大延迟

        buffer.Enqueue(new VPacket { PTS = 0 });

        // 即使时间到达，延迟大也需要等待
        Assert.Null(buffer.Dequeue(0));
        Assert.Null(buffer.Dequeue(400));
        Assert.NotNull(buffer.Dequeue(500));
    }

    [Fact(DisplayName = "JitterBuffer Clear清空")]
    public void Clear_EmptiesBuffer()
    {
        var buffer = new JitterBuffer(100);
        buffer.Enqueue(new VPacket { PTS = 0 });
        buffer.Enqueue(new VPacket { PTS = 30 });

        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.Null(buffer.Dequeue(200));
    }

    [Fact(DisplayName = "JitterBuffer Drain导出全部")]
    public void Drain_ReturnsAll()
    {
        var buffer = new JitterBuffer(100);
        buffer.Enqueue(new VPacket { PTS = 0 });
        buffer.Enqueue(new VPacket { PTS = 30 });

        var drained = buffer.Drain();

        Assert.Equal(2, drained.Count);
        Assert.Equal(0, buffer.Count);
    }

    [Fact(DisplayName = "JitterBuffer Enqueue null 不计数")]
    public void Enqueue_Null_NotCounted()
    {
        var buffer = new JitterBuffer(100);
        buffer.Enqueue(null);

        Assert.Equal(0, buffer.Count);
    }
}
