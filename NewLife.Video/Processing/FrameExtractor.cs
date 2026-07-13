using NewLife.Data;
using NewLife.Video;
using NewLife.Video.Containers;

namespace NewLife.Video.Processing;

/// <summary>帧快照提取器。从视频文件或流中按时间/帧号提取静态帧</summary>
/// <remarks>
/// 支持从容器文件（通过 VideoContainerFactory）和直接 VideoFrame 流中提取。
/// </remarks>
public class FrameExtractor
{
    /// <summary>从视频文件中按时间提取快照</summary>
    /// <param name="filePath">视频文件路径</param>
    /// <param name="timestampMs">目标时间（毫秒）</param>
    /// <returns>快照帧，未找到返回 null</returns>
    public static VideoFrame ExtractSnapshot(String filePath, Int64 timestampMs)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("视频文件不存在", filePath);

        using var reader = VideoContainerFactory.CreateReader(filePath);

        // 计算目标帧号
        var targetFrame = (Int64)(timestampMs / 1000.0 * reader.FrameRate);
        if (targetFrame < 0) targetFrame = 0;

        return ExtractByFrameIndex(reader, targetFrame);
    }

    /// <summary>从视频文件中按帧号提取快照</summary>
    /// <param name="filePath">视频文件路径</param>
    /// <param name="frameIndex">帧号（从 0 开始）</param>
    /// <returns>快照帧，未找到返回 null</returns>
    public static VideoFrame ExtractSnapshotByIndex(String filePath, Int64 frameIndex)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("视频文件不存在", filePath);

        using var reader = VideoContainerFactory.CreateReader(filePath);
        return ExtractByFrameIndex(reader, frameIndex);
    }

    private static VideoFrame ExtractByFrameIndex(IVideoContainerReader reader, Int64 frameIndex)
    {
        if (reader.SeekFrame(frameIndex))
        {
            var packet = reader.ReadFrame();
            if (packet != null)
            {
                return new VideoFrame
                {
                    Width = reader.VideoWidth,
                    Height = reader.VideoHeight,
                    PixelFormat = reader.VideoCodecType,
                    Timestamp = packet.PTS,
                    FrameIndex = packet.FrameIndex,
                    IsKeyFrame = packet.IsKeyFrame,
                    Data = packet.Data?.ToArray() ?? [],
                };
            }
        }

        return null;
    }

    /// <summary>提取视频文件的所有关键帧</summary>
    /// <param name="filePath">视频文件路径</param>
    /// <param name="maxFrames">最大提取帧数（0=不限制）</param>
    /// <returns>关键帧列表</returns>
    public static List<VideoFrame> ExtractKeyFrames(String filePath, Int32 maxFrames = 0)
    {
        var keyFrames = new List<VideoFrame>();

        if (!File.Exists(filePath))
            throw new FileNotFoundException("视频文件不存在", filePath);

        using var reader = VideoContainerFactory.CreateReader(filePath);

        VPacket packet;
        while ((packet = reader.ReadFrame()) != null)
        {
            if (packet.IsKeyFrame)
            {
                keyFrames.Add(new VideoFrame
                {
                    Width = reader.VideoWidth,
                    Height = reader.VideoHeight,
                    PixelFormat = reader.VideoCodecType,
                    Timestamp = packet.PTS,
                    FrameIndex = packet.FrameIndex,
                    IsKeyFrame = true,
                    Data = packet.Data?.ToArray() ?? [],
                });

                if (maxFrames > 0 && keyFrames.Count >= maxFrames)
                    break;
            }
        }

        return keyFrames;
    }
}
