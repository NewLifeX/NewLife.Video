using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Devices;
using NewLife.Video.Processing;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>屏幕捕获视图模型（CAPT-3）</summary>
public partial class ScreenCaptureViewModel : ViewModelBase
{
    private IScreenCaptureService? _screenCapture;

    [ObservableProperty]
    private WriteableBitmap? _previewBitmap;

    [ObservableProperty]
    private Boolean _hasPreview;

    [ObservableProperty]
    private Boolean _isCapturing;

    [ObservableProperty]
    private Int32 _captureWidth = 1920;

    [ObservableProperty]
    private Int32 _captureHeight = 1080;

    [ObservableProperty]
    private Int32 _captureFrameRate = 15;

    [ObservableProperty]
    private Boolean _captureCursor = true;

    [ObservableProperty]
    private String _outputPath = "";

    [ObservableProperty]
    private String _statusText = "就绪";

    /// <summary>可用帧率选项</summary>
    public ObservableCollection<Int32> FrameRateOptions { get; } = [5, 10, 15, 20, 24, 30];

    /// <summary>可用分辨率选项</summary>
    public ObservableCollection<ResolutionOption> ResolutionOptions { get; } =
    [
        new("原始分辨率", 0, 0),
        new("1920×1080", 1920, 1080),
        new("1280×720", 1280, 720),
        new("1024×768", 1024, 768),
        new("800×600", 800, 600),
        new("640×480", 640, 480),
    ];

    /// <summary>开始屏幕捕获</summary>
    [RelayCommand]
    private void StartCapture()
    {
        if (IsCapturing) return;

        try
        {
            _screenCapture = new ScreenCaptureService();
            _screenCapture.FrameCaptured += OnFrameCaptured;
            _screenCapture.Start();

            IsCapturing = true;
            StatusText = $"正在捕获屏幕...";
        }
        catch (Exception ex)
        {
            StatusText = $"启动失败: {ex.Message}";
        }
    }

    /// <summary>停止屏幕捕获</summary>
    [RelayCommand]
    private void StopCapture()
    {
        if (!IsCapturing) return;

        try
        {
            _screenCapture?.Stop();
            _screenCapture?.Dispose();
            _screenCapture = null;

            IsCapturing = false;
            PreviewBitmap = null;
            HasPreview = false;
            StatusText = "已停止";
        }
        catch (Exception ex)
        {
            StatusText = $"停止失败: {ex.Message}";
        }
    }

    private void OnFrameCaptured(Object? sender, VideoFrame frame)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var bgra = VideoFrameRenderer.ToBgra32(frame);
                var bitmap = PreviewBitmap;

                if (bitmap == null || bitmap.PixelSize.Width != frame.Width || bitmap.PixelSize.Height != frame.Height)
                {
                    bitmap = new WriteableBitmap(
                        new PixelSize(frame.Width, frame.Height),
                        new Vector(96, 96),
                        PixelFormat.Bgra8888,
                        AlphaFormat.Opaque);
                    PreviewBitmap = bitmap;
                    HasPreview = true;
                }

                using var locked = bitmap.Lock();
                unsafe
                {
                    var dst = (Byte*)locked.Address;
                    fixed (Byte* pSrc = bgra)
                    {
                        System.Buffer.MemoryCopy(pSrc, dst, bgra.Length, bgra.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText = $"渲染错误: {ex.Message}";
            }
        });
    }
}

/// <summary>分辨率选项</summary>
public class ResolutionOption
{
    public String Name { get; }
    public Int32 Width { get; }
    public Int32 Height { get; }

    public ResolutionOption(String name, Int32 width, Int32 height)
    {
        Name = name;
        Width = width;
        Height = height;
    }

    public override String ToString() => Name;
}
