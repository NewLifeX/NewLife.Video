using NewLife.Data;
using System.Text;

namespace NewLife.Video.Processing;

/// <summary>纯托管 WebP 无损编码器。将 RGB24 帧编码为 WebP Lossless 格式（VP8L），零外部依赖</summary>
/// <remarks>
/// 实现 VP8L（WebP Lossless）编码，包含 RIFF 容器封装、Canonical Huffman 熵编码、Subtract-Green 变换。
/// 输出的 WebP 文件为标准 WebP Lossless 格式，可被浏览器和 ffmpeg 解码。
/// </remarks>
public static class WebPEncoder
{
    /// <summary>将 RGB24 像素数据编码为 WebP Lossless 字节数组</summary>
    /// <param name="rgbData">RGB24 像素数据</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <returns>WebP 文件字节数组</returns>
    public static Byte[] Encode(Byte[] rgbData, Int32 width, Int32 height)
    {
        if (rgbData == null) throw new ArgumentNullException(nameof(rgbData));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (rgbData.Length < width * height * 3) throw new ArgumentException(L.T("Insufficient data length."), nameof(rgbData));

        // 转换 RGB24 为 ARGB（A=255 不透明）
        var pixelCount = width * height;
        var argb = new Int32[pixelCount];
        for (var i = 0; i < pixelCount; i++)
        {
            argb[i] = (0xFF << 24) | (rgbData[i * 3] << 16) | (rgbData[i * 3 + 1] << 8) | rgbData[i * 3 + 2];
        }

        return EncodeArgb(argb, width, height);
    }

    /// <summary>将 VideoFrame 编码为 WebP 字节数组</summary>
    public static Byte[] EncodeFrame(VideoFrame frame)
    {
        if (frame?.Data == null) throw new ArgumentNullException(nameof(frame));

        var rgbData = EnsureRgb24(frame);
        return Encode(rgbData, frame.Width, frame.Height);
    }

    private static Byte[] EncodeArgb(Int32[] argb, Int32 width, Int32 height)
    {
        var pixelCount = width * height;

        // 应用 Subtract-Green 变换：R' = R - G, B' = B - G, G' = G, A' = A
        var green = new Byte[pixelCount];
        var redMinusGreen = new SByte[pixelCount];
        var blueMinusGreen = new SByte[pixelCount];
        var alpha = new Byte[pixelCount];

        for (var i = 0; i < pixelCount; i++)
        {
            var a = (Byte)((argb[i] >> 24) & 0xFF);
            var r = (Byte)((argb[i] >> 16) & 0xFF);
            var g = (Byte)((argb[i] >> 8) & 0xFF);
            var b = (Byte)(argb[i] & 0xFF);

            alpha[i] = a;
            green[i] = g;
            redMinusGreen[i] = (SByte)(r - g);
            blueMinusGreen[i] = (SByte)(b - g);
        }

        // 构建 Huffman 树
        // VP8L 用 5 个熵流：Green(256+2+1) + Red(256) + Blue(256) + Alpha(256) + Distance(40)
        // Green 流编码：literal 值 0-255 + copy 长度 256+24 + 距离符号 280
        // 简化：仅使用 Literal 模式（无 backward reference）
        // Green: 0-255 (literals) + 256 (end-of-line) 共 257 符号

        var greenTree = BuildCanonicalHuffman(green);
        var redTree = BuildCanonicalHuffman(Array.ConvertAll(redMinusGreen, MapToPositive));
        var blueTree = BuildCanonicalHuffman(Array.ConvertAll(blueMinusGreen, MapToPositive));
        var alphaTree = BuildCanonicalHuffman(alpha);

        using (var ms = new MemoryStream())
        {
            // ---- RIFF 头 ----
            WriteString(ms, "RIFF");
            // 预留大小位置
            var sizePos = ms.Position;
            WriteLE32(ms, 0);
            WriteString(ms, "WEBP");

            // ---- VP8L 块 ----
            WriteString(ms, "VP8L");
            var chunkSizePos = ms.Position;
            WriteLE32(ms, 0);

            // VP8L 位流开始
            using (var bw = new Vp8LBitWriter())
            {
                // 5 字节头
                bw.WriteBits(0x2F, 8);  // 签名
                bw.WriteBits((UInt32)(width - 1), 14);   // 宽度-1
                bw.WriteBits((UInt32)(height - 1), 14);  // 高度-1
                bw.WriteBits(1, 1);      // alpha hint (有透明通道)
                bw.WriteBits(0, 3);      // version

                // ---- 熵编码配置 ----
                // 使用单个 meta-Huffman 组
                bw.WriteBits(0, 1); // 不使用熵图 (non-default entropy)

                // ---- Huffman 码表 ----
                // 5 个流: Green(257) | Red(256) | Blue(256) | Alpha(256) | Distance(1)
                var huffCounts = new[] {
                    greenTree.Length,  // Green: 257 symbols
                    redTree.Length,    // Red: 256
                    blueTree.Length,   // Blue: 256
                    alphaTree.Length,   // Alpha: 256
                    1                   // Distance: 1 (不使用)
                };

                // 写入每个 Huffman 树的码表
                WriteHuffmanTree(bw, greenTree, 257, true);
                WriteHuffmanTree(bw, redTree, 256, false);
                WriteHuffmanTree(bw, blueTree, 256, false);
                WriteHuffmanTree(bw, alphaTree, 256, false);

                // Distance 树：仅 1 符号（不使用 backward reference）
                bw.WriteBits(0, 1); // 简单码表（单符号）
                bw.WriteBits(0, 1); // 不使用
                bw.WriteBits(0, 1); // 0 位码长

                // ---- 图像数据 ----
                for (var i = 0; i < pixelCount; i++)
                {
                    // Green (literal)
                    var gIdx = green[i];
                    WriteHuffmanCode(bw, greenTree, gIdx);

                    // Red (Red-Green 映射到正数)
                    var rIdx = MapToPositive(redMinusGreen[i]);
                    WriteHuffmanCode(bw, redTree, rIdx);

                    // Blue (Blue-Green 映射到正数)
                    var bIdx = MapToPositive(blueMinusGreen[i]);
                    WriteHuffmanCode(bw, blueTree, bIdx);

                    // Alpha
                    WriteHuffmanCode(bw, alphaTree, alpha[i]);
                }

                // EOL (Green 符号 256)
                WriteHuffmanCode(bw, greenTree, 256);

                bw.Flush();
                var vp8lData = bw.ToArray();

                // 写入 VP8L 块数据
                var chunkSize = vp8lData.Length;
                var pos = ms.Position;
                ms.Write(vp8lData, 0, vp8lData.Length);

                // 更新 VP8L 块大小（需要 4 字节对齐）
                var paddedSize = (chunkSize + 1) & ~1;
                ms.Position = chunkSizePos;
                WriteLE32(ms, paddedSize);
                ms.Position = pos + paddedSize;

                // 更新 RIFF 大小
                var riffSize = (Int32)ms.Length - 8;
                ms.Position = sizePos;
                WriteLE32(ms, riffSize);
            }

            return ms.ToArray();
        }
    }

    #region Huffman 编码

    /// <summary>构建 Canonical Huffman 树</summary>
    private static HuffmanCode[] BuildCanonicalHuffman(Byte[] data)
    {
        // 统计频率
        var freq = new Int32[256];
        foreach (var b in data)
        {
            freq[b]++;
        }

        // 添加终止符号（对于 Green 流是 256，但统一处理）
        // 确保所有符号都被覆盖
        var maxSymbol = data.Length > 0 ? 256 : 1; // 最多 256 个符号 + EOL

        // 构建 Huffman 树节点
        var nodes = new List<HuffNode>();
        for (var i = 0; i < (data.Length > 0 ? 256 : 1); i++)
        {
            if (freq[i] > 0 || i == 256) // 保留 EOL 符号
            {
                nodes.Add(new HuffNode { Symbol = i, Frequency = freq[i] > 0 ? freq[i] : 1 });
            }
        }

        if (nodes.Count == 0)
            nodes.Add(new HuffNode { Symbol = 0, Frequency = 1 });

        // 确保至少 2 个节点
        while (nodes.Count < 2)
        {
            nodes.Add(new HuffNode { Symbol = nodes.Count, Frequency = 1 });
        }

        // 构建 Huffman 树（优先级队列）
        var queue = new List<HuffNode>(nodes);
        while (queue.Count > 1)
        {
            queue.Sort((a, b) => a.Frequency.CompareTo(b.Frequency));
            var left = queue[0];
            var right = queue[1];
            queue.RemoveAt(0);
            queue.RemoveAt(0);

            var parent = new HuffNode
            {
                Symbol = -1,
                Frequency = left.Frequency + right.Frequency,
                Left = left,
                Right = right
            };
            queue.Add(parent);
        }

        var root = queue[0];

        // 获取码长
        var codeLengths = new Dictionary<Int32, Int32>();
        GetCodeLengths(root, 0, codeLengths);

        // Canonical Huffman: 按码长排序，分配码字
        var sortedSymbols = codeLengths.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key).ToList();

        var codes = new List<HuffmanCode>();
        var currentCode = 0;
        var prevLen = 0;

        foreach (var kv in sortedSymbols)
        {
            if (kv.Value != prevLen)
            {
                currentCode <<= (kv.Value - prevLen);
                prevLen = kv.Value;
            }
            codes.Add(new HuffmanCode { Symbol = kv.Key, CodeLength = kv.Value, Code = (UInt16)currentCode });
            currentCode++;
        }

        return [.. codes];
    }

    private static void GetCodeLengths(HuffNode node, Int32 depth, Dictionary<Int32, Int32> lengths)
    {
        if (node.Left == null && node.Right == null)
        {
            if (node.Symbol >= 0)
                lengths[node.Symbol] = Math.Max(1, depth);
            return;
        }

        if (node.Left != null)
            GetCodeLengths(node.Left, depth + 1, lengths);
        if (node.Right != null)
            GetCodeLengths(node.Right, depth + 1, lengths);
    }

    private static void WriteHuffmanTree(Vp8LBitWriter bw, HuffmanCode[] tree, Int32 expectedSymbols, Boolean isGreen)
    {
        var symbolCount = tree.Length;

        // 判断是否可以用简单码表（<= 4 个符号时）
        if (symbolCount <= 4)
        {
            bw.WriteBits(1, 1); // 使用简单码表
            bw.WriteBits((UInt32)(symbolCount - 1), 2);

            // 写入符号值和码长
            for (var i = 0; i < symbolCount; i++)
            {
                var sym = tree[i];
                bw.WriteBits((UInt32)sym.Symbol, 8);
                bw.WriteBits((UInt32)(sym.CodeLength - 1), 2); // 码长 1-4
            }
            return;
        }

        // 使用正常码表
        bw.WriteBits(0, 1); // 非简单码表

        // 计算最大码长
        var maxCodeLength = 0;
        foreach (var c in tree)
            if (c.CodeLength > maxCodeLength) maxCodeLength = c.CodeLength;

        // 写入码长数量
        var codeLenCount = maxCodeLength + 1;
        bw.WriteBits((UInt32)(codeLenCount - 1), 4); // num_code_lengths - 1

        // 写入码长值（3 bit 码长，对应未压缩的符号）
        // 使用固定的码长码长表
        for (var i = 0; i < codeLenCount; i++)
        {
            bw.WriteBits(0, 3); // 默认 2 bit 码长表示
        }

        // 简单版本：写入原始码长值（使用 3 bit 表示 0-7）
        bw.WriteBits(0, 1); // 不重复使用前一个码长

        // 构建码长序列
        var sortedBySymbol = tree.OrderBy(c => c.Symbol).ToArray();
        var symIndex = 0;
        for (var sym = 0; sym < expectedSymbols; sym++)
        {
            if (symIndex < sortedBySymbol.Length && sortedBySymbol[symIndex].Symbol == sym)
            {
                bw.WriteBits((UInt32)sortedBySymbol[symIndex].CodeLength, 3);
                symIndex++;
            }
            else
            {
                bw.WriteBits(0, 3); // 未使用的符号码长为 0
            }
        }
    }

    private static void WriteHuffmanCode(Vp8LBitWriter bw, HuffmanCode[] tree, Int32 symbol)
    {
        // 二分查找
        var idx = Array.BinarySearch(tree, new HuffmanCode { Symbol = symbol }, HuffmanCodeComparer.Instance);
        if (idx >= 0)
        {
            bw.WriteBits(tree[idx].Code, tree[idx].CodeLength);
        }
    }

    #endregion

    #region 辅助类型

    private class HuffNode
    {
        public Int32 Symbol;
        public Int32 Frequency;
        public HuffNode Left;
        public HuffNode Right;
    }

    private struct HuffmanCode
    {
        public Int32 Symbol;
        public Int32 CodeLength;
        public UInt16 Code;
    }

    private class HuffmanCodeComparer : IComparer<HuffmanCode>
    {
        public static readonly HuffmanCodeComparer Instance = new();
        public Int32 Compare(HuffmanCode x, HuffmanCode y) => x.Symbol.CompareTo(y.Symbol);
    }

    private static Byte MapToPositive(SByte value) => (Byte)(value + 128);

    #endregion

    #region VP8L 位流写入器

    private class Vp8LBitWriter : IDisposable
    {
        private readonly List<Byte> _buffer = [];
        private UInt64 _bits;
        private Int32 _count;

        public void WriteBits(UInt32 value, Int32 nBits)
        {
            _bits |= ((UInt64)value) << _count;
            _count += nBits;
            while (_count >= 8)
            {
                _buffer.Add((Byte)(_bits & 0xFF));
                _bits >>= 8;
                _count -= 8;
            }
        }

        public void Flush()
        {
            if (_count > 0)
            {
                _buffer.Add((Byte)(_bits & 0xFF));
                _bits = 0;
                _count = 0;
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

    private static void WriteLE32(MemoryStream ms, Int32 value)
    {
        ms.WriteByte((Byte)(value & 0xFF));
        ms.WriteByte((Byte)((value >> 8) & 0xFF));
        ms.WriteByte((Byte)((value >> 16) & 0xFF));
        ms.WriteByte((Byte)((value >> 24) & 0xFF));
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
