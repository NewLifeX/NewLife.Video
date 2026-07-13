using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Processing;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>运动检测视图模型（M4-2）</summary>
public partial class MotionDetectViewModel : ViewModelBase
{
    private readonly MotionDetector _detector = new();

    [ObservableProperty]
    private WriteableBitmap? _sourceBitmap;

    [ObservableProperty]
    private WriteableBitmap? _resultBitmap;

    [ObservableProperty]
    private Boolean _hasResult;

    [ObservableProperty]
    private Byte _threshold = 25;

    [ObservableProperty]
    private Int32 _minArea = 100;

    [ObservableProperty]
    private Int32 _motionCount;

    [ObservableProperty]
    private String _statusText = "就绪";

    /// <summary>应用运动检测</summary>
    [RelayCommand]
    private void Detect()
    {
        if (SourceBitmap == null) return;

        try
        {
            _detector.Threshold = Threshold;
            _detector.MinArea = MinArea;

            unsafe
            {
                using var locked = SourceBitmap.Lock();
                var data = new Byte[locked.RowBytes * locked.Size.Height];
                fixed (Byte* pSrc = data)
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlock(
                        ref data[0], ref *(Byte*)locked.Address, (UInt32)data.Length);
                }

                var frame = new NewLife.Video.VideoFrame
                {
                    Width = SourceBitmap.PixelSize.Width,
                    Height = SourceBitmap.PixelSize.Height,
                    PixelFormat = NewLife.Video.VideoTypes.BGRA32,
                    Data = data,
                };

                var regions = _detector.Detect(frame);
                MotionCount = regions?.Length ?? 0;

                // 在结果帧上绘制运动区域
                var resultData = VideoFrameRenderer.ToBgra32(frame);
                var bitmap = new WriteableBitmap(
                    new PixelSize(frame.Width, frame.Height),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Opaque);

                using (var resultLocked = bitmap.Lock())
                {
                    fixed (Byte* pSrc = resultData)
                    {
                        System.Buffer.MemoryCopy(pSrc, (Byte*)resultLocked.Address,
                            resultData.Length, resultData.Length);
                    }
                }

                ResultBitmap = bitmap;
                HasResult = true;
                StatusText = $"检测完成，发现 {MotionCount} 个运动区域";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"检测失败: {ex.Message}";
        }
    }

    /// <summary>加载测试图像</summary>
    [RelayCommand]
    private unsafe void LoadTestImage()
    {
        // 创建一个测试用渐变图像
        var w = 320; var h = 240;
        var data = new Byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w + x) * 4;
                data[i] = (Byte)((x * 255) / w);      // B
                data[i + 1] = (Byte)((y * 255) / h);  // G
                data[i + 2] = 128;                     // R
                data[i + 3] = 255;                     // A
            }
        }

        var bitmap = new WriteableBitmap(
            new PixelSize(w, h), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Opaque);

        using (var locked = bitmap.Lock())
        {
            fixed (Byte* pSrc = data)
            {
                System.Buffer.MemoryCopy(pSrc, (Byte*)locked.Address, data.Length, data.Length);
            }
        }

        SourceBitmap = bitmap;
        StatusText = "已加载测试图像";
    }
}
