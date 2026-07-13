using NewLife.Video.Processing;
using NewLife.Video.Studio.Models;

namespace NewLife.Video.Studio.Services;

/// <summary>格式转换服务，封装 VideoTranscoder</summary>
public class ConverterService : IConverterService
{
    /// <summary>支持的输出格式</summary>
    private static readonly String[] SupportedFormats = [".mp4", ".flv", ".avi", ".mkv", ".ts", ".mov", ".webm"];

    /// <summary>获取支持的输出格式列表</summary>
    public IList<String> GetSupportedFormats() => SupportedFormats;

    /// <summary>转换视频文件</summary>
    public async Task<Boolean> ConvertAsync(String input, String output, ConvertOptions? options = null, Action<Int32>? progress = null, CancellationToken cancellationToken = default)
    {
        var transOptions = new TranscodeOptions();

        if (options != null)
        {
            transOptions.Width = options.Width;
            transOptions.Height = options.Height;
            transOptions.Bitrate = (Int32)options.Bitrate;
            transOptions.FrameRate = (Int32)options.FrameRate;
            transOptions.ExtraArgs = options.ExtraArgs;
            transOptions.EncoderName = options.Encoder;
        }

        return await VideoTranscoder.TranscodeAsync(input, output, transOptions, progress, cancellationToken);
    }
}
