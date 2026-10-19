using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NewLife.Video.FFmpeg;

/// <summary>FFmpeg 进程封装层。统一管理 ffmpeg/ffprobe 进程调用、管道通信和参数构建</summary>
/// <remarks>
/// 支持自动查找 ffmpeg 可执行文件（PATH、当前目录、指定目录）。
/// 提供同步和异步调用模式，支持标准输入/输出管道。
/// 参考 FFMpegCore 的流式 API 设计，但按 NewLife 风格简化。
/// </remarks>
public class FFmpegInvoker
{
    private String _ffmpegPath;
    private String _ffprobePath;

    /// <summary>FFmpeg 可执行文件路径</summary>
    public String FFmpegPath
    {
        get => _ffmpegPath ??= FindExecutable("ffmpeg");
        set => _ffmpegPath = value;
    }

    /// <summary>FFprobe 可执行文件路径</summary>
    public String FFprobePath
    {
        get => _ffprobePath ??= FindExecutable("ffprobe");
        set => _ffprobePath = value;
    }

    /// <summary>工作目录</summary>
    public String WorkingDirectory { get; set; }

    /// <summary>超时时间（毫秒，0=不限制）</summary>
    public Int32 TimeoutMs { get; set; }

    /// <summary>创建 FFmpeg 调用器</summary>
    /// <param name="ffmpegPath">FFmpeg 路径（null 时自动查找）</param>
    public FFmpegInvoker(String ffmpegPath = null)
    {
        if (ffmpegPath != null) _ffmpegPath = ffmpegPath;
    }

    /// <summary>执行 FFmpeg 命令（同步，无管道）</summary>
    /// <param name="arguments">命令行参数</param>
    /// <returns>执行结果</returns>
    public FFmpegResult Execute(String arguments)
    {
        return ExecuteAsync(arguments, null, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>执行 FFmpeg 命令（异步，可管道输入）</summary>
    /// <param name="arguments">命令行参数</param>
    /// <param name="inputData">管道输入数据（null 则不使用 stdin）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    public async Task<FFmpegResult> ExecuteAsync(String arguments, Byte[] inputData = null, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = FFmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = inputData != null,
        };

        if (WorkingDirectory != null)
            psi.WorkingDirectory = WorkingDirectory;

        using var process = new Process { StartInfo = psi };

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // 写入 stdin 数据
        if (inputData != null)
        {
#if NET5_0_OR_GREATER
            await process.StandardInput.BaseStream.WriteAsync(inputData, 0, inputData.Length, cancellationToken);
#else
            await process.StandardInput.BaseStream.WriteAsync(inputData, 0, inputData.Length);
#endif
            process.StandardInput.Close();
        }

        // 等待完成
        if (TimeoutMs > 0)
            process.WaitForExit(TimeoutMs);
        else
            process.WaitForExit();

        return new FFmpegResult
        {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString(),
            Error = errorBuilder.ToString(),
        };
    }

    /// <summary>执行 FFmpeg 命令并通过管道获取输出数据（用于解码）</summary>
    /// <param name="arguments">命令行参数</param>
    /// <param name="inputData">管道输入数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>标准输出数据</returns>
    public async Task<Byte[]> ExecutePipeAsync(String arguments, Byte[] inputData = null, CancellationToken cancellationToken = default)
    {
        return await ExecutePipeAsync(arguments, inputData != null ? new ReadOnlyMemory<Byte>(inputData) : default, cancellationToken);
    }

    /// <summary>执行 FFmpeg 命令并通过管道获取输出数据（用于解码），接受 ReadOnlyMemory 零拷贝输入</summary>
    /// <param name="arguments">命令行参数</param>
    /// <param name="inputData">管道输入数据（ReadOnlyMemory 视图）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>标准输出数据</returns>
    public async Task<Byte[]> ExecutePipeAsync(String arguments, ReadOnlyMemory<Byte> inputData, CancellationToken cancellationToken = default)
    {
        var hasInput = !inputData.IsEmpty;

        var psi = new ProcessStartInfo
        {
            FileName = FFmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = hasInput,
        };

        if (WorkingDirectory != null)
            psi.WorkingDirectory = WorkingDirectory;

        using var process = new Process { StartInfo = psi };
        process.Start();

        // 写入 stdin
        if (hasInput)
        {
            // 优先零拷贝：尝试获取底层 Byte[] 直接写入
            if (MemoryMarshal.TryGetArray(inputData, out var segment) && segment.Array != null)
            {
                await process.StandardInput.BaseStream.WriteAsync(segment.Array, segment.Offset, segment.Count);
            }
            else
            {
                var data = inputData.ToArray();
                await process.StandardInput.BaseStream.WriteAsync(data, 0, data.Length);
            }
            process.StandardInput.Close();
        }

        // 读取 stdout
        using var ms = new MemoryStream();
#if NET5_0_OR_GREATER
        await process.StandardOutput.BaseStream.CopyToAsync(ms, cancellationToken);
#else
        await process.StandardOutput.BaseStream.CopyToAsync(ms);
#endif

        if (TimeoutMs > 0)
            process.WaitForExit(TimeoutMs);
        else
            process.WaitForExit();

        return ms.ToArray();
    }

    /// <summary>使用 FFprobe 获取媒体信息</summary>
    /// <param name="filePath">媒体文件路径</param>
    /// <returns>JSON 格式的媒体信息</returns>
    public async Task<String> ProbeAsync(String filePath)
    {
        var arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"";
        var result = await ExecuteAsync(arguments);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(L.T("FFprobe execution failed: {0}", result.Error));

        return result.Output;
    }

    /// <summary>检查 FFmpeg 是否可用</summary>
    /// <returns>是否可用</returns>
    public Boolean IsAvailable()
    {
        try
        {
            var result = Execute("-version");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>检查 FFprobe 是否可用</summary>
    /// <returns>是否可用</returns>
    public Boolean IsProbeAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = FFprobePath,
                Arguments = "-version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var process = Process.Start(psi);
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    #region 辅助

    /// <summary>查找可执行文件</summary>
    private static String FindExecutable(String name)
    {
        // Windows 下添加 .exe 后缀
#if NETCOREAPP || NET5_0_OR_GREATER
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#else
        var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
#endif
        var exeName = isWindows ? $"{name}.exe" : name;

        // 1. 当前目录
        var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exeName);
        if (File.Exists(localPath)) return localPath;

        // 2. PATH 环境变量
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(dir, exeName);
                if (File.Exists(fullPath)) return fullPath;
            }
        }

        // 3. 常见安装位置
        var commonPaths = new[]
        {
            @"C:\ffmpeg\bin",
            @"/usr/local/bin",
            @"/usr/bin",
        };
        foreach (var dir in commonPaths)
        {
            var fullPath = Path.Combine(dir, exeName);
            if (File.Exists(fullPath)) return fullPath;
        }

        return name; // 回退到命令名（由系统 PATH 解析）
    }

    #endregion
}

/// <summary>FFmpeg 执行结果</summary>
public class FFmpegResult
{
    /// <summary>退出码</summary>
    public Int32 ExitCode { get; set; }

    /// <summary>标准输出</summary>
    public String Output { get; set; }

    /// <summary>标准错误（通常包含进度信息）</summary>
    public String Error { get; set; }

    /// <summary>是否成功</summary>
    public Boolean Success => ExitCode == 0;

    /// <summary>字符串表示</summary>
    /// <returns></returns>
    public override String ToString() => $"ExitCode={ExitCode}, Output={Output?.Length ?? 0} chars, Error={Error?.Length ?? 0} chars";
}
