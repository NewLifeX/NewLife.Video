using NewLife.Video.IoT;
using Xunit;

namespace XUnitTest.IoT;

public class RecordingSchedulerTests
{
    [Fact(DisplayName = "RecordingScheduler 初始状态")]
    public void InitialState()
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
    public void Plan_Schedule_ToString()
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
    public void Plan_Event_ToString()
    {
        var plan = new RecordingPlan { Name = "Motion" };
        Assert.True(plan.IsEventTriggered);
        Assert.Contains("Motion", plan.ToString());
    }

    [Fact(DisplayName = "RecordingScheduler 添加/移除计划")]
    public void AddRemovePlan()
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
