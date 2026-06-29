using System;
using NewLife.Data;
using NewLife.Video;

namespace XUnitTest;

/// <summary>测试用编解码器。实现 IVideoCodec 和 ICodecInfo</summary>
public class TestCodec : IVideoCodec, ICodecInfo
{
    /// <summary>最后解码的帧</summary>
    public VideoFrame[] LastDecodedFrames { get; private set; }

    /// <summary>最后编码的帧</summary>
    public VideoFrame[] LastEncodedFrames { get; private set; }

    public String Name => "TestCodec";
    public String Version => "1.0.0";
    public IReadOnlyCollection<VideoTypes> SupportedTypes => [VideoTypes.MJPEG, VideoTypes.H264, VideoTypes.VP8];
    public Boolean IsStateful => false;

    /// <summary>解码：简单返回空帧数组</summary>
    public VideoFrame[] Decode(VPacket packet)
    {
        LastDecodedFrames = [VideoFrame.Create(1920, 1080, VideoTypes.YUV420P)];
        return LastDecodedFrames;
    }

    /// <summary>编码：简单返回空包</summary>
    public VPacket Encode(VideoFrame[] frames)
    {
        LastEncodedFrames = frames;
        return new VPacket { Data = new Packet(new Byte[100]), CodecType = VideoTypes.H264 };
    }
}
