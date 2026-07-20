using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest.Streaming;

public class CameraStreamManagerTests
{
    [Fact(DisplayName = "CameraStreamManager 初始状态")]
    public void InitialState_Correct()
    {
        using var mgr = new CameraStreamManager();
        Assert.Equal(0, mgr.CameraCount);
        Assert.Equal(0, mgr.ConnectedCount);
    }

    [Fact(DisplayName = "CameraStreamManager 添加和移除摄像头")]
    public void AddRemove_Camera()
    {
        using var mgr = new CameraStreamManager();
        mgr.AddCamera("cam1", "rtsp://192.168.1.64/stream");
        Assert.Equal(1, mgr.CameraCount);

        mgr.RemoveCamera("cam1");
        Assert.Equal(0, mgr.CameraCount);
    }

    [Fact(DisplayName = "CameraStreamManager 移除不存在的摄像头")]
    public void Remove_NonExistent_Safe()
    {
        using var mgr = new CameraStreamManager();
        mgr.RemoveCamera("nonexistent");
        Assert.Equal(0, mgr.CameraCount);
    }

    [Fact(DisplayName = "CameraStreamManager 重复添加同名摄像头")]
    public void Add_DuplicateName()
    {
        using var mgr = new CameraStreamManager();
        mgr.AddCamera("cam1", "rtsp://192.168.1.64/stream");
        mgr.AddCamera("cam1", "rtsp://192.168.1.65/stream");
        Assert.Equal(1, mgr.CameraCount);
    }

    [Fact(DisplayName = "CameraStreamManager 多个摄像头")]
    public void Add_MultipleCameras()
    {
        using var mgr = new CameraStreamManager();
        mgr.AddCamera("cam1", "rtsp://192.168.1.64/stream");
        mgr.AddCamera("cam2", "rtsp://192.168.1.65/stream");
        mgr.AddCamera("cam3", "rtsp://192.168.1.66/stream");
        Assert.Equal(3, mgr.CameraCount);
    }

    [Fact(DisplayName = "CameraStreamManager Dispose 安全")]
    public void Dispose_Safe()
    {
        var mgr = new CameraStreamManager();
        mgr.Dispose();
        Assert.True(true);
    }
}
