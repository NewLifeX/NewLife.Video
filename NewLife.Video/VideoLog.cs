using NewLife.Log;

namespace NewLife.Video;

/// <summary>视频组件日志入口。消息先按日志语言翻译，再拼接前缀</summary>
/// <remarks>
/// 与 NewLife.Core 的 <c>LogExtensions.WritePrefixed</c> 保持一致：带前缀的日志必须先翻译后拼接，
/// 否则用包含动态前缀的整串查表会永远回退英文。
/// </remarks>
public static class VideoLog
{
    /// <summary>日志对象。默认使用全局日志 XTrace.Log，宿主可替换为自己的日志实现</summary>
    public static ILog Log { get; set; } = XTrace.Log;

    /// <summary>默认日志前缀</summary>
    public const String DefaultPrefix = "[Video]";

    /// <summary>当前日志前缀。默认 [Video]，宿主可替换为自己的组件标识</summary>
    public static String Prefix { get; set; } = DefaultPrefix;

    /// <summary>写调试日志。前缀在翻译之后拼接</summary>
    /// <param name="format">英文原文模板</param>
    /// <param name="args">格式化参数</param>
    public static void Debug(String format, params Object[] args) => Write(LogLevel.Debug, Prefix, format, args);

    /// <summary>写信息日志。前缀在翻译之后拼接</summary>
    /// <param name="format">英文原文模板</param>
    /// <param name="args">格式化参数</param>
    public static void Info(String format, params Object[] args) => Write(LogLevel.Info, Prefix, format, args);

    /// <summary>写警告日志。前缀在翻译之后拼接</summary>
    /// <param name="format">英文原文模板</param>
    /// <param name="args">格式化参数</param>
    public static void Warn(String format, params Object[] args) => Write(LogLevel.Warn, Prefix, format, args);

    /// <summary>写错误日志。前缀在翻译之后拼接</summary>
    /// <param name="format">英文原文模板</param>
    /// <param name="args">格式化参数</param>
    public static void Error(String format, params Object[] args) => Write(LogLevel.Error, Prefix, format, args);

    /// <summary>写自定义前缀的日志。前缀在翻译之后拼接</summary>
    /// <param name="level">日志等级</param>
    /// <param name="prefix">日志前缀，不参与本地化</param>
    /// <param name="format">英文原文模板</param>
    /// <param name="args">格式化参数</param>
    public static void Write(LogLevel level, String prefix, String format, params Object[] args)
    {
        prefix ??= DefaultPrefix;
        if (prefix.Length > 0 && !prefix.EndsWith(" ", StringComparison.Ordinal)) prefix += " ";

        Log?.WritePrefixed(level, prefix, format, args);
    }
}
