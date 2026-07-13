using System.Diagnostics;
using NewLife.Log;
using NewLife.Web;

namespace NewLife.Video.FFmpeg;

/// <summary>FFmpeg 运行环境管理。自动检测、下载和缓存 FFmpeg 可执行文件</summary>
/// <remarks>
/// 提供一键式 FFmpeg 运行环境管理：
/// <code>
/// // 确保 FFmpeg 可用（自动检测，可选下载）
/// if (!FFmpegSetup.IsAvailable())
///     await FFmpegSetup.EnsureAsync();
///
/// // 获取 ffmpeg 路径
/// var ffmpeg = FFmpegSetup.GetFFmpegPath();
/// </code>
/// </remarks>
public static class FFmpegSetup
{
    #region 属性

    /// <summary>插件服务器地址。默认使用 NewLife.Setting.Current.PluginServer，从该页面上根据关键字分析链接并下载 FFmpeg</summary>
    public static String DownloadBaseUrl { get; set; } = NewLife.Setting.Current.PluginServer;

    /// <summary>FFmpeg 目录。可指定自定义路径，默认使用插件目录下的 ffmpeg 子目录</summary>
    public static String FFmpegDirectory
    {
        get => _ffmpegDirectory ?? Path.Combine(NewLife.Setting.Current.GetPluginPath(), "ffmpeg");
        set => _ffmpegDirectory = value;
    }
    private static String _ffmpegDirectory;

    /// <summary>本地缓存目录。默认指向 FFmpegDirectory</summary>
    public static String CacheDirectory
    {
        get => _cacheDirectory ?? FFmpegDirectory;
        set => _cacheDirectory = value;
    }
    private static String _cacheDirectory;

    /// <summary>已找到的 FFmpeg 路径缓存</summary>
    private static String _cachedPath;

    /// <summary>是否已执行过检测</summary>
    private static Boolean _checked;

    #endregion

    #region 方法

    /// <summary>检测 FFmpeg 是否可用</summary>
    /// <returns>是否可用</returns>
    public static Boolean IsAvailable()
    {
        if (!_checked)
        {
            _cachedPath = FindFFmpeg();
            _checked = true;
        }
        return _cachedPath != null;
    }

    /// <summary>获取 FFmpeg 可执行文件路径</summary>
    /// <returns>完整路径，未找到时返回 null</returns>
    public static String GetFFmpegPath()
    {
        if (!_checked) IsAvailable();
        return _cachedPath;
    }

    /// <summary>确保 FFmpeg 可用。先检测系统路径，若未找到则自动下载</summary>
    /// <param name="source">下载源 URL（覆盖 DownloadBaseUrl）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>FFmpeg 路径，失败时返回 null</returns>
    public static async Task<String> EnsureAsync(String source = null, CancellationToken cancellationToken = default)
    {
        if (IsAvailable()) return _cachedPath;

        if (!String.IsNullOrEmpty(source))
            DownloadBaseUrl = source;

        await DownloadAsync(cancellationToken).ConfigureAwait(false);

        // 下载后重新检测
        _checked = false;
        return IsAvailable() ? _cachedPath : null;
    }

    /// <summary>从插件服务器下载 FFmpeg</summary>
    /// <param name="cancellationToken">取消令牌</param>
    public static Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        var linkName = GetPlatformLinkName();
        if (linkName == null)
            throw new PlatformNotSupportedException("不支持的操作系统");

        var dir = FFmpegDirectory;
        Directory.CreateDirectory(dir);

        // 临时解压目录
        var extractDir = Path.Combine(dir, "_tmp");
        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        Directory.CreateDirectory(extractDir);

        XTrace.WriteLine("从插件服务器下载 {0} ...", linkName);

        using var client = new WebClientX { AuthKey = "NewLife", Log = XTrace.Log };
        var file = client.DownloadLinkAndExtract(DownloadBaseUrl, linkName, extractDir, true);

        // 在解压结果中搜索 ffmpeg 可执行文件并移动到目标目录
        if (!file.IsNullOrEmpty() && Directory.Exists(extractDir))
        {
            var files = Directory.GetFiles(extractDir, "ffmpeg*", SearchOption.AllDirectories);
            foreach (var fi in files)
            {
                var name = Path.GetFileName(fi);
                if (name.EqualIgnoreCase("ffmpeg", "ffmpeg.exe"))
                {
                    var dest = Path.Combine(dir, name);
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(fi, dest);
                    XTrace.WriteLine("FFmpeg 下载完成: {0}", dest);
                    break;
                }
            }

            // 清理临时目录
            try { Directory.Delete(extractDir, true); } catch { }
        }

        // 清除缓存标记，下次检测重新查找
        _checked = false;

        return Task.CompletedTask;
    }

    /// <summary>获取当前平台的链接名称</summary>
    private static String GetPlatformLinkName()
    {
        if (Runtime.Windows) return "ffmpeg-win64";
        if (Runtime.Linux) return "ffmpeg-linux64";
        if (Runtime.OSX) return "ffmpeg-darwin64";
        return null;
    }

    /// <summary>清除缓存的 FFmpeg 路径，下次调用 IsAvailable 时重新检测</summary>
    public static void Reset()
    {
        _cachedPath = null;
        _checked = false;
    }

    #endregion

    #region 辅助

    /// <summary>查找系统中的 FFmpeg 可执行文件</summary>
    private static String FindFFmpeg()
    {
        var name = "ffmpeg";

        // 1. 检查 FFmpeg 目录
        var dir = FFmpegDirectory;
        if (Directory.Exists(dir))
        {
            var files = Directory.GetFiles(dir, "ffmpeg*", SearchOption.AllDirectories);
            var found = files.FirstOrDefault(f =>
            {
                var fn = Path.GetFileName(f);
                return fn.EqualIgnoreCase("ffmpeg", "ffmpeg.exe");
            });
            if (found != null) return found;
        }

        // 2. 当前目录
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var localPath = Path.Combine(baseDir, name);
        if (File.Exists(localPath)) return localPath;
        if (File.Exists(localPath + ".exe")) return localPath + ".exe";

        // 3. 通过 PATH 查找
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "ffmpeg",
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadLine();
                proc.WaitForExit();
                if (!String.IsNullOrEmpty(output) && File.Exists(output))
                    return output;
            }
        }
        catch
        {
            // where 命令可能不存在
        }

        // Linux/macOS 下通过 which 查找
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "ffmpeg",
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadLine();
                proc.WaitForExit();
                if (!String.IsNullOrEmpty(output) && File.Exists(output))
                    return output;
            }
        }
        catch
        {
            // which 命令可能不存在
        }

        // 4. 常见安装路径
        var commonPaths = new[]
        {
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            "/usr/local/bin/ffmpeg",
            "/usr/bin/ffmpeg",
            "/opt/homebrew/bin/ffmpeg",
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path)) return path;
        }

        return null;
    }

    #endregion
}
