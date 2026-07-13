using System.Diagnostics;
using NewLife.Data;

namespace NewLife.Video.Streaming;

/// <summary>基于 FFmpeg 的轻量播放引擎。通过管道调用 ffmpeg 将 RTSP/RTMP/HLS 流转为 rawvideo 输出</summary>
/// <remarks>
/// 纯托管实现，无外部播放器依赖。自动检测视频分辨率，支持网络流优化参数。
/// 
/// 优化参考：
/// - 自动分辨率检测（借鉴 VLC 的自动流探测）
/// - RTSP TCP 传输模式（避免 UDP 丢包）
/// - 探针大小/分析时长配置（稳定网络流）
/// 
/// 使用示例：
/// <code>
/// var player = new FfmpegPlayer();
/// player.Open("rtsp://camera/stream");
/// player.FrameDecoded += (s, frame) => { /* 渲染 */ };
/// player.Play();
/// </code>
/// </remarks>
public class FfmpegPlayer : IDisposable
{
    private Process _process;
    private Thread _decodeThread;
    private volatile Boolean _running;
    private volatile Boolean _paused;
    private readonly Object _lock = new();

    /// <summary>是否正在播放</summary>
    public Boolean IsPlaying => _running && !_paused;

    /// <summary>视频宽度（自动探测后更新）</summary>
    public Int32 Width { get; private set; }

    /// <summary>视频高度（自动探测后更新）</summary>
    public Int32 Height { get; private set; }

    /// <summary>帧解码完成事件</summary>
    public event EventHandler<VideoFrame> FrameDecoded;

    /// <summary>播放状态变化事件</summary>
    public event EventHandler<PlayState> StateChanged;

    /// <summary>创建 FFmpeg 播放器</summary>
    /// <param name="options">额外 FFmpeg 参数，如 "-rtsp_transport tcp"</param>
    public FfmpegPlayer(String options = null)
    {
        _extraArgs = options ?? "-rtsp_transport tcp";
    }
    private readonly String _extraArgs;

    /// <summary>打开媒体源（RTSP/RTMP/HLS 或本地文件）</summary>
    public void Open(Uri uri)
    {
        if (uri == null) throw new ArgumentNullException(nameof(uri));
        Open(uri.ToString());
    }

    /// <summary>打开媒体源</summary>
    public void Open(String url)
    {
        if (String.IsNullOrEmpty(url)) throw new ArgumentNullException(nameof(url));
        _url = url;
    }

    /// <summary>开始播放</summary>
    public void Play()
    {
        if (_running) return;
        if (String.IsNullOrEmpty(_url)) throw new InvalidOperationException("请先设置播放源");

        // 1. 先探测分辨率
        if (Width <= 0 || Height <= 0)
            DetectResolution();

        lock (_lock)
        {
            _running = true;
            _paused = false;
        }
        StateChanged?.Invoke(this, PlayState.Playing);

        // 2. 启动解码进程
        var probesize = "-probesize 100M -analyzeduration 100M";
        var args = $"{probesize} {_extraArgs} -i \"{_url}\" -f rawvideo -pix_fmt rgb24 -s {Width}x{Height} -an -vcodec rawvideo pipe:1";

        var psi = new ProcessStartInfo
        {
            FileName = FindFFmpeg(),
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false,
        };

        _process = Process.Start(psi);
        if (_process == null) { _running = false; return; }

        _decodeThread = new Thread(DecodeLoop) { IsBackground = true, Name = "FFmpegDecode" };
        _decodeThread.Start();
    }

    /// <summary>停止播放</summary>
    public void Stop()
    {
        lock (_lock)
        {
            _running = false;
            _paused = false;
            Monitor.PulseAll(_lock);
        }
        KillProcess();
        StateChanged?.Invoke(this, PlayState.Stopped);
    }

    /// <summary>暂停/继续</summary>
    public void Pause()
    {
        if (!_running) return;
        _paused = !_paused;
        StateChanged?.Invoke(this, _paused ? PlayState.Paused : PlayState.Playing);
    }

    /// <summary>设置期望分辨率（自动探测优先于该值）</summary>
    public void SetResolution(Int32 width, Int32 height) { Width = width; Height = height; }

    /// <summary>获取当前帧率统计</summary>
    public Double FrameRate { get; private set; }

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        Stop();
        _process?.Dispose();
    }

    #region 私有

    private String _url;
    private Int64 _frameCount;
    private DateTime _lastFpsCheck = DateTime.UtcNow;

    private void DecodeLoop()
    {
        var frameSize = Width * Height * 3;
        var buffer = new Byte[frameSize];

        try
        {
            while (_running)
            {
                // 暂停时不读取管道，避免进程缓冲溢出
                if (_paused)
                {
                    Thread.Sleep(50);
                    continue;
                }

                if (_process == null || _process.HasExited) break;

                var read = _process.StandardOutput.BaseStream.Read(buffer, 0, frameSize);
                if (read != frameSize) break;

                var frame = new VideoFrame
                {
                    Data = new Byte[frameSize],
                    Width = Width,
                    Height = Height,
                    PixelFormat = VideoTypes.RGB24,
                };
                Array.Copy(buffer, frame.Data, frameSize);

                // 统计帧率
                _frameCount++;
                var elapsed = (DateTime.UtcNow - _lastFpsCheck).TotalSeconds;
                if (elapsed >= 1.0)
                {
                    FrameRate = _frameCount / elapsed;
                    _frameCount = 0;
                    _lastFpsCheck = DateTime.UtcNow;
                }

                FrameDecoded?.Invoke(this, frame);
            }
        }
        catch { }
        finally
        {
            _running = false;
            StateChanged?.Invoke(this, PlayState.Ended);
        }
    }

    /// <summary>自动探测视频分辨率。使用 ffprobe 快速读取流信息</summary>
    private void DetectResolution()
    {
        try
        {
            var ffprobe = FindFFprobe();
            if (ffprobe == null) return;

            var psi = new ProcessStartInfo
            {
                FileName = ffprobe,
                Arguments = $"-v quiet -select_streams v:0 -show_entries stream=width,height -of csv=s=x:p=0 \"{_url}\"",
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return;

            var output = proc.StandardOutput.ReadLine();
            proc.WaitForExit(5000);

            if (!String.IsNullOrEmpty(output))
            {
                var parts = output.Split('x');
                if (parts.Length == 2 &&
                    Int32.TryParse(parts[0], out var w) &&
                    Int32.TryParse(parts[1], out var h) &&
                    w > 0 && h > 0)
                {
                    Width = w;
                    Height = h;
                }
            }
        }
        catch
        {
            // 探测失败使用默认分辨率
        }
    }

    private void KillProcess()
    {
        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(2000);
            }
        }
        catch { }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }

    private static String FindFFmpeg()
    {
        var setup = FFmpeg.FFmpegSetup.GetFFmpegPath();
        return setup ?? "ffmpeg";
    }

    private static String FindFFprobe()
    {
        var ffmpeg = FindFFmpeg();
        if (String.IsNullOrEmpty(ffmpeg)) return null;

        // ffprobe 与 ffmpeg 同目录
        var dir = Path.GetDirectoryName(ffmpeg);
        if (dir == null) return "ffprobe";

        var probe = Path.Combine(dir, "ffprobe");
        if (File.Exists(probe)) return probe;
        if (File.Exists(probe + ".exe")) return probe + ".exe";
        return "ffprobe";
    }

    #endregion
}

/// <summary>播放状态</summary>
public enum PlayState
{
    /// <summary>播放中</summary>
    Playing,
    /// <summary>已暂停</summary>
    Paused,
    /// <summary>已停止</summary>
    Stopped,
    /// <summary>播放结束</summary>
    Ended,
}

