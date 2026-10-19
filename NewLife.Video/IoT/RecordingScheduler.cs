namespace NewLife.Video.IoT;

/// <summary>录像计划管理器。管理定时录像和事件触发录像</summary>
public class RecordingScheduler
{
    private readonly String _outputDirectory;
    private readonly List<RecordingPlan> _plans = [];
    private System.Threading.Timer _timer;
    private RecordingPlan _activePlan;

    /// <summary>录像文件输出目录</summary>
    public String OutputDirectory => _outputDirectory;

    /// <summary>录像计划列表</summary>
    public IReadOnlyList<RecordingPlan> Plans => _plans;

    /// <summary>当前活跃的录像计划</summary>
    public RecordingPlan ActivePlan => _activePlan;

    /// <summary>是否正在录像</summary>
    public Boolean IsRecording => _activePlan != null;

    /// <summary>录像开始事件</summary>
    public event EventHandler<RecordingPlan> RecordingStarted;

    /// <summary>录像停止事件</summary>
    public event EventHandler<RecordingPlan> RecordingStopped;

    /// <summary>创建录像计划管理器</summary>
    /// <param name="outputDirectory">输出目录</param>
    public RecordingScheduler(String outputDirectory)
    {
        _outputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
        if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);
    }

    /// <summary>添加定时录像计划</summary>
    /// <param name="plan">录像计划</param>
    public void AddPlan(RecordingPlan plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        _plans.Add(plan);
    }

    /// <summary>移除录像计划</summary>
    /// <param name="plan">录像计划</param>
    public void RemovePlan(RecordingPlan plan) => _plans.Remove(plan);

    /// <summary>启动计划调度</summary>
    public void Start()
    {
        _timer = new Timer(_ => CheckPlans(), null, 1000, 1000);
    }

    /// <summary>停止计划调度</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        StopRecording();
    }

    /// <summary>手动开始录像</summary>
    /// <param name="plan">录像计划</param>
    public void StartRecording(RecordingPlan plan)
    {
        if (_activePlan != null) StopRecording();
        _activePlan = plan;
        plan.StartTime = DateTime.Now;
        RecordingStarted?.Invoke(this, plan);
    }

    /// <summary>停止录像</summary>
    public void StopRecording()
    {
        if (_activePlan == null) return;
        var plan = _activePlan;
        _activePlan = null;
        plan.EndTime = DateTime.Now;
        RecordingStopped?.Invoke(this, plan);
    }

    /// <summary>生成录像文件路径</summary>
    /// <param name="plan">录像计划</param>
    /// <param name="timestamp">时间戳</param>
    /// <returns>文件路径</returns>
    public String GetFilePath(RecordingPlan plan, DateTime timestamp)
    {
        var fileName = $"{plan.Name}_{timestamp:yyyyMMdd_HHmmss}.mp4";
        return Path.Combine(_outputDirectory, fileName);
    }

    private void CheckPlans()
    {
        var now = DateTime.Now;
        foreach (var plan in _plans)
        {
            if (ShouldStart(plan, now) && !IsRecording)
                StartRecording(plan);
            else if (ShouldStop(plan, now) && IsRecording && _activePlan == plan)
                StopRecording();
        }
    }

    private static Boolean ShouldStart(RecordingPlan plan, DateTime now)
    {
        if (plan.Schedule == null) return false;
        var start = plan.Schedule.Value.Start;
        var end = plan.Schedule.Value.End;
        var time = now.TimeOfDay;
        return time >= start && time <= end && plan.StartTime == null;
    }

    private static Boolean ShouldStop(RecordingPlan plan, DateTime now)
    {
        if (plan.Schedule == null) return false;
        var end = plan.Schedule.Value.End;
        return now.TimeOfDay > end;
    }
}

/// <summary>录像计划</summary>
public class RecordingPlan
{
    /// <summary>计划名称</summary>
    public String Name { get; set; }

    /// <summary>定时时间段（null 表示手动触发）</summary>
    public (TimeSpan Start, TimeSpan End)? Schedule { get; set; }

    /// <summary>最大录像时长（分钟，0=不限制）</summary>
    public Int32 MaxDurationMinutes { get; set; }

    /// <summary>录像开始时间</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>录像结束时间</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>预录像秒数</summary>
    public Int32 PreRecordSeconds { get; set; }

    /// <summary>是否为事件触发录像</summary>
    public Boolean IsEventTriggered => !Schedule.HasValue;

    /// <summary>字符串表示</summary>
    public override String ToString()
    {
        if (Schedule.HasValue)
            return L.T("Scheduled recording [{0}] {1}~{2}", Name, Schedule.Value.Start, Schedule.Value.End);
        return L.T("Event recording [{0}]", Name);
    }
}
