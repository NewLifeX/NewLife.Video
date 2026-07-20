using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Devices;
using NewLife.Video.Studio.Services;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>设备管理器视图模型（CAPT-1）</summary>
public partial class DeviceManagerViewModel : ViewModelBase
{
    private readonly IDeviceService _deviceService;

    [ObservableProperty]
    private ObservableCollection<DeviceItem> _devices = [];

    [ObservableProperty]
    private DeviceItem? _selectedDevice;

    [ObservableProperty]
    private Boolean _isCapturing;

    [ObservableProperty]
    private Boolean _hasDevice;

    [ObservableProperty]
    private WriteableBitmap? _previewBitmap;

    [ObservableProperty]
    private Boolean _hasPreview;

    [ObservableProperty]
    private String _statusText = "就绪";

    /// <summary>实例化设备管理器视图模型</summary>
    public DeviceManagerViewModel() : this(new DeviceService()) { }

    /// <summary>实例化设备管理器视图模型（含依赖注入）</summary>
    public DeviceManagerViewModel(IDeviceService deviceService)
    {
        _deviceService = deviceService;
        _deviceService.FrameCaptured += OnFrameCaptured;
    }

    /// <summary>刷新设备列表</summary>
    [RelayCommand]
    private void RefreshDevices()
    {
        StatusText = "正在枚举设备...";
        Devices.Clear();

        try
        {
            var list = _deviceService.EnumerateDevices();
            foreach (var device in list)
            {
                Devices.Add(new DeviceItem
                {
                    DeviceInfo = device,
                    Name = device.Name ?? "未知设备",
                    DeviceId = device.DeviceId ?? "",
                    BackendType = device.BackendType ?? "",
                });
            }
            StatusText = $"找到 {Devices.Count} 个设备";
        }
        catch (Exception ex)
        {
            StatusText = $"枚举失败: {ex.Message}";
        }
    }

    /// <summary>打开选中设备</summary>
    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedDevice?.DeviceInfo == null) return;

        _deviceService.OpenDevice(SelectedDevice.DeviceId, SelectedDevice.Name);
        HasDevice = true;
        StatusText = $"已打开: {SelectedDevice.Name}";
    }

    /// <summary>开始预览</summary>
    [RelayCommand]
    private void StartPreview()
    {
        if (!HasDevice) return;

        _deviceService.StartCapture();
        IsCapturing = true;
        StatusText = "预览中...";
    }

    /// <summary>停止预览</summary>
    [RelayCommand]
    private void StopPreview()
    {
        _deviceService.StopCapture();
        IsCapturing = false;
        PreviewBitmap = null;
        StatusText = "已停止";
    }

    private void OnFrameCaptured(Object? sender, (Byte[] Data, Int32 Width, Int32 Height, Int32 Stride) e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var bitmap = PreviewBitmap;
                if (bitmap == null || bitmap.PixelSize.Width != e.Width || bitmap.PixelSize.Height != e.Height)
                {
                    bitmap = new WriteableBitmap(
                        new PixelSize(e.Width, e.Height),
                        new Vector(96, 96),
                        PixelFormat.Bgra8888,
                        AlphaFormat.Opaque);
                    PreviewBitmap = bitmap;
                    HasPreview = true;
                }

                using var locked = bitmap.Lock();
                unsafe
                {
                    var src = e.Data;
                    var dst = (Byte*)locked.Address;
                    fixed (Byte* pSrc = src)
                    {
                        System.Buffer.MemoryCopy(pSrc, dst, src.Length, src.Length);
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

/// <summary>设备列表项</summary>
public class DeviceItem
{
    public VideoDeviceInfo? DeviceInfo { get; set; }
    public String Name { get; set; } = "";
    public String DeviceId { get; set; } = "";
    public String BackendType { get; set; } = "";
}
