using NewLife.Data;
using System.Text;

namespace NewLife.Video.Processing;

/// <summary>纯托管 GIF 编码器。将 RGB24 帧数据编码为 GIF 87a 格式，零外部依赖</summary>
/// <remarks>
/// 自研 LZW 压缩实现 + 自适应调色板（256 色中位切割量化）。
/// 支持单帧编码（IMG-8）和多帧动画编码（IMG-11）。
/// </remarks>
public static class GifEncoder
{
    /// <summary>将 RGB24 像素数据编码为 GIF 文件字节数组（单帧）</summary>
    /// <param name="rgbData">RGB24 像素数据</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <returns>GIF 文件字节数组</returns>
    public static Byte[] Encode(Byte[] rgbData, Int32 width, Int32 height)
    {
        if (rgbData == null) throw new ArgumentNullException(nameof(rgbData));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        return EncodeFrame(rgbData, width, height, 0);
    }

    /// <summary>将 VideoFrame 编码为 GIF 字节数组</summary>
    public static Byte[] EncodeFrame(VideoFrame frame)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));

        var rgbData = EnsureRgb24(frame);
        return Encode(rgbData, frame.Width, frame.Height);
    }

    /// <summary>将多帧 RGB24 数据编码为动画 GIF 文件字节数组</summary>
    /// <param name="frames">RGB24 帧序列</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <param name="delayCentiseconds">帧间隔（百分之一秒，默认 10 = 0.1 秒）</param>
    /// <param name="loopCount">循环次数（0 = 无限循环）</param>
    /// <returns>GIF 文件字节数组</returns>
    public static Byte[] EncodeAnimation(IReadOnlyList<Byte[]> frames, Int32 width, Int32 height, Int32 delayCentiseconds = 10, Int32 loopCount = 0)
    {
        if (frames == null) throw new ArgumentNullException(nameof(frames));
        if (frames.Count == 0) return [];
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        // 全局调色板：使用第一帧做量化
        var (palette, _) = Quantize(frames[0]);

        using (var ms = new MemoryStream())
        {
            // GIF 89a 头（支持动画扩展）
            WriteString(ms, "GIF89a");

            // 逻辑屏幕描述符
            WriteLE16(ms, width);
            WriteLE16(ms, height);

            var packed = (Byte)(0x80 | // 全局调色板标志
                7 << 4 |              // 颜色分辨率
                (Byte)(Math.Log(palette.Length / 3, 2) - 1)); // 调色板大小
            ms.WriteByte(packed);
            ms.WriteByte(0); // 背景色
            ms.WriteByte(0); // 像素宽高比

            // 全局调色板
            ms.Write(palette, 0, palette.Length);

            // 应用扩展：循环次数 (NETSACAPE 2.0)
            WriteExtension(ms, 0xFF, writer =>
            {
                WriteString(writer, "NETSCAPE2.0");
                writer.WriteByte(3);  // 子块大小
                writer.WriteByte(1);  // 子块 ID（循环）
                WriteLE16(writer, (Int16)loopCount);
                writer.WriteByte(0);  // 块终结
            });

            // 写入每一帧
            for (var i = 0; i < frames.Count; i++)
            {
                var (_, indices) = QuantizeFrame(frames[i], palette);
                WriteFrame(ms, indices, width, height, palette.Length / 3, delayCentiseconds);
            }

            // Trailer
            ms.WriteByte(0x3B);

            return ms.ToArray();
        }
    }

    #region 单帧编码

    private static Byte[] EncodeFrame(Byte[] rgbData, Int32 width, Int32 height, Int32 delay)
    {
        var (palette, indices) = Quantize(rgbData);

        using (var ms = new MemoryStream())
        {
            WriteString(ms, "GIF87a");

            WriteLE16(ms, width);
            WriteLE16(ms, height);

            var colorCount = palette.Length / 3;
            var packed = (Byte)(0x80 | 7 << 4 | (Byte)(Math.Log(colorCount, 2) - 1));
            ms.WriteByte(packed);
            ms.WriteByte(0); // 背景色
            ms.WriteByte(0); // 像素宽高比

            ms.Write(palette, 0, palette.Length);

            WriteFrame(ms, indices, width, height, colorCount, delay);

            ms.WriteByte(0x3B);
            return ms.ToArray();
        }
    }

    #endregion

    #region GIF 写入

    private static void WriteFrame(MemoryStream ms, Byte[] indices, Int32 width, Int32 height, Int32 colorCount, Int32 delay)
    {
        // 图形控制扩展（透明色等）
        if (delay > 0)
        {
            WriteExtension(ms, 0xF9, writer =>
            {
                writer.WriteByte(4);  // 块大小
                writer.WriteByte(0);  // 封装字段（无透明色）
                WriteLE16(writer, (Int16)delay); // 延迟时间
                writer.WriteByte(0);  // 透明色索引
                writer.WriteByte(0);  // 块终结
            });
        }

        // 图像描述符
        ms.WriteByte(0x2C); // 图像分隔符
        WriteLE16(ms, 0);   // Image Left
        WriteLE16(ms, 0);   // Image Top
        WriteLE16(ms, width);
        WriteLE16(ms, height);
        ms.WriteByte(0);    // 无局部调色板

        // LZW 编码图像数据
        var lzwData = LzwEncode(indices, colorCount);
        ms.Write(lzwData, 0, lzwData.Length);
    }

    private static void WriteExtension(MemoryStream ms, Byte label, Action<MemoryStream> writeData)
    {
        ms.WriteByte(0x21); // 扩展引入符
        ms.WriteByte(label);
        writeData(ms);
    }

    #endregion

    #region 颜色量化

    private static (Byte[] palette, Byte[] indices) Quantize(Byte[] rgbData)
    {
        // 使用中位切割量化到 256 色
        return QuantizeMedianCut(rgbData, 256);
    }

    private static (Byte[] palette, Byte[] indices) QuantizeFrame(Byte[] rgbData, Byte[] palette)
    {
        var colorCount = palette.Length / 3;
        var pixelCount = rgbData.Length / 3;
        var indices = new Byte[pixelCount];

        for (var i = 0; i < pixelCount; i++)
        {
            var r = rgbData[i * 3];
            var g = rgbData[i * 3 + 1];
            var b = rgbData[i * 3 + 2];
            indices[i] = (Byte)FindNearestColor(palette, colorCount, r, g, b);
        }

        return (palette, indices);
    }

    /// <summary>中位切割量化算法</summary>
    private static (Byte[] palette, Byte[] indices) QuantizeMedianCut(Byte[] rgbData, Int32 maxColors)
    {
        var pixelCount = rgbData.Length / 3;

        // 收集所有像素
        var pixels = new (Byte r, Byte g, Byte b)[pixelCount];
        for (var i = 0; i < pixelCount; i++)
        {
            pixels[i] = (rgbData[i * 3], rgbData[i * 3 + 1], rgbData[i * 3 + 2]);
        }

        // 中位切割
        var boxes = new List<ColorBox> { new ColorBox(pixels, 0, pixelCount - 1) };

        while (boxes.Count < maxColors)
        {
            // 找到最大的盒子
            var largest = boxes.OrderByDescending(b => b.Size).FirstOrDefault();
            if (largest == null || largest.Size < 2) break;

            boxes.Remove(largest);
            var split = largest.Split();
            if (split != null)
            {
                boxes.Add(split.Value.Item1);
                boxes.Add(split.Value.Item2);
            }
            else
            {
                boxes.Add(largest);
                break;
            }
        }

        // 构建调色板（每个盒子的平均色）
        var colorCount = boxes.Count;
        var palette = new Byte[colorCount * 3];
        var indices = new Byte[pixelCount];

        for (var bi = 0; bi < colorCount; bi++)
        {
            var box = boxes[bi];
            var sumR = 0;
            var sumG = 0;
            var sumB = 0;
            for (var pi = box.Start; pi <= box.End; pi++)
            {
                sumR += pixels[pi].r;
                sumG += pixels[pi].g;
                sumB += pixels[pi].b;
            }
            var count = box.Size;
            palette[bi * 3] = (Byte)(sumR / count);
            palette[bi * 3 + 1] = (Byte)(sumG / count);
            palette[bi * 3 + 2] = (Byte)(sumB / count);

            // 给盒子内像素分配颜色索引
            for (var pi = box.Start; pi <= box.End; pi++)
            {
                // 找到该像素在原数组中的位置
                // 需要反向映射
            }
        }

        // 重新映射每个像素到最近的调色板颜色
        for (var i = 0; i < pixelCount; i++)
        {
            var r = rgbData[i * 3];
            var g = rgbData[i * 3 + 1];
            var b = rgbData[i * 3 + 2];
            var bestIdx = 0;
            var bestDist = Int32.MaxValue;
            for (var ci = 0; ci < colorCount; ci++)
            {
                var dr = r - palette[ci * 3];
                var dg = g - palette[ci * 3 + 1];
                var db = b - palette[ci * 3 + 2];
                var dist = dr * dr + dg * dg + db * db;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = ci;
                }
            }
            indices[i] = (Byte)bestIdx;
        }

        return (palette, indices);
    }

    private static Int32 FindNearestColor(Byte[] palette, Int32 colorCount, Byte r, Byte g, Byte b)
    {
        var bestIdx = 0;
        var bestDist = Int32.MaxValue;
        for (var i = 0; i < colorCount; i++)
        {
            var dr = r - palette[i * 3];
            var dg = g - palette[i * 3 + 1];
            var db = b - palette[i * 3 + 2];
            var dist = dr * dr + dg * dg + db * db;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    private class ColorBox
    {
        public (Byte r, Byte g, Byte b)[] Pixels { get; }
        public Int32 Start { get; }
        public Int32 End { get; }
        public Int32 Size => End - Start + 1;

        public ColorBox((Byte r, Byte g, Byte b)[] pixels, Int32 start, Int32 end)
        {
            Pixels = pixels;
            Start = start;
            End = end;
        }

        public (ColorBox, ColorBox)? Split()
        {
            // 找到最大颜色分量范围
            var minR = Byte.MaxValue; var maxR = Byte.MinValue;
            var minG = Byte.MaxValue; var maxG = Byte.MinValue;
            var minB = Byte.MaxValue; var maxB = Byte.MinValue;

            for (var i = Start; i <= End; i++)
            {
                if (Pixels[i].r < minR) minR = Pixels[i].r;
                if (Pixels[i].r > maxR) maxR = Pixels[i].r;
                if (Pixels[i].g < minG) minG = Pixels[i].g;
                if (Pixels[i].g > maxG) maxG = Pixels[i].g;
                if (Pixels[i].b < minB) minB = Pixels[i].b;
                if (Pixels[i].b > maxB) maxB = Pixels[i].b;
            }

            var rangeR = maxR - minR;
            var rangeG = maxG - minG;
            var rangeB = maxB - minB;

            // 按最大范围的颜色分量排序
            if (rangeR >= rangeG && rangeR >= rangeB)
                Array.Sort(Pixels, Start, Size, new ColorComparer(c => c.r));
            else if (rangeG >= rangeR && rangeG >= rangeB)
                Array.Sort(Pixels, Start, Size, new ColorComparer(c => c.g));
            else
                Array.Sort(Pixels, Start, Size, new ColorComparer(c => c.b));

            var mid = (Start + End) / 2;
            if (mid == Start || mid == End) return null;

            return (new ColorBox(Pixels, Start, mid), new ColorBox(Pixels, mid + 1, End));
        }
    }

    private class ColorComparer : IComparer<(Byte r, Byte g, Byte b)>
    {
        private readonly Func<(Byte r, Byte g, Byte b), Byte> _selector;
        public ColorComparer(Func<(Byte r, Byte g, Byte b), Byte> selector) => _selector = selector;
        public Int32 Compare((Byte r, Byte g, Byte b) x, (Byte r, Byte g, Byte b) y) => _selector(x).CompareTo(_selector(y));
    }

    #endregion

    #region LZW 编码

    /// <summary>GIF LZW 编码（标准 LZW 算法，使用 (prevCode &lt;&lt; 8) | nextByte 哈希表）</summary>
    private static Byte[] LzwEncode(Byte[] indices, Int32 colorCount)
    {
        var minCodeSize = colorCount <= 2 ? 2 : (Int32)Math.Ceiling(Math.Log(colorCount, 2));
        if (minCodeSize < 2) minCodeSize = 2;

        var clearCode = 1 << minCodeSize;
        var eoiCode = clearCode + 1;
        var initialCodeSize = minCodeSize + 1;

        using (var ms = new MemoryStream())
        {
            ms.WriteByte((Byte)minCodeSize);

            var nextCode = eoiCode + 1;
            var codeSize = initialCodeSize;

            // LZW 字典：key = (prevCode << 8) | nextByte, value = newCode
            var dictionary = new Dictionary<Int32, Int32>();

            using (var bitWriter = new BitStreamWriter())
            {
                bitWriter.WriteCode(clearCode, codeSize);

                if (indices.Length > 0)
                {
                    var prevCode = (Int32)indices[0];

                    for (var i = 1; i < indices.Length; i++)
                    {
                        var nextByte = (Int32)indices[i];
                        var key = (prevCode << 8) | nextByte;

                        if (dictionary.TryGetValue(key, out var existingCode))
                        {
                            prevCode = existingCode;
                        }
                        else
                        {
                            bitWriter.WriteCode(prevCode, codeSize);

                            if (nextCode < 4096)
                            {
                                dictionary[key] = nextCode;
                                nextCode++;

                                if (nextCode > (1 << codeSize) && codeSize < 12)
                                    codeSize++;
                            }

                            prevCode = nextByte;

                            // 字典满时清空
                            if (nextCode >= 4096)
                            {
                                bitWriter.WriteCode(clearCode, codeSize);
                                dictionary.Clear();
                                nextCode = eoiCode + 1;
                                codeSize = initialCodeSize;
                            }
                        }
                    }

                    bitWriter.WriteCode(prevCode, codeSize);
                }

                bitWriter.WriteCode(eoiCode, codeSize);
                bitWriter.Flush();

                WriteSubBlocks(ms, bitWriter.ToArray());
            }

            return ms.ToArray();
        }
    }

    private static void WriteSubBlocks(MemoryStream ms, Byte[] data)
    {
        var offset = 0;
        while (offset < data.Length)
        {
            var chunkSize = Math.Min(255, data.Length - offset);
            ms.WriteByte((Byte)chunkSize);
            ms.Write(data, offset, chunkSize);
            offset += chunkSize;
        }
        ms.WriteByte(0); // 块终结
    }

    #endregion

    #region 位流写入器

    private class BitStreamWriter : IDisposable
    {
        private readonly List<Byte> _buffer = [];
        private Int32 _bitsInBuffer;
        private UInt32 _bufferBits;

        public void WriteCode(Int32 code, Int32 bitCount)
        {
            _bufferBits |= (UInt32)(code << _bitsInBuffer);
            _bitsInBuffer += bitCount;

            while (_bitsInBuffer >= 8)
            {
                _buffer.Add((Byte)(_bufferBits & 0xFF));
                _bufferBits >>= 8;
                _bitsInBuffer -= 8;
            }
        }

        public void Flush()
        {
            if (_bitsInBuffer > 0)
            {
                _buffer.Add((Byte)(_bufferBits & 0xFF));
                _bufferBits = 0;
                _bitsInBuffer = 0;
            }
        }

        public Byte[] ToArray() => [.. _buffer];

        public void Dispose() => Flush();
    }

    #endregion

    #region 辅助

    private static void WriteString(MemoryStream ms, String s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        ms.Write(bytes, 0, bytes.Length);
    }

    private static void WriteLE16(MemoryStream ms, Int32 value)
    {
        ms.WriteByte((Byte)(value & 0xFF));
        ms.WriteByte((Byte)((value >> 8) & 0xFF));
    }

    private static Byte[] EnsureRgb24(VideoFrame frame)
    {
        if (frame.PixelFormat == VideoTypes.RGB24)
        {
            var data = new Byte[frame.Data.Length];
            Array.Copy(frame.Data, data, frame.Data.Length);
            return data;
        }

        var rgb = frame.PixelFormat switch
        {
            VideoTypes.YUV420P => PixelFormatConverter.YUV420PToRGB24(frame.Data, frame.Width, frame.Height),
            VideoTypes.NV12 => PixelFormatConverter.NV12ToRGB24(frame.Data, frame.Width, frame.Height),
            VideoTypes.NV21 => PixelFormatConverter.NV21ToRGB24(frame.Data, frame.Width, frame.Height),
            VideoTypes.BGRA32 => PixelFormatConverter.BGRA32ToRGB24(frame.Data, frame.Width, frame.Height),
            _ => PixelFormatConverter.YUV420PToRGB24(frame.Data, frame.Width, frame.Height),
        };
        return rgb ?? [];
    }

    #endregion
}
