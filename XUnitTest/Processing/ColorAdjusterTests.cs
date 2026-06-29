using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class ColorAdjusterTests
{
    [Fact(DisplayName = "ColorAdjuster默认参数不改变帧")]
    public void Default_NoChange()
    {
        var adjuster = new ColorAdjuster();
        var input = VideoFrame.Create(8, 8, VideoTypes.RGB24);
        // 填充已知值
        for (var i = 0; i < input.Data.Length; i += 3)
        {
            input.Data[i] = 100;     // B
            input.Data[i + 1] = 150; // G
            input.Data[i + 2] = 200; // R
        }

        var output = adjuster.ProcessFrame(input);

        // 允许 ±1 的舍入误差
        Assert.True(Math.Abs(output.Data[0] - 100) <= 1);
        Assert.True(Math.Abs(output.Data[1] - 150) <= 1);
        Assert.True(Math.Abs(output.Data[2] - 200) <= 1);
    }

    [Fact(DisplayName = "ColorAdjuster提高亮度")]
    public void Brightness_Increased()
    {
        var adjuster = new ColorAdjuster(brightness: 0.2); // +20%
        var input = VideoFrame.Create(4, 4, VideoTypes.RGB24);
        Array.Fill(input.Data, (Byte)100);

        var output = adjuster.ProcessFrame(input);

        // 亮度增加20% → 100 + 0.2*255 = 151
        Assert.True(output.Data[0] > 100, $"Expected >100, got {output.Data[0]}");
    }

    [Fact(DisplayName = "ColorAdjuster降低亮度钳位到0")]
    public void Brightness_Decreased_ClampToZero()
    {
        var adjuster = new ColorAdjuster(brightness: -1.0); // -100%
        var input = VideoFrame.Create(4, 4, VideoTypes.RGB24);
        Array.Fill(input.Data, (Byte)50);

        var output = adjuster.ProcessFrame(input);

        // 亮度降低超过原始值 → 钳位到 0
        Assert.Equal(0, output.Data[0]);
    }

    [Fact(DisplayName = "ColorAdjuster提高对比度")]
    public void Contrast_Increased()
    {
        var adjuster = new ColorAdjuster(contrast: 2.0); // 双倍对比度

        // 创建一半暗一半亮的帧
        var input = VideoFrame.Create(4, 4, VideoTypes.RGB24);
        var half = input.Data.Length / 2;
        for (var i = 0; i < half; i++) input.Data[i] = 50;    // 暗
        for (var i = half; i < input.Data.Length; i++) input.Data[i] = 200; // 亮

        var output = adjuster.ProcessFrame(input);

        // 暗的更暗，亮的更亮
        Assert.True(output.Data[0] < 50, $"Dark became lighter: {output.Data[0]}");
    }

    [Fact(DisplayName = "ColorAdjuster输出尺寸和格式正确")]
    public void OutputFormat_Correct()
    {
        var adjuster = new ColorAdjuster();
        var input = VideoFrame.Create(16, 16, VideoTypes.RGB24);

        var output = adjuster.ProcessFrame(input);

        Assert.Equal(16, output.Width);
        Assert.Equal(16, output.Height);
        Assert.Equal(VideoTypes.RGB24, output.PixelFormat);
    }

    [Fact(DisplayName = "ColorAdjuster串接在管线中正常工作")]
    public void InPipeline_WorksCorrectly()
    {
        var pipeline = new VideoPipeline()
            .Append(new FrameResizer(8, 8))
            .Append(new ColorAdjuster(brightness: 0.1, contrast: 1.2));

        var input = VideoFrame.Create(16, 16, VideoTypes.RGB24);

        var output = pipeline.ProcessFrame(input);

        Assert.Equal(8, output.Width);
        Assert.Equal(8, output.Height);
    }
}
