using System.Globalization;
using System.Reflection;
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

    [Theory(DisplayName = "10 种语言词表均生效")]
    [InlineData("zh-Hans", "无效 AVI")]
    [InlineData("zh-Hant", "無效 AVI")]
    [InlineData("es", "AVI no válido.")]
    [InlineData("de", "Ungültiges AVI.")]
    [InlineData("ja", "無効な AVI です。")]
    [InlineData("fr", "AVI non valide.")]
    [InlineData("pt", "AVI inválido.")]
    [InlineData("ru", "Недопустимый AVI.")]
    [InlineData("ko", "유효하지 않은 AVI입니다.")]
    [InlineData("ar", "AVI غير صالح.")]
    public void AllCultures_Translate(String cultureName, String expected)
    {
        var culture = TryGetCulture(cultureName);
        if (culture == null) return;

        var old = Localization.Culture;
        try
        {
            Localization.Culture = culture;
            Assert.True(Localization.IsSupported(culture));
            Assert.Equal(expected, Localization.Translate("Invalid AVI."));
        }
        finally
        {
            Localization.Culture = old;
        }
    }

    [Theory(DisplayName = "新增异常消息在全部语言中有译文")]
    [InlineData("zh-Hans", "VP8 解码器不支持编码。")]
    [InlineData("zh-Hant", "VP8 解碼器不支援編碼。")]
    [InlineData("es", "El decodificador VP8 no admite codificación.")]
    [InlineData("de", "Der VP8-Decoder unterstützt keine Codierung.")]
    [InlineData("ja", "VP8 デコーダーはエンコードをサポートしていません。")]
    [InlineData("fr", "Le décodeur VP8 ne prend pas en charge l'encodage.")]
    [InlineData("pt", "O decodificador VP8 não oferece suporte a codificação.")]
    [InlineData("ru", "Декодер VP8 не поддерживает кодирование.")]
    [InlineData("ko", "VP8 디코더는 인코딩을 지원하지 않습니다.")]
    [InlineData("ar", "وحدة فك ترميز VP8 لا تدعم الترميز.")]
    public void AllCultures_TranslateNewMessage(String cultureName, String expected)
    {
        var culture = TryGetCulture(cultureName);
        if (culture == null) return;

        var old = Localization.Culture;
        try
        {
            Localization.Culture = culture;
            Assert.Equal(expected, Localization.Translate("The VP8 decoder does not support encoding."));
            Assert.NotEqual("The VP9 encoder does not support decoding.", Localization.Translate("The VP9 encoder does not support decoding."));
        }
        finally
        {
            Localization.Culture = old;
        }
    }

    [Fact(DisplayName = "程序集标题与描述按界面语言本地化")]
    public void AssemblyMetadata_IsLocalized()
    {
        var assembly = typeof(VideoLog).Assembly;
        var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
        var description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
        Assert.Equal("NewLife Video Library", title);
        Assert.Equal("Video codecs, container formats, frame processing, streaming media and IoT video adaptation.", description);

        var zh = TryGetCulture("zh-Hans");
        if (zh == null) return;

        var old = Localization.Culture;
        try
        {
            Localization.Culture = zh;
            Assert.Equal("NewLife视频组件库", Localization.Translate(title));
            Assert.Equal("视频编解码、容器格式、帧处理、流媒体传输与IoT视频适配。", Localization.Translate(description));
        }
        finally
        {
            Localization.Culture = old;
        }
    }

    [Fact(DisplayName = "VP8/VP9 不支持方向的异常消息本地化")]
    public void UnsupportedDirection_MessageIsLocalized()
    {
        var zh = TryGetCulture("zh-Hans");
        if (zh == null) return;

        var old = Localization.Culture;
        try
        {
            Localization.Culture = zh;

            using var vp8 = new FFmpegVP8Decoder();
            var ex = Assert.Throws<NotSupportedException>(() => vp8.Encode([]));
            Assert.Equal("VP8 解码器不支持编码。", ex.Message);

            using var vp9 = new FFmpegVP9Encoder();
            var ex2 = Assert.Throws<NotSupportedException>(() => vp9.Decode(new VPacket()));
            Assert.Equal("VP9 编码器不支持解码。", ex2.Message);
        }
        finally
        {
            Localization.Culture = old;
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
