using NewLife.Data;

namespace NewLife.Video;

/// <summary>芯片视频头处理接口</summary>
/// <remarks>
/// IoT 芯片（海思/大华/宇视等）在视频编码数据前附加厂商自定义头部。
/// 实现此接口以支持自动识别和去除/还原芯片头。
/// </remarks>
public interface IVideoChipHeader
{
    /// <summary>优先级，数字越小越先尝试</summary>
    Int32 Priority { get; }

    /// <summary>尝试去除芯片头</summary>
    /// <param name="data">含芯片头的视频数据</param>
    /// <param name="result">去除头后的数据</param>
    /// <returns>是否成功去除</returns>
    Boolean TryTrim(Packet data, out Packet result);

    /// <summary>尝试添加芯片头</summary>
    /// <param name="data">原始视频数据</param>
    /// <param name="result">添加头后的数据</param>
    /// <returns>是否成功添加</returns>
    Boolean TryAdd(Packet data, out Packet result);
}
