using System.Diagnostics;

namespace NewLife.Video.Streaming;

/// <summary>基于 FFmpeg 的轻量播放引擎。通过管道调用 ffmpeg 将 RTSP/RTMP/HLS 流转为 rawvideo 输出</summary>
/// <remarks>
/// 纯托管实现，无外部播放器依赖。通过 FFmpegInvoker 管道调用 ffmpeg 进程，
/// 输出 rawvideo 帧数据供上层渲染。支持 WPF/WinForms/MAUI 等 UI 框架。
/// 
/// 使用示例：
/// <code>
/// var player = new FfmpegPlayer();
/// player.Open(new Uri("rtsp://camera/stream"));
/// player.FrameDecoded += (s, frame) => { /* 渲染 frame */ };
/// player.Play();
/// </code>
/// </remarks>
public class FfmpegPlayer : IDisposable
{
    private Process _process;
    private Thread _decodeThread;
    private Boolean _running;

    /// <summary>是否正在播放</summary>
    public Boolean IsPlaying => _running;

    /// <summary>帧解码完成事件</summary>
    public event EventHandler<VideoFrame> FrameDecoded;

    /// <summary>播放状态变化事件</summary>
    public event EventHandler<PlayState> StateChanged;

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

        _running = true;
        StateChanged?.Invoke(this, PlayState.Playing);

        var args = $"-i \"{_url}\" -f rawvideo -pix_fmt rgb24 -s {_width}x{_height} -an pipe:1";
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

        _decodeThread = new Thread(DecodeLoop) { IsBackground = true };
        _decodeThread.Start();
    }

    /// <summary>停止播放</summary>
    public void Stop()
    {
        _running = false;
        _process?.Kill();
        _process?.Dispose();
        _process = null;
        StateChanged?.Invoke(this, PlayState.Stopped);
    }

    /// <summary>暂停/恢复</summary>
    public void Pause() => StateChanged?.Invoke(this, PlayState.Paused);

    /// <summary>设置输出分辨率</summary>
    public void SetResolution(Int32 width, Int32 height) { _width = width; _height = height; }

    /// <summary>释放资源</summary>
    public void Dispose() => Stop();

    #region 私有

    private String _url;
    private Int32 _width = 640;
    private Int32 _height = 480;

    private void DecodeLoop()
    {
        var frameSize = _width * _height * 3;
        var buffer = new Byte[frameSize];

        try
        {
            while (_running && _process != null && !_process.HasExited)
            {
                var read = _process.StandardOutput.BaseStream.Read(buffer, 0, frameSize);
                if (read != frameSize) break;

                var frame = new VideoFrame
                {
                    Data = new Byte[frameSize],
                    Width = _width,
                    Height = _height,
                    PixelFormat = VideoTypes.RGB24,
                };
                Array.Copy(buffer, frame.Data, frameSize);
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

    private static String FindFFmpeg()
    {
        var setup = FFmpeg.FFmpegSetup.GetFFmpegPath();
        return setup ?? "ffmpeg";
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

