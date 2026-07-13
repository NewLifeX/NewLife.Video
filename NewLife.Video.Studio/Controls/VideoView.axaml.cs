using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace NewLife.Video.Studio.Controls;

/// <summary>视频渲染控件，将 BGRA32 像素数据渲染到屏幕</summary>
public partial class VideoView : UserControl
{
    private WriteableBitmap? _bitmap;
    private readonly Image _image;

    /// <summary>获取或设置视频的拉伸模式</summary>
    public Stretch Stretch
    {
        get => _image.Stretch;
        set => _image.Stretch = value;
    }

    /// <summary>实例化视频渲染控件</summary>
    public VideoView()
    {
        InitializeComponent();

        _image = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        Content = _image;
    }

    /// <summary>更新视频帧</summary>
    /// <param name="data">BGRA32 像素数据</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="stride">行跨度 (width * 4)</param>
    public void UpdateFrame(Byte[] data, Int32 width, Int32 height, Int32 stride)
    {
        if (data == null || data.Length == 0) return;

        if (_bitmap == null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
            _image.Source = _bitmap;
        }

        using var locked = _bitmap.Lock();
        unsafe
        {
            var dst = (Byte*)locked.Address;
            fixed (Byte* pSrc = data)
            {
                System.Buffer.MemoryCopy(pSrc, dst, data.Length, data.Length);
            }
        }
    }

    /// <summary>清除当前帧</summary>
    public void Clear()
    {
        _bitmap = null;
        _image.Source = null;
    }
}
