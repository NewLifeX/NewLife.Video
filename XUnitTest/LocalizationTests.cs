using System.Globalization;
using NewLife;
using NewLife.Log;
using NewLife.Video;
using NewLife.Video.Codecs;
using NewLife.Video.Streaming;
using Xunit;

namespace XUnitTest;

[CollectionDefinition("Localization", DisableParallelization = true)]
public class LocalizationCollection { }

/// <summary>运行时多语言与带前缀日志测试</summary>
[Collection("Localization")]
public class LocalizationTests
{
    [Fact(DisplayName = "翻译按目标语言输出，缺失时回退英文")]
    public void Translate_RespectsCulture()
    {
        var zh = TryGetCulture("zh-Hans");
        if (zh == null) return;

        var old = Localization.Culture;
        try
        {
            Localization.Culture = zh;
            Assert.Equal("[H264] 没有注册对应的编解码器。", Localization.Translate("No codec is registered for [{0}].", VideoTypes.H264));

            Localization.Culture = TryGetCulture("en");
            Assert.Equal("No codec is registered for [H264].", Localization.Translate("No codec is registered for [{0}].", VideoTypes.H264));
            Assert.Equal("Unknown key for test.", Localization.Translate("Unknown key for test."));
        }
        finally
        {
            Localization.Culture = old;
        }
    }

    [Fact(DisplayName = "日志语言独立于界面语言")]
    public void TranslateForLog_UsesLogCulture()
    {
        var ja = TryGetCulture("ja");
        if (ja == null) return;

        var oldCulture = Localization.Culture;
        var oldLogCulture = Localization.LogCulture;
        try
        {
            Localization.Culture = null;
            Localization.LogCulture = ja;
            Assert.Equal("[H264] に対応するコーデックが登録されていません。", Localization.TranslateForLog("No codec is registered for [{0}].", VideoTypes.H264));
        }
        finally
        {
            Localization.Culture = oldCulture;
            Localization.LogCulture = oldLogCulture;
        }
    }

    [Fact(DisplayName = "异常消息按界面语言本地化")]
    public void Exception_MessageIsLocalized()
    {
        var zh = TryGetCulture("zh-Hans");
        if (zh == null) return;

        var old = Localization.Culture;
        try
        {
            Localization.Culture = zh;
            var ex = Assert.Throws<NotSupportedException>(() => new CodecRegistry().GetCodec(VideoTypes.H264));
            Assert.Equal("[H264] 没有注册对应的编解码器。", ex.Message);
        }
        finally
        {
            Localization.Culture = old;
        }
    }

    [Fact(DisplayName = "带前缀日志先翻译再拼接前缀")]
    public void VideoLog_PrefixAfterTranslation()
    {
        var zh = TryGetCulture("zh-Hans");
        if (zh == null) return;

        var oldLog = VideoLog.Log;
        var oldLogCulture = Localization.LogCulture;
        try
        {
            var capture = new CaptureLogger();
            VideoLog.Log = capture;
            Localization.LogCulture = zh;

            VideoLog.Write(LogLevel.Info, "[FFmpeg]", "FFmpeg downloaded: {0}", "/tmp/ffmpeg");

            var line = Assert.Single(capture.Lines);
            Assert.StartsWith("[FFmpeg] ", line);
            Assert.Equal("[FFmpeg] FFmpeg 下载完成：/tmp/ffmpeg", line);
        }
        finally
        {
            VideoLog.Log = oldLog;
            Localization.LogCulture = oldLogCulture;
        }
    }

    [Fact(DisplayName = "流媒体连接失败输出带前缀日志")]
    public async Task ConnectFailure_LogsWithPrefix()
    {
        var oldLog = VideoLog.Log;
        try
        {
            var capture = new CaptureLogger();
            VideoLog.Log = capture;

            using (var rtsp = new RtspClient("rtsp://127.0.0.1:1/stream"))
            {
                Assert.False(await rtsp.ConnectAsync());
            }
            using (var rtmp = new RtmpClient("rtmp://127.0.0.1:1/live/test"))
            {
                Assert.False(await rtmp.ConnectAsync());
            }

            Assert.Contains(capture.Lines, line => line.StartsWith("[RTSP] ", StringComparison.Ordinal));
            Assert.Contains(capture.Lines, line => line.StartsWith("[RTMP] ", StringComparison.Ordinal));
        }
        finally
        {
            VideoLog.Log = oldLog;
        }
    }

    private static CultureInfo TryGetCulture(String name)
    {
        try
        {
            return new CultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private sealed class CaptureLogger : Logger
    {
        public List<String> Lines { get; } = [];

        protected override void OnWrite(LogLevel level, String format, params Object[] args)
            => Lines.Add(args == null || args.Length == 0 ? format : String.Format(format, args));
    }
}
