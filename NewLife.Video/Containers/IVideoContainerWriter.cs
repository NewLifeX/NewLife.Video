namespace NewLife.Video.Containers;

/// <summary>视频容器写入器接口</summary>
public interface IVideoContainerWriter : IDisposable
{
    /// <summary>写入元数据头（必须在写入帧之前调用）</summary>
    /// <param name="width">视频宽度</param>
    /// <param name="height">视频高度</param>
    /// <param name="codecType">编码类型</param>
    /// <param name="frameRate">帧率</param>
    void WriteMetadata(Int32 width, Int32 height, VideoTypes codecType, Double frameRate);

    /// <summary>写入一帧编码数据</summary>
    /// <param name="packet">编码数据包</param>
    void WriteFrame(VPacket packet);

    /// <summary>刷新缓冲区并完成文件写入</summary>
    void Flush();
}
