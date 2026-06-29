using NewLife.Data;

namespace NewLife.Video;

/// <summary>视频编解码器接口</summary>
public interface IVideoCodec
{
    /// <summary>解码编码数据为原始帧</summary>
    /// <param name="packet">编码数据包</param>
    /// <returns>解码后的视频帧数组（一包可能解出多帧）</returns>
    VideoFrame[] Decode(VPacket packet);

    /// <summary>编码原始帧为编码数据</summary>
    /// <param name="frames">原始视频帧数组</param>
    /// <returns>编码后的数据包</returns>
    VPacket Encode(VideoFrame[] frames);
}
