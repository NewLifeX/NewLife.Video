namespace NewLife.Video;

/// <summary>视频编码类型</summary>
public enum VideoTypes : Byte
{
    /// <summary>H.264 / AVC</summary>
    H264 = 98,

    /// <summary>H.265 / HEVC</summary>
    H265 = 99,

    /// <summary>MJPEG (Motion JPEG)</summary>
    MJPEG = 1,

    /// <summary>VP8</summary>
    VP8 = 2,

    /// <summary>VP9</summary>
    VP9 = 3,

    /// <summary>AV1</summary>
    AV1 = 4,

    /// <summary>YUV420P 平面格式 (YYYY...UU...VV...)</summary>
    YUV420P = 50,

    /// <summary>NV12 半平面格式 (YYYY...UVUV...)</summary>
    NV12 = 51,

    /// <summary>NV21 半平面格式 (YYYY...VUVU...)</summary>
    NV21 = 52,

    /// <summary>YUV422P 平面格式</summary>
    YUV422P = 53,

    /// <summary>YUV444P 平面格式</summary>
    YUV444P = 54,

    /// <summary>RGB24 打包格式 (BGR顺序，8位/通道)</summary>
    RGB24 = 70,

    /// <summary>BGR24 打包格式 (RGB顺序，8位/通道)</summary>
    BGR24 = 71,

    /// <summary>BGRA32 打包格式 (BGRA顺序，8位/通道)</summary>
    BGRA32 = 72,

    /// <summary>RGBA32 打包格式 (RGBA顺序，8位/通道)</summary>
    RGBA32 = 73,

    /// <summary>灰度 8位</summary>
    GRAY8 = 80,

    /// <summary>未知/原始数据</summary>
    Unknown = 255,
}
