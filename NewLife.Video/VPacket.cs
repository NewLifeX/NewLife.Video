using NewLife.Data;

namespace NewLife.Video;

/// <summary>视频数据包（编码后的视频数据）</summary>
public class VPacket
{
    /// <summary>数据</summary>
    public IPacket Data { get; set; }

    /// <summary>编码类型</summary>
    public VideoTypes CodecType { get; set; }

    /// <summary>显示时间戳（毫秒）</summary>
    public Int64 PTS { get; set; }

    /// <summary>解码时间戳（毫秒）</summary>
    public Int64 DTS { get; set; }

    /// <summary>是否为关键帧</summary>
    public Boolean IsKeyFrame { get; set; }

    /// <summary>帧序号</summary>
    public Int64 FrameIndex { get; set; }

    /// <summary>创建空数据包</summary>
    public VPacket() { }

    /// <summary>从数据创建数据包</summary>
    /// <param name="data">编码数据</param>
    /// <param name="codecType">编码类型</param>
    public VPacket(IPacket data, VideoTypes codecType)
    {
        Data = data;
        CodecType = codecType;
    }
}
