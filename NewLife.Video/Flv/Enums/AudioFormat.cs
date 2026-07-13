namespace NewLife.Video.Flv.Enums;

/// <summary>FLV 音频格式标识</summary>
public enum AudioFormat
{
    /// <summary>Linear PCM, platform endian</summary>
    Pcm_Platform = 0,
    /// <summary>ADPCM</summary>
    ADPCM = 1,
    /// <summary>MP3</summary>
    MP3 = 2,
    /// <summary>Linear PCM, little endian</summary>
    Pcm_Little = 3,
    /// <summary>16-kHz mono Nellymoser</summary>
    Nellymoser_16Khz = 4,
    /// <summary>8-kHz mono Nellymoser</summary>
    Nellymoser_8Khz = 5,
    /// <summary>Nellymoser</summary>
    Nellymoser = 6,
    /// <summary>G.711 A-law</summary>
    G711_A_law = 7,
    /// <summary>G.711 mu-law</summary>
        G711_mu_law = 8,

        /// <summary>AAC</summary>
        AAC = 10,

        /// <summary>Speex</summary>
        Speex = 11,

    /// <summary>MP3 8kHz</summary>
    MP3_8Khz = 14,
    /// <summary>Device-specific sound</summary>
    DeviceSpecific = 15,
}
