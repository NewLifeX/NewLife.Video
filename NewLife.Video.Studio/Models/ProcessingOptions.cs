namespace NewLife.Video.Studio.Models;

/// <summary>帧处理选项</summary>
public class ProcessingOptions
{
    /// <summary>缩放宽度（0=不缩放）</summary>
    public Int32 ResizeWidth { get; set; }

    /// <summary>缩放高度（0=不缩放）</summary>
    public Int32 ResizeHeight { get; set; }

    /// <summary>裁剪区域 X</summary>
    public Int32 CropX { get; set; }

    /// <summary>裁剪区域 Y</summary>
    public Int32 CropY { get; set; }

    /// <summary>裁剪宽度</summary>
    public Int32 CropWidth { get; set; }

    /// <summary>裁剪高度</summary>
    public Int32 CropHeight { get; set; }

    /// <summary>旋转角度（90/180/270）</summary>
    public Int32 Rotation { get; set; }

    /// <summary>水平翻转</summary>
    public Boolean FlipHorizontal { get; set; }

    /// <summary>垂直翻转</summary>
    public Boolean FlipVertical { get; set; }

    /// <summary>亮度调整（-1.0 ~ 1.0）</summary>
    public Double Brightness { get; set; }

    /// <summary>对比度调整</summary>
    public Double Contrast { get; set; }

    /// <summary>饱和度调整</summary>
    public Double Saturation { get; set; }

    /// <summary>水印文字</summary>
    public String? WatermarkText { get; set; }
}
