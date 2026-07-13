using System.Diagnostics;
using System.Net.Http;

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

    /// <summary>FFmpeg 下载基础 URL。默认使用 GitHub Release，可替换为国内镜像</summary>
    public static String DownloadBaseUrl { get; set; } = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest";

    /// <summary>本地缓存目录</summary>
    public static String CacheDirectory { get; set; }

    /// <summary>自动下载开关（默认关闭，需调用 EnsureAsync 或设为 true）</summary>
    public static Boolean AutoDownload { get; set; } = false;

    /// <summary>已找到的 FFmpeg 路径缓存</summary>
    private static String _cachedPath;

    /// <summary>是否已执行过检测</summary>
    private static Boolean _checked;

    #endregion

    #region 构造

    static FFmpegSetup()
    {
        CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NewLife",
            "ffmpeg"
        );
    }

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

    /// <summary>下载 FFmpeg 到本地缓存目录</summary>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        var isLinux = Environment.OSVersion.Platform == PlatformID.Unix;
        var isMac = Environment.OSVersion.Platform == PlatformID.MacOSX;

        // 确定文件名模板
        String archiveName;
        if (isWindows)
            archiveName = "ffmpeg-master-latest-win64-gpl.zip";
        else if (isLinux)
            archiveName = "ffmpeg-master-latest-linux64-gpl.tar.xz";
        else if (isMac)
            archiveName = "ffmpeg-master-latest-darwin64-gpl.zip";
        else
            throw new PlatformNotSupportedException("不支持的操作系统");

        var url = $"{DownloadBaseUrl}/{archiveName}";
        var archivePath = Path.Combine(CacheDirectory, archiveName);

        // 创建缓存目录
        Directory.CreateDirectory(CacheDirectory);

        // 下载压缩包
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var fileStream = File.Create(archivePath);
        await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);
        await fileStream.FlushAsync().ConfigureAwait(false);

        // 解压（调用系统命令避免引入压缩库依赖）
        var extractDir = Path.Combine(CacheDirectory, "extracted");
        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        Directory.CreateDirectory(extractDir);

        if (isWindows)
        {
            // Windows 使用 PowerShell Expand-Archive
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"Expand-Archive -Path '{archivePath}' -DestinationPath '{extractDir}' -Force\"",
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            var proc = Process.Start(psi);
            proc?.WaitForExit();
        }
        else
        {
            // Linux/macOS 使用 tar
            var psi = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xf \"{archivePath}\" -C \"{extractDir}\"",
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            var proc = Process.Start(psi);
            proc?.WaitForExit();
        }

        // 移动 ffmpeg 可执行文件到缓存根目录
        if (Directory.Exists(extractDir))
        {
            var files = Directory.GetFiles(extractDir, "ffmpeg*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (name.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                {
                    var dest = Path.Combine(CacheDirectory, name);
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(file, dest);
                    break;
                }
            }

            // 清理解压目录
            try { Directory.Delete(extractDir, true); } catch { }
        }

        // 清理压缩包
        try { File.Delete(archivePath); } catch { }
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

        // 1. 检查缓存目录
        var cacheDir = CacheDirectory;
        if (Directory.Exists(cacheDir))
        {
            var files = Directory.GetFiles(cacheDir, "ffmpeg*", SearchOption.AllDirectories);
            var found = files.FirstOrDefault(f =>
            {
                var fn = Path.GetFileName(f);
                return fn.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase) ||
                       fn.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase);
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
