using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Devices;
using NewLife.Video.Processing;
using NewLife.Video.Studio.Models;
using NewLife.Video.Studio.Services;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>帧处理管线视图模型（FRAM-1）</summary>
public partial class ProcessingViewModel : ViewModelBase
{
    private readonly IProcessingService _processingService;

    [ObservableProperty]
    private WriteableBitmap? _sourceBitmap;

    [ObservableProperty]
    private WriteableBitmap? _resultBitmap;

    [ObservableProperty]
    private Boolean _hasSource;

    [ObservableProperty]
    private Boolean _hasResult;

    [ObservableProperty]
    private Int32 _resizeWidth = 640;

    [ObservableProperty]
    private Int32 _resizeHeight = 480;

    [ObservableProperty]
    private Int32 _rotation;

    [ObservableProperty]
    private Boolean _flipHorizontal;

    [ObservableProperty]
    private Boolean _flipVertical;

    [ObservableProperty]
    private Double _brightness;

    [ObservableProperty]
    private Double _contrast;

    [ObservableProperty]
    private Double _saturation;

    [ObservableProperty]
    private String _watermarkText = "";

    [ObservableProperty]
    private String _statusText = "就绪";

    /// <summary>可用旋转角度</summary>
    public ObservableCollection<Int32> RotationOptions { get; } = [0, 90, 180, 270];

    /// <summary>实例化处理视图模型</summary>
    public ProcessingViewModel() : this(new ProcessingService()) { }

    /// <summary>实例化处理视图模型（含依赖注入）</summary>
    public ProcessingViewModel(IProcessingService processingService)
    {
        _processingService = processingService;
    }

    /// <summary>从文件加载图像</summary>
    [RelayCommand]
    private async Task LoadImage()
    {
        // TODO: 文件对话框
        await Task.CompletedTask;
    }

    /// <summary>应用处理</summary>
    [RelayCommand]
    private void ApplyProcessing()
    {
        if (SourceBitmap == null)
        {
            StatusText = "请先加载图像";
            return;
        }

        try
        {
            // 从 WriteableBitmap 构建 VideoFrame
            var sourceFrame = new VideoFrame
            {
                Width = SourceBitmap.PixelSize.Width,
                Height = SourceBitmap.PixelSize.Height,
                PixelFormat = VideoTypes.BGRA32,
            };

            // 读取像素数据
            unsafe
            {
                using var locked = SourceBitmap.Lock();
                var data = new Byte[locked.RowBytes * locked.Size.Height];
                System.Runtime.CompilerServices.Unsafe.CopyBlock(
                    ref data[0],
                    ref *(Byte*)locked.Address,
                    (UInt32)data.Length);
                sourceFrame.Data = data;
            }

            var options = new ProcessingOptions
            {
                ResizeWidth = ResizeWidth,
                ResizeHeight = ResizeHeight,
                Rotation = Rotation,
                FlipHorizontal = FlipHorizontal,
                FlipVertical = FlipVertical,
                Brightness = Brightness,
                Contrast = Contrast,
                Saturation = Saturation,
                WatermarkText = String.IsNullOrEmpty(WatermarkText) ? null : WatermarkText,
            };

            var result = _processingService.ProcessFrame(sourceFrame, options);
            var bgra = VideoFrameRenderer.ToBgra32(result);
            var bitmap = new WriteableBitmap(
                new PixelSize(result.Width, result.Height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);

            unsafe
            {
                using var locked = bitmap.Lock();
                fixed (Byte* pSrc = bgra)
                {
                    Buffer.MemoryCopy(pSrc, (Byte*)locked.Address, bgra.Length, bgra.Length);
                }
            }

            ResultBitmap = bitmap;
            HasResult = true;
            StatusText = "处理完成";
        }
        catch (Exception ex)
        {
            StatusText = $"处理失败: {ex.Message}";
        }
    }

    /// <summary>重置处理</summary>
    [RelayCommand]
    private void ResetProcessing()
    {
        ResizeWidth = 640;
        ResizeHeight = 480;
        Rotation = 0;
        FlipHorizontal = false;
        FlipVertical = false;
        Brightness = 0;
        Contrast = 0;
        Saturation = 0;
        WatermarkText = "";
        ResultBitmap = null;
        HasResult = false;
        StatusText = "已重置";
    }
}
