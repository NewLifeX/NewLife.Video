using NewLife.Video.IoT;
using Xunit;

namespace XUnitTest;

public class OnvifTests
{
    [Fact(DisplayName = "OnvifMediaClient 构造参数验证")]
    public void MediaClient_NullUrl_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OnvifMediaClient(null));
    }

    [Fact(DisplayName = "OnvifProfile ToString正确")]
    public void OnvifProfile_ToString_Correct()
    {
        var p = new OnvifProfile { Token = "main", Name = "MainStream", VideoWidth = 1920, VideoHeight = 1080 };
        var s = p.ToString();
        Assert.Contains("MainStream", s);
        Assert.Contains("1920", s);
    }

    [Fact(DisplayName = "OnvifPtzClient 构造参数验证")]
    public void PtzClient_NullUrl_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OnvifPtzClient(null));
    }

    [Fact(DisplayName = "OnvifPtzClient 可正常构造")]
    public void PtzClient_Constructs()
    {
        using var client = new OnvifPtzClient("http://192.168.1.1/onvif/ptz");
        Assert.NotNull(client);
    }

    [Fact(DisplayName = "RecordingScheduler 初始状态")]
    public void Scheduler_InitialState()
    {
        var dir = Path.Combine(Path.GetTempPath(), "test_rec_" + Guid.NewGuid());
        try
        {
            var scheduler = new RecordingScheduler(dir);
            Assert.Empty(scheduler.Plans);
            Assert.False(scheduler.IsRecording);
            Assert.Equal(dir, scheduler.OutputDirectory);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact(DisplayName = "RecordingPlan 定时计划字符串表示")]
    public void RecordingPlan_Schedule_ToString()
    {
        var plan = new RecordingPlan
        {
            Name = "DayTime",
            Schedule = (new TimeSpan(8, 0, 0), new TimeSpan(18, 0, 0)),
        };
        Assert.Contains("DayTime", plan.ToString());
        Assert.Contains("08:00", plan.ToString());
    }

    [Fact(DisplayName = "RecordingPlan 事件计划字符串表示")]
    public void RecordingPlan_Event_ToString()
    {
        var plan = new RecordingPlan { Name = "Motion" };
        Assert.True(plan.IsEventTriggered);
        Assert.Contains("Motion", plan.ToString());
    }

    [Fact(DisplayName = "RecordingScheduler 添加/移除计划")]
    public void Scheduler_AddRemovePlan()
    {
        var dir = Path.Combine(Path.GetTempPath(), "test_rec2_" + Guid.NewGuid());
        try
        {
            var scheduler = new RecordingScheduler(dir);
            var plan = new RecordingPlan { Name = "Test" };
            scheduler.AddPlan(plan);
            Assert.Single(scheduler.Plans);
            scheduler.RemovePlan(plan);
            Assert.Empty(scheduler.Plans);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
