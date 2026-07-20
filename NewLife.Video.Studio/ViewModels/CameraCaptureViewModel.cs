using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Devices;
using NewLife.Video.Processing;
using NewLife.Video.Studio.Services;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>摄像头捕获增强版视图模型（CAPT-2）</summary>
public partial class CameraCaptureViewModel : ViewModelBase
{
    private readonly IDeviceService _deviceService;
    private IVideoCaptureDevice? _captureDevice;

    [ObservableProperty]
    private ObservableCollection<DeviceItem> _devices = [];

    [ObservableProperty]
    private DeviceItem? _selectedDevice;

    [ObservableProperty]
    private WriteableBitmap? _previewBitmap;

    [ObservableProperty]
    private Boolean _hasPreview;

    [ObservableProperty]
    private Boolean _isCapturing;

    [ObservableProperty]
    private Int32 _captureWidth = 640;

    [ObservableProperty]
    private Int32 _captureHeight = 480;

    [ObservableProperty]
    private Int32 _captureFrameRate = 30;

    [ObservableProperty]
    private String _statusText = "就绪";

    /// <summary>可选分辨率</summary>
    public ObservableCollection<ResolutionOption> ResolutionOptions { get; } =
    [
        new("640×480", 640, 480),
        new("800×600", 800, 600),
        new("1024×768", 1024, 768),
        new("1280×720", 1280, 720),
        new("1920×1080", 1920, 1080),
    ];

    /// <summary>可选帧率</summary>
    public ObservableCollection<Int32> FrameRateOptions { get; } = [15, 20, 24, 25, 30, 60];

    [ObservableProperty]
    private ResolutionOption _selectedResolution = new("640×480", 640, 480);

    /// <summary>实例化摄像头捕获视图模型</summary>
    public CameraCaptureViewModel() : this(new DeviceService()) { }

    public CameraCaptureViewModel(IDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    /// <summary>刷新设备列表</summary>
    [RelayCommand]
    private void RefreshDevices()
    {
        StopCapture();
        Devices.Clear();

        try
        {
            var list = _deviceService.EnumerateDevices();
            foreach (var dev in list)
            {
                Devices.Add(new DeviceItem
                {
                    DeviceInfo = dev,
                    Name = dev.Name ?? "未知",
                    DeviceId = dev.DeviceId ?? "",
                    BackendType = dev.BackendType ?? "",
                });
            }
            StatusText = $"找到 {Devices.Count} 个摄像头";
        }
        catch (Exception ex)
        {
            StatusText = $"枚举失败: {ex.Message}";
        }
    }

    /// <summary>开始捕获</summary>
    [RelayCommand]
    private async Task StartCapture()
    {
        if (SelectedDevice?.DeviceInfo == null) return;

        StopCapture();
        StatusText = "正在启动摄像头...";

        await Task.Run(() =>
        {
            try
            {
                var backend = new NewLife.Video.Devices.VideoDeviceManager();
                _captureDevice = backend.OpenDevice(
                    SelectedDevice.DeviceId,
                    SelectedResolution.Width,
                    SelectedResolution.Height,
                    CaptureFrameRate,
                    VideoTypes.YUV420P);

                if (_captureDevice != null)
                {
                    _captureDevice.FrameCaptured += OnFrameCaptured;
                    _captureDevice.StartCapture();
                    IsCapturing = true;
                }
            }
            catch (Exception ex)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    StatusText = $"启动失败: {ex.Message}");
            }
        });

        if (IsCapturing)
            StatusText = $"正在捕获: {SelectedDevice.Name}";
    }

    /// <summary>停止捕获</summary>
    [RelayCommand]
    private void StopCapture()
    {
        if (_captureDevice != null)
        {
            _captureDevice.FrameCaptured -= OnFrameCaptured;
            _captureDevice.StopCapture();
            _captureDevice.Dispose();
            _captureDevice = null;
        }
        IsCapturing = false;
        HasPreview = false;
        PreviewBitmap = null;
        StatusText = "已停止";
    }

    /// <summary>拍照快照</summary>
    [RelayCommand]
    private void TakeSnapshot()
    {
        if (PreviewBitmap == null) return;

        try
        {
            var dir = "Snapshots";
            Directory.CreateDirectory(dir);
            var fileName = Path.Combine(dir, $"snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            PreviewBitmap.Save(fileName);
            StatusText = $"快照已保存: {fileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"保存失败: {ex.Message}";
        }
    }

    private void OnFrameCaptured(Object? sender, NewLife.Video.VideoFrame frame)
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
