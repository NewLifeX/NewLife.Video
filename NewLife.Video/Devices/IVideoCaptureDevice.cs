namespace NewLife.Video.Devices;

/// <summary>视频捕获设备接口</summary>
public interface IVideoCaptureDevice : IDisposable
{
    /// <summary>设备名称</summary>
    String Name { get; }

    /// <summary>设备 ID</summary>
    String DeviceId { get; }

    /// <summary>当前视频宽度</summary>
    Int32 VideoWidth { get; }

    /// <summary>当前视频高度</summary>
    Int32 VideoHeight { get; }

    /// <summary>当前帧率</summary>
    Double FrameRate { get; }

    /// <summary>当前像素格式</summary>
    VideoTypes PixelFormat { get; }

    /// <summary>支持的视频格式列表</summary>
    IReadOnlyList<VideoCaptureFormat> SupportedFormats { get; }

    /// <summary>是否正在采集</summary>
    Boolean IsCapturing { get; }

    /// <summary>帧采集事件</summary>
    event EventHandler<VideoFrame> FrameCaptured;

    /// <summary>启动采集</summary>
    void StartCapture();

    /// <summary>启动采集（指定格式）</summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="frameRate">帧率</param>
    /// <param name="pixelFormat">像素格式</param>
    void StartCapture(Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat);

    /// <summary>停止采集</summary>
    void StopCapture();
}

/// <summary>视频设备信息</summary>
public class VideoDeviceInfo
{
    /// <summary>设备名称</summary>
    public String Name { get; set; }

    /// <summary>设备 ID</summary>
    public String DeviceId { get; set; }

    /// <summary>后端类型（DirectShow/V4L2等）</summary>
    public String BackendType { get; set; }

    /// <summary>支持的视频格式</summary>
    public List<VideoCaptureFormat> SupportedFormats { get; set; } = [];
}

/// <summary>视频采集格式</summary>
public class VideoCaptureFormat
{
    /// <summary>宽度</summary>
    public Int32 Width { get; set; }

    /// <summary>高度</summary>
    public Int32 Height { get; set; }

    /// <summary>最大帧率</summary>
    public Double MaxFrameRate { get; set; }

    /// <summary>像素格式</summary>
    public VideoTypes PixelFormat { get; set; }

    /// <summary>字符串表示</summary>
    /// <returns></returns>
    public override String ToString() => $"{Width}x{Height} @ {MaxFrameRate}fps ({PixelFormat})";
}
