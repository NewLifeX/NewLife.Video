using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Codecs;
using NewLife.Video.Containers;
using NewLife.Video.Processing;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>帧级浏览器视图模型（FRAM-3）</summary>
public partial class FrameBrowserViewModel : ViewModelBase
{
    private IVideoContainerReader? _reader;
    private Int64 _currentFrameIndex;

    [ObservableProperty]
    private String _filePath = "";

    [ObservableProperty]
    private WriteableBitmap? _frameBitmap;

    [ObservableProperty]
    private Boolean _hasVideo;

    [ObservableProperty]
    private Int64 _totalFrames;

    [ObservableProperty]
    private Int64 _currentFrame;

    [ObservableProperty]
    private String _frameType = "";

    [ObservableProperty]
    private Int32 _frameWidth;

    [ObservableProperty]
    private Int32 _frameHeight;

    [ObservableProperty]
    private String _statusText = "就绪";

    /// <summary>打开文件</summary>
    [RelayCommand]
    private void OpenFile()
    {
        // TODO: 文件对话框
    }

    /// <summary>加载视频</summary>
    [RelayCommand]
    private void LoadVideo(String path)
    {
        if (String.IsNullOrWhiteSpace(path)) return;
        CloseVideo();

        try
        {
            _reader = VideoContainerFactory.CreateReader(path);
            if (_reader == null) { StatusText = "无法打开文件"; return; }

            HasVideo = true;
            TotalFrames = _reader.TotalFrames;
            FrameWidth = _reader.VideoWidth;
            FrameHeight = _reader.VideoHeight;
            _currentFrameIndex = -1;
            StatusText = $"已加载: {Path.GetFileName(path)}";
            FilePath = path;

            GoToFrame(0);
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
        }
    }

    /// <summary>跳转到上一帧</summary>
    [RelayCommand]
    private void PreviousFrame()
    {
        if (_currentFrameIndex > 0)
            GoToFrame(_currentFrameIndex - 1);
    }

    /// <summary>跳转到下一帧</summary>
    [RelayCommand]
    private void NextFrame()
    {
        if (_reader == null || (_reader.TotalFrames > 0 && _currentFrameIndex >= _reader.TotalFrames - 1))
            return;
        GoToFrame(_currentFrameIndex + 1);
    }

    /// <summary>跳转到指定帧</summary>
    [RelayCommand]
    private void GoToFrame(Int64 frameIndex)
    {
        if (_reader == null) return;

        try
        {
            // SeekFrame 返回 false 时尝试连续读取
            if (!_reader.SeekFrame(frameIndex))
                return;

            var packet = _reader.ReadFrame();
            if (packet == null) return;

            _currentFrameIndex = frameIndex;
            CurrentFrame = frameIndex;
            FrameType = packet.IsKeyFrame ? "关键帧" : "普通帧";

            // 尝试解码并显示
            RenderFrame(packet);
            StatusText = $"帧 {frameIndex + 1}/{TotalFrames} ({FrameType})";
        }
        catch (Exception ex)
        {
            StatusText = $"跳转失败: {ex.Message}";
        }
    }

    private void RenderFrame(VPacket packet)
    {
        try
        {
            // 尝试用工厂解码
            var factory = new VideoCodecFactory();
            var frames = factory.Decode(packet);
            if (frames == null || frames.Length == 0) return;

            var frame = frames[0];
            var bgra = VideoFrameRenderer.ToBgra32(frame);
            var bitmap = new WriteableBitmap(
                new PixelSize(frame.Width, frame.Height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);

            unsafe
            {
                using var locked = bitmap.Lock();
                fixed (Byte* pSrc = bgra)
                {
                    System.Buffer.MemoryCopy(pSrc, (Byte*)locked.Address, bgra.Length, bgra.Length);
                }
            }

            FrameBitmap = bitmap;
        }
        catch
        {
            // 跳过解码失败的情况
        }
    }

    /// <summary>关闭</summary>
    [RelayCommand]
    private void CloseVideo()
    {
        if (_reader != null)
        {
            _reader.Dispose();
            _reader = null;
        }
        HasVideo = false;
        FrameBitmap = null;
        _currentFrameIndex = 0;
        CurrentFrame = 0;
        TotalFrames = 0;
        FrameWidth = 0;
        FrameHeight = 0;
        StatusText = "已关闭";
    }
}
