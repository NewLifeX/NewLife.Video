using System.Runtime.InteropServices;

namespace NewLife.Video.Devices;

/// <summary>屏幕捕获后端。截取屏幕画面作为视频帧</summary>
/// <remarks>
/// Windows: Graphics.CopyFromScreen (net461 原生 / netcore 通过反射调用 System.Drawing.Common)
/// Linux: import(ImageMagick) / gnome-screenshot 命令行回退
/// macOS: screencapture 命令行回退
/// 支持多显示器枚举和区域截取。
/// </remarks>
public class ScreenCaptureBackend : IVideoBackend
{
    /// <summary>后端名称</summary>
    public String Name => "ScreenCapture";

    /// <summary>枚举屏幕捕获源</summary>
    public VideoDeviceInfo[] EnumerateDevices()
    {
        var screens = GetScreenInfos();
        if (screens.Length == 0)
        {
            return
            [
                new VideoDeviceInfo
                {
                    Name = "Screen Capture",
                    DeviceId = "screen:0",
                    BackendType = "ScreenCapture",
                    SupportedFormats = [new VideoCaptureFormat { Width = 1920, Height = 1080, MaxFrameRate = 30, PixelFormat = VideoTypes.BGRA32 }],
                },
            ];
        }

        var devices = new VideoDeviceInfo[screens.Length];
        for (var i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            devices[i] = new VideoDeviceInfo
            {
                Name = s.IsPrimary ? $"Screen {i + 1} (Primary)" : $"Screen {i + 1}",
                DeviceId = $"screen:{i}",
                BackendType = "ScreenCapture",
                SupportedFormats = [new VideoCaptureFormat { Width = s.Width, Height = s.Height, MaxFrameRate = 60, PixelFormat = VideoTypes.BGRA32 }],
            };
        }

        return devices;
    }

    /// <summary>创建屏幕捕获设备</summary>
    public IVideoCaptureDevice CreateCaptureDevice(String deviceId, Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat)
    {
        if (String.IsNullOrEmpty(deviceId) || deviceId == "screen:0")
        {
            var screens = GetScreenInfos();
            if (screens.Length > 0)
            {
                var s = screens[0];
                return new ScreenCaptureDevice(s.X, s.Y, s.Width, s.Height,
                    width > 0 ? width : s.Width, height > 0 ? height : s.Height, frameRate, pixelFormat, "screen:0");
            }
            return new ScreenCaptureDevice(0, 0, width, height, width, height, frameRate, pixelFormat, "screen:0");
        }

        if (deviceId.StartsWith("screen:"))
        {
            var screens = GetScreenInfos();
            var index = Int32.Parse(deviceId.Substring(7));
            if (screens.Length > 0 && index < screens.Length)
            {
                var s = screens[index];
                return new ScreenCaptureDevice(s.X, s.Y, s.Width, s.Height,
                    width > 0 ? width : s.Width, height > 0 ? height : s.Height, frameRate, pixelFormat, deviceId);
            }
        }

        return null;
    }

    private ScreenInfo[] GetScreenInfos()
    {
        try
        {
#if NETFRAMEWORK
            var screens = System.Windows.Forms.Screen.AllScreens;
            var infos = new ScreenInfo[screens.Length];
            for (var i = 0; i < screens.Length; i++)
            {
                var b = screens[i].Bounds;
                infos[i] = new ScreenInfo { X = b.X, Y = b.Y, Width = b.Width, Height = b.Height, IsPrimary = screens[i].Primary };
            }
            return infos;
#else
            // .NET Core: 尝试反射 System.Drawing.Common 获取屏幕信息
            try
            {
                var screenType = Type.GetType("System.Windows.Forms.Screen, System.Windows.Forms");
                if (screenType != null)
                {
                    var allMethod = screenType.GetMethod("AllScreens");
                    var screens = (Array)allMethod.Invoke(null, null);
                    var infos = new ScreenInfo[screens.Length];
                    for (var i = 0; i < screens.Length; i++)
                    {
                        var s = screens.GetValue(i);
                        var boundsProp = s.GetType().GetProperty("Bounds");
                        var primaryProp = s.GetType().GetProperty("Primary");
                        var bounds = boundsProp.GetValue(s);
                        var x = (Int32)bounds.GetType().GetProperty("X").GetValue(bounds);
                        var y = (Int32)bounds.GetType().GetProperty("Y").GetValue(bounds);
                        var w = (Int32)bounds.GetType().GetProperty("Width").GetValue(bounds);
                        var h = (Int32)bounds.GetType().GetProperty("Height").GetValue(bounds);
                        infos[i] = new ScreenInfo { X = x, Y = y, Width = w, Height = h, IsPrimary = (Boolean)primaryProp.GetValue(s) };
                    }
                    return infos;
                }
            }
            catch { }

            return [new ScreenInfo { Width = 1920, Height = 1080, IsPrimary = true }];
#endif
        }
        catch { return []; }
    }

    private struct ScreenInfo
    {
        public Int32 X, Y, Width, Height;
        public Boolean IsPrimary;
    }
}

/// <summary>屏幕捕获设备。Windows/跨平台截屏实现</summary>
internal class ScreenCaptureDevice : IVideoCaptureDevice
{
    private Boolean _capturing;
    private Boolean _disposed;
    private System.Threading.Timer _timer;
    private readonly Int32 _srcX, _srcY, _srcW, _srcH;
    private readonly String _deviceId;

    public String Name => "Screen Capture";
    public String DeviceId => _deviceId;
    public Int32 VideoWidth { get; }
    public Int32 VideoHeight { get; }
    public Double FrameRate { get; }
    public VideoTypes PixelFormat { get; }
    public IReadOnlyList<VideoCaptureFormat> SupportedFormats => [];
    public Boolean IsCapturing => _capturing;

    public event EventHandler<VideoFrame> FrameCaptured;

    public ScreenCaptureDevice(Int32 srcX, Int32 srcY, Int32 srcW, Int32 srcH,
        Int32 outW, Int32 outH, Double fps, VideoTypes fmt, String deviceId)
    {
        _srcX = srcX; _srcY = srcY; _srcW = srcW; _srcH = srcH;
        VideoWidth = outW; VideoHeight = outH;
        FrameRate = fps; PixelFormat = fmt;
        _deviceId = deviceId ?? "screen:0";
    }

    public void StartCapture() => StartCapture(VideoWidth, VideoHeight, FrameRate, PixelFormat);

    public void StartCapture(Int32 width, Int32 height, Double frameRate, VideoTypes pixelFormat)
    {
        _capturing = true;
        var interval = (Int32)(1000.0 / frameRate);
        if (interval < 10) interval = 10;

        _timer = new System.Threading.Timer(_ =>
        {
            if (!_capturing || _timer == null) return;
            try
            {
                var frame = CaptureFrame();
                if (frame != null)
                {
                    frame.Timestamp = DateTime.Now.Ticks / 10000;
                    FrameCaptured?.Invoke(this, frame);
                }
            }
            catch { }
        }, null, interval, interval);
    }

    private VideoFrame CaptureFrame()
    {
        var frame = VideoFrame.Create(VideoWidth, VideoHeight, VideoTypes.BGRA32);

#if NETFRAMEWORK
        // Windows 原生截屏
        using var bmp = new System.Drawing.Bitmap(_srcW, _srcH);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.CopyFromScreen(_srcX, _srcY, 0, 0, new System.Drawing.Size(_srcW, _srcH));
        var r = new System.Drawing.Rectangle(0, 0, _srcW, _srcH);
        var bd = bmp.LockBits(r, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < Math.Min(VideoHeight, _srcH); y++)
            {
                Marshal.Copy(bd.Scan0 + y * bd.Stride, frame.Data, y * VideoWidth * 4, Math.Min(VideoWidth * 4, _srcW * 4));
            }
        }
        finally { bmp.UnlockBits(bd); }
#else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (TryReflectionCapture(frame)) return frame;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (TryLinuxCapture(frame)) return frame;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (TryMacCapture(frame)) return frame;
        }

        // 回退灰色帧
        for (var i = 0; i < frame.Data.Length; i++) frame.Data[i] = 128;
#endif
        return frame;
    }

#if !NETFRAMEWORK
    private Boolean TryReflectionCapture(VideoFrame frame)
    {
        try
        {
            var bmpType = Type.GetType("System.Drawing.Bitmap, System.Drawing.Common");
            var gfxType = Type.GetType("System.Drawing.Graphics, System.Drawing.Common");
            if (bmpType == null || gfxType == null) return false;

            var bmp = Activator.CreateInstance(bmpType, [_srcW, _srcH]);
            var g = gfxType.GetMethod("FromImage", [bmpType]).Invoke(null, [bmp]);
            var sizeType = Type.GetType("System.Drawing.Size, System.Drawing.Common");
            var size = Activator.CreateInstance(sizeType, [_srcW, _srcH]);
            var cfsMethod = gfxType.GetMethod("CopyFromScreen", [typeof(Int32), typeof(Int32), typeof(Int32), typeof(Int32), sizeType]);
            cfsMethod.Invoke(g, [_srcX, _srcY, 0, 0, size]);

            var rectType = Type.GetType("System.Drawing.Rectangle, System.Drawing.Common");
            var lmType = Type.GetType("System.Drawing.Imaging.ImageLockMode, System.Drawing.Common");
            var pfType = Type.GetType("System.Drawing.Imaging.PixelFormat, System.Drawing.Common");
            var rect = Activator.CreateInstance(rectType, [0, 0, _srcW, _srcH]);
            var lockMode = Enum.Parse(lmType, "ReadOnly");
            var pf = Enum.Parse(pfType, "Format32bppArgb");

            var lb = bmpType.GetMethod("LockBits", [rectType, lmType, pfType]);
            var bd = lb.Invoke(bmp, [rect, lockMode, pf]);
            var scan0 = (IntPtr)bd.GetType().GetProperty("Scan0").GetValue(bd);
            var stride = (Int32)bd.GetType().GetProperty("Stride").GetValue(bd);
            for (var y = 0; y < Math.Min(VideoHeight, _srcH); y++)
                Marshal.Copy(scan0 + y * stride, frame.Data, y * VideoWidth * 4, Math.Min(VideoWidth * 4, _srcW * 4));
            bmpType.GetMethod("UnlockBits", [bd.GetType()]).Invoke(bmp, [bd]);
            g.GetType().GetMethod("Dispose")?.Invoke(g, null);
            bmp.GetType().GetMethod("Dispose")?.Invoke(bmp, null);
            return true;
        }
        catch { return false; }
    }

    private Boolean TryLinuxCapture(VideoFrame frame)
    {
        // 尝试 ImageMagick import 命令
        var tmp = Path.GetTempFileName() + ".bmp";
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("import", $"-window root -resize {VideoWidth}x{VideoHeight}! \"{tmp}\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(3000);
            if (File.Exists(tmp)) return TryReadBmp(tmp, frame);
        }
        catch { }
        finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
        return false;
    }

    private Boolean TryMacCapture(VideoFrame frame)
    {
        var tmp = Path.GetTempFileName() + ".bmp";
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("screencapture", $"-x -t bmp \"{tmp}\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(3000);
            if (File.Exists(tmp)) return TryReadBmp(tmp, frame);
        }
        catch { }
        finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
        return false;
    }

    private Boolean TryReadBmp(String path, VideoFrame frame)
    {
        try
        {
            var data = File.ReadAllBytes(path);
            if (data.Length < 54 || data[0] != 'B' || data[1] != 'M') return false;
            var dataOff = BitConverter.ToInt32(data, 10);
            var w = BitConverter.ToInt32(data, 18);
            var h = Math.Abs(BitConverter.ToInt32(data, 22));
            var bpp = BitConverter.ToInt16(data, 28);
            var topDown = BitConverter.ToInt32(data, 22) < 0;
            var rowSize = ((w * bpp + 31) / 32) * 4;
            for (var y = 0; y < Math.Min(VideoHeight, h); y++)
            {
                var sy = topDown ? y : h - 1 - y;
                var srcOff = dataOff + sy * rowSize;
                var dstOff = y * VideoWidth * 4;
                var cl = Math.Min(VideoWidth * (bpp / 8), rowSize);
                if (srcOff + cl <= data.Length && dstOff + cl <= frame.Data.Length)
                    Array.Copy(data, srcOff, frame.Data, dstOff, cl);
            }
            return true;
        }
        catch { return false; }
    }
#endif

    public void StopCapture() { _capturing = false; _timer?.Dispose(); _timer = null; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopCapture();
    }
}
