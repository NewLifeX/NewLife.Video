using NewLife.Video;
using NewLife.Video.Processing;
using Xunit;

namespace XUnitTest.Processing;

public class GifEncoderTests
{
    [Fact(DisplayName = "GIF 单帧编码正确")]
    public void Encode_SingleFrame_ProducesValidGif()
    {
        var frame = VideoFrame.Create(32, 24, VideoTypes.RGB24);

        var gif = GifEncoder.EncodeFrame(frame);

        Assert.NotNull(gif);
        Assert.True(gif.Length > 20);
        // GIF87a 签名
        Assert.Equal(0x47, gif[0]); // 'G'
        Assert.Equal(0x49, gif[1]); // 'I'
        Assert.Equal(0x46, gif[2]); // 'F'
        Assert.Equal(0x38, gif[3]); // '8'
        Assert.Equal(0x37, gif[4]); // '7'
        Assert.Equal(0x61, gif[5]); // 'a'
        // Trailer
        Assert.Equal(0x3B, gif[gif.Length - 1]);
    }

    [Fact(DisplayName = "GIF 多帧动画编码正确")]
    public void EncodeAnimation_MultipleFrames_ProducesValidGif()
    {
        var width = 16;
        var height = 16;
        var frame1 = new Byte[width * height * 3];
        var frame2 = new Byte[width * height * 3];
        // 填充不同颜色
        for (var i = 0; i < frame1.Length; i++) frame1[i] = 0xFF;
        for (var i = 0; i < frame2.Length; i++) frame2[i] = 0x80;

        var frames = new[] { frame1, frame2 };
        var gif = GifEncoder.EncodeAnimation(frames, width, height, delayCentiseconds: 20, loopCount: 0);

        Assert.NotNull(gif);
        Assert.True(gif.Length > 50);
        // GIF89a 签名（动画用）
        Assert.Equal(0x47, gif[0]); // 'G'
        Assert.Equal(0x49, gif[1]); // 'I'
        Assert.Equal(0x46, gif[2]); // 'F'
        Assert.Equal(0x38, gif[3]); // '8'
        Assert.Equal(0x39, gif[4]); // '9' (89a)
        Assert.Equal(0x61, gif[5]); // 'a'
        // Trailer
        Assert.Equal(0x3B, gif[gif.Length - 1]);

        // 包含 Netscape 循环扩展
        var nsExt = "NETSCAPE2.0"u8.ToArray();
        var containsNetscape = false;
        for (var i = 0; i <= gif.Length - nsExt.Length; i++)
        {
            var match = true;
            for (var j = 0; j < nsExt.Length; j++)
            {
                if (gif[i + j] != nsExt[j]) { match = false; break; }
            }
            if (match) { containsNetscape = true; break; }
        }
        Assert.True(containsNetscape, "动画 GIF 应包含 NETSCAPE2.0 循环扩展");
    }

    [Fact(DisplayName = "GIF 空帧抛异常")]
    public void EncodeAnimation_EmptyFrames_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GifEncoder.EncodeAnimation(null, 16, 16));
    }

    [Fact(DisplayName = "GIF 单帧参数校验")]
    public void Encode_InvalidParams_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GifEncoder.Encode(null, 16, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => GifEncoder.Encode([], 0, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => GifEncoder.Encode([], 16, -1));
    }

    [Fact(DisplayName = "PNG 编码正确")]
    public void PngEncode_ProducesValidPng()
    {
        var frame = VideoFrame.Create(32, 24, VideoTypes.RGB24);

        var png = PngEncoder.EncodeFrame(frame);

        Assert.NotNull(png);
        Assert.True(png.Length > 40);
        // PNG 签名: 137 80 78 71 13 10 26 10
        Assert.Equal(137, png[0]);
        Assert.Equal(80,  png[1]); // 'P'
        Assert.Equal(78,  png[2]); // 'N'
        Assert.Equal(71,  png[3]); // 'G'
        Assert.Equal(13,  png[4]);
        Assert.Equal(10,  png[5]);
        Assert.Equal(26,  png[6]);
        Assert.Equal(10,  png[7]);
        // IEND 末尾标记（4字节长度 + IEND + CRC = 12字节，IEND在倒数8-5位）
        var iendStart = png.Length - 12;
        Assert.Equal(0, png[iendStart + 0]); // 长度高位
        Assert.Equal(0, png[iendStart + 1]);
        Assert.Equal(0, png[iendStart + 2]);
        Assert.Equal(0, png[iendStart + 3]);
        Assert.Equal(0x49, png[iendStart + 4]); // 'I'
        Assert.Equal(0x45, png[iendStart + 5]); // 'E'
        Assert.Equal(0x4E, png[iendStart + 6]); // 'N'
        Assert.Equal(0x44, png[iendStart + 7]); // 'D'
    }

    [Fact(DisplayName = "WebP 编码正确")]
    public void WebPEncode_ProducesValidWebP()
    {
        var frame = VideoFrame.Create(32, 24, VideoTypes.RGB24);

        var webp = WebPEncoder.EncodeFrame(frame);

        Assert.NotNull(webp);
        Assert.True(webp.Length > 20);
        // RIFF 容器
        Assert.Equal(0x52, webp[0]); // 'R'
        Assert.Equal(0x49, webp[1]); // 'I'
        Assert.Equal(0x46, webp[2]); // 'F'
        Assert.Equal(0x46, webp[3]); // 'F'
        // WEBP 类型
        Assert.Equal(0x57, webp[8]);  // 'W'
        Assert.Equal(0x45, webp[9]);  // 'E'
        Assert.Equal(0x42, webp[10]); // 'B'
        Assert.Equal(0x50, webp[11]); // 'P'
        // VP8L 块
        Assert.Equal(0x56, webp[12]); // 'V'
        Assert.Equal(0x50, webp[13]); // 'P'
        Assert.Equal(0x38, webp[14]); // '8'
        Assert.Equal(0x4C, webp[15]); // 'L'
    }

    [Fact(DisplayName = "VideoFrameRenderer BGRA32转换正确")]
    public void Renderer_ToBgra32_CorrectFormat()
    {
        var width = 16;
        var height = 16;
        var frame = VideoFrame.Create(width, height, VideoTypes.RGB24);

        var bgra = VideoFrameRenderer.ToBgra32(frame);

        Assert.NotNull(bgra);
        Assert.Equal(width * height * 4, bgra.Length);

        // Alpha 通道应为 255
        for (var i = 3; i < bgra.Length; i += 4)
        {
            Assert.Equal(255, bgra[i]);
        }
    }

    [Fact(DisplayName = "VideoFrameRenderer YUV→BGRA32转换正确")]
    public void Renderer_YuvToBgra32_Works()
    {
        var frame = VideoFrame.Create(16, 16, VideoTypes.YUV420P);

        var bgra = VideoFrameRenderer.ToBgra32(frame);

        Assert.NotNull(bgra);
        Assert.Equal(16 * 16 * 4, bgra.Length);
    }

    [Fact(DisplayName = "VideoFrameRenderer GetPixel/SetPixel 正确")]
    public void Renderer_GetSetPixel_Works()
    {
        var buffer = VideoFrameRenderer.CreateBuffer(4, 4);
        Assert.Equal(4 * 4 * 4, buffer.Length);

        VideoFrameRenderer.SetPixel(buffer, 4, 1, 2, 128, 64, 255, 200);
        var pixel = VideoFrameRenderer.GetPixel(buffer, 4, 1, 2);

        Assert.Equal((Byte)128, pixel.b);
        Assert.Equal((Byte)64, pixel.g);
        Assert.Equal((Byte)255, pixel.r);
        Assert.Equal((Byte)200, pixel.a);
    }
}
