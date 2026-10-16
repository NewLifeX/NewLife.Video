using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Devices;
using NewLife.Video.Processing;
using NewLife.Video.Studio.Helpers;
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
        var path = await FileDialogHelper.PickFileAsync("选择图像文件", FileDialogHelper.ImageFileType);
        if (String.IsNullOrEmpty(path)) return;

        try
        {
            using var stream = File.OpenRead(path);
            using var decoded = WriteableBitmap.Decode(stream);
            SourceBitmap = EnsureBgra32(decoded);
            HasSource = true;
            StatusText = $"已加载: {Path.GetFileName(path)} ({decoded.PixelSize.Width}x{decoded.PixelSize.Height})";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
        }
    }

    /// <summary>将解码位图复制到 BGRA32 目标位图，供处理管线读取；已是 BGRA32 时直接复用</summary>
    private static WriteableBitmap EnsureBgra32(WriteableBitmap source)
    {
        using var srcLock = source.Lock();
        if (srcLock.Format == PixelFormat.Bgra8888) return source;

        var width = srcLock.Size.Width;
        var height = srcLock.Size.Height;
        var target = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        unsafe
        {
            var src = (Byte*)srcLock.Address;
            using var dstLock = target.Lock();
            var dst = (Byte*)dstLock.Address;
            for (var y = 0; y < height; y++)
            {
                var s = src + y * srcLock.RowBytes;
                var d = dst + y * dstLock.RowBytes;
                for (var x = 0; x < width; x++)
                {
                    // 源格式为 RGBA，交换 R/B 通道
                    d[x * 4 + 0] = s[x * 4 + 2];
                    d[x * 4 + 1] = s[x * 4 + 1];
                    d[x * 4 + 2] = s[x * 4 + 0];
                    d[x * 4 + 3] = 0xFF;
                }
            }
        }

        return target;
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
