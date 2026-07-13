namespace NewLife.Video.Studio.ViewModels;

/// <summary>屏幕捕获服务接口</summary>
public interface IScreenCaptureService : IDisposable
{
    /// <summary>帧捕获事件</summary>
    event EventHandler<NewLife.Video.VideoFrame>? FrameCaptured;

    /// <summary>是否正在捕获</summary>
    Boolean IsCapturing { get; }

    /// <summary>开始捕获</summary>
    void Start();

    /// <summary>停止捕获</summary>
    void Stop();
}
