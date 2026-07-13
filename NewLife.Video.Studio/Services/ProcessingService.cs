using NewLife.Video.Processing;
using NewLife.Video.Studio.Models;

namespace NewLife.Video.Studio.Services;

/// <summary>帧处理管线服务</summary>
public class ProcessingService : IProcessingService
{
    /// <summary>获取可用的处理器列表</summary>
    public IList<String> GetAvailableProcessors() =>
    [
        "缩放", "裁剪", "旋转", "水平翻转", "垂直翻转",
        "亮度调整", "对比度调整", "饱和度调整", "水印叠加"
    ];

    /// <summary>处理单帧图像</summary>
    public VideoFrame ProcessFrame(VideoFrame frame, ProcessingOptions options)
    {
        var pipeline = new VideoPipeline();

        if (options.ResizeWidth > 0 && options.ResizeHeight > 0)
            pipeline.Append(new FrameResizer(options.ResizeWidth, options.ResizeHeight));

        if (options.CropWidth > 0 && options.CropHeight > 0)
            pipeline.Append(new FrameCropper(options.CropX, options.CropY, options.CropWidth, options.CropHeight));

        var rotationAngle = options.Rotation switch
        {
            90 => FrameRotator.RotationAngle.Clockwise90,
            180 => FrameRotator.RotationAngle.Clockwise180,
            270 => FrameRotator.RotationAngle.Clockwise270,
            _ => FrameRotator.RotationAngle.None,
        };

        if (rotationAngle != FrameRotator.RotationAngle.None || options.FlipHorizontal || options.FlipVertical)
        {
            pipeline.Append(new FrameRotator(rotationAngle, options.FlipHorizontal, options.FlipVertical));
        }

        if (Math.Abs(options.Brightness) > 0.01 || Math.Abs(options.Contrast) > 0.01 || Math.Abs(options.Saturation) > 0.01)
            pipeline.Append(new ColorAdjuster(options.Brightness, options.Contrast, options.Saturation));

        if (!String.IsNullOrEmpty(options.WatermarkText))
            pipeline.Append(new WatermarkOverlay(options.WatermarkText));

        return pipeline.ProcessFrame(frame);
    }
}
