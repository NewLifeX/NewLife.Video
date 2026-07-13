namespace NewLife.Video.Flv.Enums;

/// <summary>AVC（H.264）视频包类型</summary>
public enum AvcPacketType : Byte
{
    SequenceHeader,
    Raw,
    AVCEndSequence,
}
