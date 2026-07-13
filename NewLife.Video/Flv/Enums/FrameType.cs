namespace NewLife.Video.Flv.Enums;

/// <summary>FLV 视频帧类型</summary>
public enum FrameType : Byte
{
    /// <summary>关键帧</summary>
    KeyFrame = 16,
    /// <summary>非关键帧（P/B 帧）</summary>
    InterFrame = 32,
    /// <summary>可丢弃的非关键帧</summary>
    DisposableInterFrame = 48,
    /// <summary>生成的关键帧</summary>
    GeneratedKeyFrame = 64,
    /// <summary>视频信息/命令帧</summary>
    VideoInfoOrCommandFrame = 80,
}
