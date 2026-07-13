using NewLife.Video.Studio.Models;

namespace NewLife.Video.Studio.Services;

/// <summary>格式转换服务接口（M3-1/2）</summary>
public interface IConverterService
{
    /// <summary>转换视频文件</summary>
    /// <param name="input">输入文件路径</param>
    /// <param name="output">输出文件路径</param>
    /// <param name="options">转换选项</param>
    /// <param name="progress">进度回调 (0-100)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<Boolean> ConvertAsync(String input, String output, ConvertOptions? options = null, Action<Int32>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>获取支持的输出格式列表</summary>
    IList<String> GetSupportedFormats();
}
