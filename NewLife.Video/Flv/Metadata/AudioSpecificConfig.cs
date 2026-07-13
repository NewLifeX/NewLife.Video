using NewLife.Audio.Codecs;

namespace NewLife.Video.Flv.Metadata;

/// <summary>AAC AudioSpecificConfig (ISO 14496-3)。委托到 NewLife.Audio 完整实现</summary>
public class AudioSpecificConfig
{
    /// <summary>AAC 音频对象类型（1=Main, 2=LC, 5=SBR）</summary>
    public Int32 AudioType { get; set; } = 2;

    /// <summary>采样率索引（0-12, 15=逃逸）</summary>
    public Int32 SamplingFrequencyIndex { get; set; } = 11;

    /// <summary>声道配置（1=单声道, 2=立体声）</summary>
    public Int32 ChannelConfiguration { get; set; } = 1;

    /// <summary>序列化为字节数组</summary>
    public Byte[] ToArray()
    {
        var inner = NewLife.Audio.Codecs.AudioSpecificConfig.FromParameters(AudioType, GetSampleRate(), ChannelConfiguration);
        return inner.ToByteArray();
    }

    private Int32 GetSampleRate() => SamplingFrequencyIndex switch
    {
        0 => 96000, 1 => 88200, 2 => 64000, 3 => 48000,
        4 => 44100, 5 => 32000, 6 => 24000, 7 => 22050,
        8 => 16000, 9 => 12000, 10 => 11025, 11 => 8000,
        12 => 7350, _ => 8000,
    };
}
