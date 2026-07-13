using System.Text.Json;
using NewLife.Log;
using NewLife.Video.FFmpeg;
using NewLife.Video.Studio.Models;

namespace NewLife.Video.Studio.Services;

/// <summary>媒体信息查询服务，基于 ffprobe</summary>
public class MediaInfoService : IMediaInfoService
{
    private readonly FFmpegInvoker _invoker;
    private Boolean? _available;

    /// <summary>检查 FFmpeg/ffprobe 是否可用</summary>
    public Boolean IsAvailable => _available ??= CheckAvailable();

    /// <summary>实例化媒体信息服务</summary>
    public MediaInfoService() => _invoker = new FFmpegInvoker();

    private static Boolean CheckAvailable()
    {
        try
        {
            var invoker = new FFmpegInvoker();
            return invoker.IsProbeAvailable();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>获取媒体文件信息</summary>
    public async Task<MediaFileInfo?> GetInfoAsync(String filePath)
    {
        if (!IsAvailable) return null;
        if (!File.Exists(filePath)) return null;

        try
        {
            var json = await _invoker.ProbeAsync(filePath);
            return ParseProbeJson(json, filePath);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return null;
        }
    }

    private static MediaFileInfo? ParseProbeJson(String json, String filePath)
    {
        if (String.IsNullOrEmpty(json)) return null;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var info = new MediaFileInfo
        {
            FileName = Path.GetFileName(filePath),
            FilePath = filePath,
            FileSize = new FileInfo(filePath).Length,
        };

        // 解析 format 信息
        if (root.TryGetProperty("format", out var format))
        {
            info.Format = format.GetProperty("format_name").GetString() ?? "";
            info.Duration = format.TryGetProperty("duration", out var dur) ? dur.GetDouble() : 0;
            info.BitRate = format.TryGetProperty("bit_rate", out var br) ? br.GetInt64() : 0;
            info.FormatLong = format.TryGetProperty("format_long_name", out var fln) ? fln.GetString() : "";
        }

        // 解析 streams 信息
        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = stream.TryGetProperty("codec_type", out var ct) ? ct.GetString() : "";
                if (codecType == "video")
                {
                    info.VideoCodec = stream.TryGetProperty("codec_name", out var cn) ? cn.GetString() ?? "" : "";
                    info.VideoCodecLong = stream.TryGetProperty("codec_long_name", out var cln) ? cln.GetString() ?? "" : "";
                    info.Width = stream.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                    info.Height = stream.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
                    info.FrameRate = stream.TryGetProperty("r_frame_rate", out var fr) ? ParseFraction(fr.GetString() ?? "") : 0;
                    info.PixelFormat = stream.TryGetProperty("pix_fmt", out var pf) ? pf.GetString() ?? "" : "";
                    info.IsInterlaced = stream.TryGetProperty("field_order", out var fo) && fo.GetString() != "progressive";
                }
                else if (codecType == "audio")
                {
                    info.AudioCodec = stream.TryGetProperty("codec_name", out var an) ? an.GetString() ?? "" : "";
                    info.AudioSampleRate = stream.TryGetProperty("sample_rate", out var sr) ? sr.GetInt32() : 0;
                    info.AudioChannels = stream.TryGetProperty("channels", out var ch) ? ch.GetInt32() : 0;
                }
            }
        }

        return info;
    }

    private static Double ParseFraction(String fraction)
    {
        if (String.IsNullOrEmpty(fraction)) return 0;
        var parts = fraction.Split('/');
        if (parts.Length != 2) return 0;
        if (Double.TryParse(parts[0], out var num) && Double.TryParse(parts[1], out var den) && den != 0)
            return num / den;
        return 0;
    }
}
