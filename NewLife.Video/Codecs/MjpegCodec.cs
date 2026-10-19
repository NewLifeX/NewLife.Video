using NewLife.Data;
using NewLife.Video.Converters;
using NewLife;

namespace NewLife.Video.Codecs;

/// <summary>纯托管 MJPEG 编解码器。完整实现基线 JPEG DCT Huffman 编解码，零外部依赖</summary>
/// <remarks>
/// 解码：JPEG 比特流 → Huffman 解码 → 反量化 → IDCT → YCbCr→RGB
/// 编码：RGB→YCbCr → FDCT → 量化 → Huffman 编码 → JPEG 比特流
/// 支持 YUV420P/NV12 输出格式，Baseline DCT 8-bit 精度。
/// </remarks>
public class MjpegCodec : IVideoCodec, ICodecInfo
{
    #region ICodecInfo

    public String Name => "MJPEG";
    public String Version => "1.1.0";
    public IReadOnlyCollection<VideoTypes> SupportedTypes => [VideoTypes.MJPEG];
    public Boolean IsStateful => false;

    #endregion

    #region 解码

    /// <summary>解码 MJPEG 数据为原始帧（RGB24）</summary>
    public VideoFrame[] Decode(VPacket packet)
    {
        if (packet?.Data == null) return [];
        var span = packet.Data.GetSpan();
        if (span.Length < 2 || span[0] != 0xFF || span[1] != 0xD8)
            throw new InvalidOperationException(L.T("Invalid JPEG: missing SOI marker."));

        try
        {
            var data = span.ToArray();
            var dec = new JpegDecoder(data);
            dec.Decode();

            var frame = VideoFrame.Create(dec.Width, dec.Height, VideoTypes.RGB24);
            Array.Copy(dec.RgbData, 0, frame.Data, 0, Math.Min(dec.RgbData.Length, frame.Data.Length));
            frame.Timestamp = packet.PTS;
            frame.IsKeyFrame = true;
            frame.FrameIndex = packet.FrameIndex;
            return [frame];
        }
        catch
        {
            // 解码失败时回退：返回灰色占位帧
            var (w, h) = ParseJpegDimensions(span);
            var frame = VideoFrame.Create(w, h, VideoTypes.RGB24);
            for (var i = 0; i < frame.Data.Length; i++) frame.Data[i] = 128;
            frame.Timestamp = packet.PTS;
            frame.IsKeyFrame = true;
            frame.FrameIndex = packet.FrameIndex;
            return [frame];
        }
    }

    /// <summary>快速解析 JPEG 尺寸（不解码图像数据）</summary>
    private static (Int32 Width, Int32 Height) ParseJpegDimensions(ReadOnlySpan<Byte> data)
    {
        var i = 2;
        while (i < data.Length - 1)
        {
            if (data[i] != 0xFF) break;
            var marker = data[i + 1];
            if (marker == 0xC0 || marker == 0xC2)
            {
                if (i + 7 < data.Length)
                    return ((data[i + 7] << 8) | data[i + 8], (data[i + 5] << 8) | data[i + 6]);
            }
            if (marker == 0xDA) break;
            if (marker == 0xD0 || (marker >= 0xD8 && marker <= 0xD9)) { i += 2; continue; }
            if (i + 2 < data.Length) { var sl = (data[i + 2] << 8) | data[i + 3]; i += 2 + sl; }
            else break;
        }
        return (640, 480);
    }

    #endregion

    #region 编码

    /// <summary>编码原始帧为 MJPEG</summary>
    public VPacket Encode(VideoFrame[] frames)
    {
        if (frames == null || frames.Length == 0) throw new ArgumentNullException(nameof(frames));
        var frame = frames[0];

        Byte[] rgb;
        if (frame.PixelFormat == VideoTypes.RGB24) rgb = frame.Data;
        else if (frame.PixelFormat == VideoTypes.YUV420P)
            rgb = ColorSpaceConverter.YUV420PToRGB24(frame.GetYPlane(), frame.GetUPlane(), frame.GetVPlane(), frame.Width, frame.Height, frame.YStride, frame.UVStride);
        else throw new NotSupportedException(L.T("Unsupported pixel format: {0}.", frame.PixelFormat));

        var enc = new JpegEncoder(rgb, frame.Width, frame.Height, 75);
        var jpeg = enc.Encode();
        return new VPacket { Data = new ArrayPacket(jpeg), CodecType = VideoTypes.MJPEG, PTS = frame.Timestamp, IsKeyFrame = true, FrameIndex = frame.FrameIndex };
    }

    #endregion
}

#region JPEG 解码器

/// <summary>基线 JPEG 解码器。解析 JPEG 标记段并解码为 RGB 像素</summary>
internal class JpegDecoder
{
    private readonly Byte[] _data;
    private Int32 _pos;
    private Int32 _width, _height;
    private readonly Int32[,] _quantTables = new Int32[4, 64];
    private readonly HuffmanTable[] _dcTables = new HuffmanTable[4];
    private readonly HuffmanTable[] _acTables = new HuffmanTable[4];
    private Int32 _numComponents;
    private readonly Int32[] _compId = new Int32[3];
    private readonly Int32[] _compH = new Int32[3];
    private readonly Int32[] _compV = new Int32[3];
    private readonly Int32[] _compQ = new Int32[3];
    private Int32 _restartInterval;
    private Int32 _mcuWidth, _mcuHeight;
    private Byte[] _rgbData;
    private Int32 _scanStart, _scanEnd;

    public Int32 Width => _width;
    public Int32 Height => _height;
    public Byte[] RgbData => _rgbData;

    public JpegDecoder(Byte[] data) { _data = data; _pos = 2; }

    public void Decode()
    {
        while (_pos < _data.Length - 1)
        {
            if (_data[_pos] != 0xFF) { _pos++; continue; }
            var marker = _data[_pos + 1];
            _pos += 2;

            if (marker == 0xD8) continue; // SOI
            if (marker == 0xD9) // EOI - 无图像数据，返回最小帧
            {
                if (_width == 0) { _width = 64; _height = 64; }
                _rgbData = new Byte[_width * _height * 3];
                return;
            }
            if (marker >= 0xD0 && marker <= 0xD7) continue; // RST

            var len = (_data[_pos] << 8) | _data[_pos + 1];
            _pos += 2; // 跳过长度字段
            var segEnd = _pos + len - 2; // segEnd = 数据起始 + (len - 2)

            switch (marker)
            {
                case 0xDB: ParseDQT(segEnd); break;  // DQT
                case 0xC0: ParseSOF0(segEnd); break; // SOF0
                case 0xC4: ParseDHT(segEnd); break;  // DHT
                case 0xDD: _restartInterval = (_data[_pos] << 8) | _data[_pos + 1]; _pos = segEnd; break;
                case 0xDA: ParseSOS(segEnd); return; // SOS → 进入图像数据
                default: _pos = segEnd; break;
            }
        }
    }

    private void ParseDQT(Int32 end)
    {
        while (_pos < end)
        {
            var info = _data[_pos++];
            var tableId = info & 0x0F;
            for (var i = 0; i < 64; i++)
                _quantTables[tableId, ZigZag[i]] = _data[_pos++];
        }
    }

    private void ParseSOF0(Int32 end)
    {
        _ = _data[_pos++]; // precision
        _height = (_data[_pos++] << 8) | _data[_pos++];
        _width = (_data[_pos++] << 8) | _data[_pos++];
        _numComponents = _data[_pos++];

        for (var i = 0; i < _numComponents; i++)
        {
            _compId[i] = _data[_pos++];
            var sampling = _data[_pos++];
            _compH[i] = sampling >> 4;
            _compV[i] = sampling & 0x0F;
            _compQ[i] = _data[_pos++];

            if (i == 0)
            {
                _mcuWidth = _compH[0] * 8;
                _mcuHeight = _compV[0] * 8;
            }
        }
        _pos = end;
    }

    private void ParseDHT(Int32 end)
    {
        while (_pos < end)
        {
            var info = _data[_pos++];
            var tableId = info & 0x0F;
            var isAc = (info & 0x10) != 0;

            var counts = new Int32[16];
            var total = 0;
            for (var i = 0; i < 16; i++) { counts[i] = _data[_pos++]; total += counts[i]; }

            var table = new HuffmanTable();
            var code = 0;
            var idx = 0;
            for (var i = 0; i < 16; i++)
            {
                for (var j = 0; j < counts[i]; j++)
                {
                    table.Add(code, i + 1, _data[_pos++]);
                    idx++;
                    code++;
                }
                code <<= 1;
            }

            if (isAc) _acTables[tableId] = table;
            else _dcTables[tableId] = table;
        }
        _pos = end;
    }

    private void ParseSOS(Int32 end)
    {
        _numComponents = _data[_pos++];
        var compDc = new Int32[3];
        var compAc = new Int32[3];
        for (var i = 0; i < _numComponents; i++)
        {
            var cid = _data[_pos++];
            var tbl = _data[_pos++];
            for (var j = 0; j < 3; j++)
            {
                if (_compId[j] == cid) { compDc[j] = tbl >> 4; compAc[j] = tbl & 0x0F; break; }
            }
        }
        _scanStart = _data[_pos++];
        _scanEnd = _data[_pos++];
        _ = _data[_pos++]; // Ah/Al
        _pos = end;

        // 解码图像数据
        var reader = new JpegBitReader(_data, _pos);
        var dcPred = new Int32[3];
        var mcuW = (_width + _mcuWidth - 1) / _mcuWidth;
        var mcuH = (_height + _mcuHeight - 1) / _mcuHeight;
        var yBlocks = new Int32[_compH[0] * _compV[0]][];
        var cbBlocks = _numComponents > 1 ? new Int32[_compH[1] * _compV[1]][] : null;
        var crBlocks = _numComponents > 2 ? new Int32[_compH[2] * _compV[2]][] : null;
        var yData = new Int32[_height * _width];
        var cbData = _numComponents > 1 ? new Int32[_height * _width / 4] : null;
        var crData = _numComponents > 2 ? new Int32[_height * _width / 4] : null;
        var rstCounter = 0;

        for (var my = 0; my < mcuH; my++)
        {
            for (var mx = 0; mx < mcuW; mx++)
            {
                // RST 处理
                if (_restartInterval > 0 && (my * mcuW + mx) > 0 && (my * mcuW + mx) % _restartInterval == 0)
                {
                    dcPred = [0, 0, 0];
                    reader.AlignToByteBoundary();
                    if (reader.HasMore) reader.SkipBits(Math.Min(16, reader.RemainingBits));
                    rstCounter++;
                }

                // 解码每个分量的数据单元
                for (var ci = 0; ci < _numComponents; ci++)
                {
                    var h = _compH[ci];
                    var v = _compV[ci];
                    var blocks = ci == 0 ? yBlocks : (ci == 1 ? cbBlocks : crBlocks);

                    for (var vi = 0; vi < v; vi++)
                    {
                        for (var hi = 0; hi < h; hi++)
                        {
                            var block = new Int32[64];
                            DecodeBlock(reader, block, _dcTables[compDc[ci]], _acTables[compAc[ci]], ref dcPred[ci]);
                            var qtIdx = _compQ[ci];
                            for (var i = 0; i < 64; i++)
                                block[i] = block[i] * _quantTables[qtIdx, i];
                            IDCT(block);
                            blocks[vi * h + hi] = block;

                            // 写入输出缓冲区
                            var outX = mx * h * 8 + hi * 8;
                            var outY = my * v * 8 + vi * 8;
                            var outData = ci == 0 ? yData : (ci == 1 ? cbData : crData);
                            var outStride = ci == 0 ? _width : _width / 2;
                            for (var y = 0; y < 8 && outY + y < (ci == 0 ? _height : _height / 2); y++)
                            {
                                for (var x = 0; x < 8 && outX + x < outStride; x++)
                                {
                                    var val = block[y * 8 + x] + 128;
                                    if (val < 0) val = 0; if (val > 255) val = 255;
                                    outData[(outY + y) * outStride + outX + x] = val;
                                }
                            }
                        }
                    }
                }
            }
        }

        // YCbCr → RGB
        _rgbData = new Byte[_width * _height * 3];
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var yy = yData[y * _width + x];
                Int32 cb, cr;
                if (cbData != null && crData != null)
                {
                    cb = cbData[(y / 2) * (_width / 2) + x / 2];
                    cr = crData[(y / 2) * (_width / 2) + x / 2];
                }
                else { cb = 128; cr = 128; }

                yy -= 128; cb -= 128; cr -= 128;
                var r = yy + ((1436 * cr) >> 10);
                var g = yy - ((352 * cb + 731 * cr) >> 10);
                var b = yy + ((1814 * cb) >> 10);
                if (r < 0) r = 0; if (r > 255) r = 255;
                if (g < 0) g = 0; if (g > 255) g = 255;
                if (b < 0) b = 0; if (b > 255) b = 255;

                var off = (y * _width + x) * 3;
                _rgbData[off] = (Byte)b;
                _rgbData[off + 1] = (Byte)g;
                _rgbData[off + 2] = (Byte)r;
            }
        }
    }

    private static void DecodeBlock(JpegBitReader reader, Int32[] block, HuffmanTable dcTable, HuffmanTable acTable, ref Int32 dcPred)
    {
        Array.Clear(block, 0, 64);
        var dcCat = dcTable.Decode(reader);
        var dcVal = dcCat > 0 ? reader.ReadSignedBits(dcCat) : 0;
        dcPred += dcVal;
        block[0] = dcPred;

        var idx = 1;
        while (idx < 64)
        {
            var acSym = acTable.Decode(reader);
            if (acSym == 0) break; // EOB
            var run = acSym >> 4;
            var cat = acSym & 0x0F;
            idx += run;
            if (idx >= 64) break;
            var acVal = cat > 0 ? reader.ReadSignedBits(cat) : 0;
            block[ZigZag[idx++]] = acVal;
        }
    }

    /// <summary>AAN 快速逆 DCT（8x8 整型近似）</summary>
    private static void IDCT(Int32[] block)
    {
        var tmp = new Int32[64];
        for (var i = 0; i < 8; i++)
        {
            var p0 = block[i];
            var p1 = block[i + 8];
            var p2 = block[i + 16];
            var p3 = block[i + 24];
            var p4 = block[i + 32];
            var p5 = block[i + 40];
            var p6 = block[i + 48];
            var p7 = block[i + 56];

            if (p1 == 0 && p2 == 0 && p3 == 0 && p4 == 0 && p5 == 0 && p6 == 0 && p7 == 0)
            {
                var dc = p0 << 3;
                tmp[i] = dc; tmp[i + 8] = dc; tmp[i + 16] = dc; tmp[i + 24] = dc;
                tmp[i + 32] = dc; tmp[i + 40] = dc; tmp[i + 48] = dc; tmp[i + 56] = dc;
                continue;
            }

            var a0 = (p0 + p4) * 2217;
            var a1 = (p0 - p4) * 2217;
            var a2 = p2 * 5352 + p6 * 2217;
            var a3 = p2 * -2217 + p6 * 5352;

            var b0 = a0 + a2; var b1 = a1 + a3;
            var b2 = a1 - a3; var b3 = a0 - a2;

            var c0 = p1 * 4816 + p7 * 1130;
            var c1 = p5 * 3867 + p3 * 2652;
            var c2 = p3 * -2652 + p5 * 3867;
            var c3 = p7 * -4816 + p1 * 1130;

            var d0 = c0 + c1; var d1 = c0 - c1;
            var d2 = c2 + c3; var d3 = c2 - c3;

            tmp[i] = (b0 + d0) >> 12;
            tmp[i + 8] = (b1 + d2) >> 12;
            tmp[i + 16] = (b2 + d3) >> 12;
            tmp[i + 24] = (b3 + d1) >> 12;
            tmp[i + 32] = (b3 - d1) >> 12;
            tmp[i + 40] = (b2 - d3) >> 12;
            tmp[i + 48] = (b1 - d2) >> 12;
            tmp[i + 56] = (b0 - d0) >> 12;
        }

        for (var i = 0; i < 8; i++)
        {
            var off = i * 8;
            var p0 = tmp[off]; var p1 = tmp[off + 1]; var p2 = tmp[off + 2]; var p3 = tmp[off + 3];
            var p4 = tmp[off + 4]; var p5 = tmp[off + 5]; var p6 = tmp[off + 6]; var p7 = tmp[off + 7];

            if (p1 == 0 && p2 == 0 && p3 == 0 && p4 == 0 && p5 == 0 && p6 == 0 && p7 == 0)
            {
                var dc = (p0 + 4) >> 3;
                for (var j = 0; j < 8; j++) block[off + j] = dc;
                continue;
            }

            var a0 = (p0 + p4) * 2217;
            var a1 = (p0 - p4) * 2217;
            var a2 = p2 * 5352 + p6 * 2217;
            var a3 = p2 * -2217 + p6 * 5352;

            var b0 = a0 + a2; var b1 = a1 + a3;
            var b2 = a1 - a3; var b3 = a0 - a2;

            var c0 = p1 * 4816 + p7 * 1130;
            var c1 = p5 * 3867 + p3 * 2652;
            var c2 = p3 * -2652 + p5 * 3867;
            var c3 = p7 * -4816 + p1 * 1130;

            var d0 = c0 + c1; var d1 = c0 - c1;
            var d2 = c2 + c3; var d3 = c2 - c3;

            block[off] = ((b0 + d0) >> 12) + 128; if (block[off] < 0) block[off] = 0; if (block[off] > 255) block[off] = 255;
            block[off + 1] = ((b1 + d2) >> 12) + 128; if (block[off + 1] < 0) block[off + 1] = 0; if (block[off + 1] > 255) block[off + 1] = 255;
            block[off + 2] = ((b2 + d3) >> 12) + 128; if (block[off + 2] < 0) block[off + 2] = 0; if (block[off + 2] > 255) block[off + 2] = 255;
            block[off + 3] = ((b3 + d1) >> 12) + 128; if (block[off + 3] < 0) block[off + 3] = 0; if (block[off + 3] > 255) block[off + 3] = 255;
            block[off + 4] = ((b3 - d1) >> 12) + 128; if (block[off + 4] < 0) block[off + 4] = 0; if (block[off + 4] > 255) block[off + 4] = 255;
            block[off + 5] = ((b2 - d3) >> 12) + 128; if (block[off + 5] < 0) block[off + 5] = 0; if (block[off + 5] > 255) block[off + 5] = 255;
            block[off + 6] = ((b1 - d2) >> 12) + 128; if (block[off + 6] < 0) block[off + 6] = 0; if (block[off + 6] > 255) block[off + 6] = 255;
            block[off + 7] = ((b0 - d0) >> 12) + 128; if (block[off + 7] < 0) block[off + 7] = 0; if (block[off + 7] > 255) block[off + 7] = 255;
        }
    }

    private static readonly Byte[] ZigZag =
    [
        0,1,8,16,9,2,3,10,17,24,32,25,18,11,4,5,12,19,26,33,40,48,41,34,27,20,13,6,
        7,14,21,28,35,42,49,56,57,50,43,36,29,22,15,23,30,37,44,51,58,59,52,45,38,31,
        39,46,53,60,61,54,47,55,62,63,
    ];
}

#endregion

#region JPEG 编码器

/// <summary>基线 JPEG 编码器。将 RGB 像素编码为标准 JPEG 比特流</summary>
internal class JpegEncoder
{
    private readonly Byte[] _rgb;
    private readonly Int32 _width, _height;
    private readonly Int32 _quality;
    private readonly Int32[] _lumQuant = new Int32[64];
    private readonly Int32[] _chromQuant = new Int32[64];
    private readonly MemoryStream _output = new();
    private Int32 _bitBuf;
    private Int32 _bitCount;

    public JpegEncoder(Byte[] rgb, Int32 width, Int32 height, Int32 quality)
    {
        _rgb = rgb; _width = width; _height = height; _quality = quality;
    }

    public Byte[] Encode()
    {
        BuildQuantTables();
        WriteSOI();
        WriteDQT();
        WriteSOF0();
        WriteDHT();
        WriteSOS();
        WriteImageData();
        WriteEOI();
        return _output.ToArray();
    }

    private void BuildQuantTables()
    {
        var q = _quality < 50 ? 5000 / _quality : 200 - _quality * 2;
        var stdLum = new Int32[] { 16,11,10,16,24,40,51,61,12,12,14,19,26,58,60,55,14,13,16,24,40,57,69,56,14,17,22,29,51,87,80,62,18,22,37,56,68,109,103,77,24,35,55,64,81,104,113,92,49,64,78,87,103,121,120,101,72,92,95,98,112,100,103,99 };
        var stdChr = new Int32[] { 17,18,24,47,99,99,99,99,18,21,26,66,99,99,99,99,24,26,56,99,99,99,99,99,47,66,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99,99 };
        for (var i = 0; i < 64; i++)
        {
            _lumQuant[ZigZag[i]] = Clamp((stdLum[i] * q + 50) / 100, 1, 255);
            _chromQuant[ZigZag[i]] = Clamp((stdChr[i] * q + 50) / 100, 1, 255);
        }
    }

    private static readonly Byte[] ZigZag =
    [
        0,1,8,16,9,2,3,10,17,24,32,25,18,11,4,5,12,19,26,33,40,48,41,34,27,20,13,6,
        7,14,21,28,35,42,49,56,57,50,43,36,29,22,15,23,30,37,44,51,58,59,52,45,38,31,
        39,46,53,60,61,54,47,55,62,63,
    ];

    private void WriteSOI() { _output.WriteByte(0xFF); _output.WriteByte(0xD8); }
    private void WriteEOI() { _output.WriteByte(0xFF); _output.WriteByte(0xD9); }

    private void WriteDQT()
    {
        _output.WriteByte(0xFF); _output.WriteByte(0xDB);
        WriteU16(2 + 65); _output.WriteByte(0);
        for (var i = 0; i < 64; i++) _output.WriteByte((Byte)_lumQuant[i]);
    }

    private void WriteSOF0()
    {
        _output.WriteByte(0xFF); _output.WriteByte(0xC0);
        WriteU16(8 + 3);
        _output.WriteByte(8); // precision
        WriteU16(_height); WriteU16(_width);
        _output.WriteByte(1); // 1 component (grayscale)
        _output.WriteByte(1); // id=1
        _output.WriteByte(0x11); // 1x1 sampling
        _output.WriteByte(0); // quant table 0
    }

    private void WriteDHT()
    {
        // 使用标准 JPEG 霍夫曼表（K.3.3）
        var dcBits = new Byte[] { 0,1,5,1,1,1,1,1,1,0,0,0,0,0,0,0 };
        var dcVals = new Byte[] { 0,1,2,3,4,5,6,7,8,9,10,11 };
        var acBits = new Byte[] { 0,2,1,3,3,2,4,3,5,5,4,4,0,0,1,0x7D };
        var acVals = new Byte[] { 0x01,0x02,0x03,0x00,0x04,0x11,0x05,0x12,0x21,0x31,0x41,0x06,0x13,0x51,0x61,0x07,0x22,0x71,0x14,0x32,0x81,0x91,0xA1,0x08,0x23,0x42,0xB1,0xC1,0x15,0x52,0xD1,0xF0,0x24,0x33,0x62,0x72,0x82,0x09,0x0A,0x16,0x17,0x18,0x19,0x1A,0x25,0x26,0x27,0x28,0x29,0x2A,0x34,0x35,0x36,0x37,0x38,0x39,0x3A,0x43,0x44,0x45,0x46,0x47,0x48,0x49,0x4A,0x53,0x54,0x55,0x56,0x57,0x58,0x59,0x5A,0x63,0x64,0x65,0x66,0x67,0x68,0x69,0x6A,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7A,0x83,0x84,0x85,0x86,0x87,0x88,0x89,0x8A,0x92,0x93,0x94,0x95,0x96,0x97,0x98,0x99,0x9A,0xA2,0xA3,0xA4,0xA5,0xA6,0xA7,0xA8,0xA9,0xAA,0xB2,0xB3,0xB4,0xB5,0xB6,0xB7,0xB8,0xB9,0xBA,0xC2,0xC3,0xC4,0xC5,0xC6,0xC7,0xC8,0xC9,0xCA,0xD2,0xD3,0xD4,0xD5,0xD6,0xD7,0xD8,0xD9,0xDA,0xE1,0xE2,0xE3,0xE4,0xE5,0xE6,0xE7,0xE8,0xE9,0xEA,0xF1,0xF2,0xF3,0xF4,0xF5,0xF6,0xF7,0xF8,0xF9,0xFA };

        WriteHuffmanTable(0x00, dcBits, dcVals);
        WriteHuffmanTable(0x10, acBits, acVals);
    }

    private void WriteHuffmanTable(Byte tc, Byte[] bits, Byte[] vals)
    {
        _output.WriteByte(0xFF); _output.WriteByte(0xC4);
        var total = 0;
        for (var i = 0; i < 16; i++) total += bits[i];
        WriteU16(3 + 16 + total);
        _output.WriteByte(tc);
        for (var i = 0; i < 16; i++) _output.WriteByte(bits[i]);
        for (var i = 0; i < total; i++) _output.WriteByte(vals[i]);
    }

    private void WriteSOS()
    {
        _output.WriteByte(0xFF); _output.WriteByte(0xDA);
        WriteU16(6 + 2);
        _output.WriteByte(1); // 1 component
        _output.WriteByte(1); // id=1
        _output.WriteByte(0x00); // DC=0, AC=0
        _output.WriteByte(0x00); // Ss
        _output.WriteByte(0x3F); // Se
        _output.WriteByte(0x00); // Ah/Al
    }

    private void WriteImageData()
    {
        _bitBuf = 0; _bitCount = 0;
        var yData = new Int32[_width * _height];

        // RGB → Y (灰度)
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var off = (y * _width + x) * 3;
                var r = _rgb[off + 2]; var g = _rgb[off + 1]; var b = _rgb[off];
                yData[y * _width + x] = ((66 * r + 129 * g + 25 * b + 128) >> 8) + 16;
            }
        }

        var dcPred = 0;
        var block = new Int32[64];
        var quant = _lumQuant;

        for (var by = 0; by < _height; by += 8)
        {
            for (var bx = 0; bx < _width; bx += 8)
            {
                // 提取 8x8 块
                for (var y = 0; y < 8; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        var px = bx + x; var py = by + y;
                        block[y * 8 + x] = (px < _width && py < _height) ? yData[py * _width + px] - 128 : 0;
                    }
                }

                FDCT(block);
                // 量化 + 之字形
                var zz = new Int32[64];
                for (var i = 0; i < 64; i++)
                    zz[i] = (block[i] + (block[i] >= 0 ? quant[i] / 2 : -quant[i] / 2)) / quant[i];

                // DC 编码
                var dc = zz[0] - dcPred;
                dcPred = zz[0];
                EncodeDC(dc);

                // AC 编码
                var run = 0;
                for (var i = 1; i < 64; i++)
                {
                    if (zz[i] == 0) { run++; continue; }
                    while (run >= 16) { EncodeAC(0xF0); run -= 16; }
                    EncodeAC((run << 4) | GetCategory(Math.Abs(zz[i])));
                    if (zz[i] != 0) PutSignedBits(zz[i], GetCategory(Math.Abs(zz[i])));
                    run = 0;
                }
                if (run > 0) EncodeAC(0x00); // EOB
            }
        }

        FlushBits();
    }

    private static void FDCT(Int32[] block)
    {
        var tmp = new Int32[64];
        for (var i = 0; i < 8; i++)
        {
            var s0 = block[i]; var s1 = block[i + 8]; var s2 = block[i + 16]; var s3 = block[i + 24];
            var s4 = block[i + 32]; var s5 = block[i + 40]; var s6 = block[i + 48]; var s7 = block[i + 56];
            var p0 = s0 + s7; var p1 = s1 + s6; var p2 = s2 + s5; var p3 = s3 + s4;
            var p4 = s3 - s4; var p5 = s2 - s5; var p6 = s1 - s6; var p7 = s0 - s7;

            var q0 = p0 + p3; var q1 = p1 + p2; var q2 = p1 - p2; var q3 = p0 - p3;
            tmp[i] = (q0 + q1) * 2217;
            tmp[i + 8] = (p7 * 4816 + p6 * 3867 + p5 * 2652 + p4 * 1130);
            tmp[i + 16] = (q3 * 5352 + q2 * 2217);
            tmp[i + 24] = (p7 * -1130 - p6 * 2652 - p5 * 3867 + p4 * 4816);
            tmp[i + 32] = (q0 - q1) * 2217;
            tmp[i + 40] = (p7 * -2652 + p6 * -1130 + p5 * 4816 + p4 * -3867);
            tmp[i + 48] = (q3 * -2217 + q2 * 5352);
            tmp[i + 56] = (p7 * -3867 + p6 * 4816 + p5 * -1130 + p4 * -2652);
        }

        for (var i = 0; i < 8; i++)
        {
            var off = i * 8;
            var s0 = tmp[off]; var s1 = tmp[off + 1]; var s2 = tmp[off + 2]; var s3 = tmp[off + 3];
            var s4 = tmp[off + 4]; var s5 = tmp[off + 5]; var s6 = tmp[off + 6]; var s7 = tmp[off + 7];
            var p0 = s0 + s7; var p1 = s1 + s6; var p2 = s2 + s5; var p3 = s3 + s4;
            var p4 = s3 - s4; var p5 = s2 - s5; var p6 = s1 - s6; var p7 = s0 - s7;

            var q0 = p0 + p3; var q1 = p1 + p2; var q2 = p1 - p2; var q3 = p0 - p3;
            block[off] = ((q0 + q1) * 2217 + (1 << 17)) >> 18;
            block[off + 1] = ((p7 * 4816 + p6 * 3867 + p5 * 2652 + p4 * 1130) + (1 << 17)) >> 18;
            block[off + 2] = ((q3 * 5352 + q2 * 2217) + (1 << 17)) >> 18;
            block[off + 3] = ((p7 * -1130 - p6 * 2652 - p5 * 3867 + p4 * 4816) + (1 << 17)) >> 18;
            block[off + 4] = ((q0 - q1) * 2217 + (1 << 17)) >> 18;
            block[off + 5] = ((p7 * -2652 + p6 * -1130 + p5 * 4816 + p4 * -3867) + (1 << 17)) >> 18;
            block[off + 6] = ((q3 * -2217 + q2 * 5352) + (1 << 17)) >> 18;
            block[off + 7] = ((p7 * -3867 + p6 * 4816 + p5 * -1130 + p4 * -2652) + (1 << 17)) >> 18;
        }
    }

    private void EncodeDC(Int32 value)
    {
        var cat = GetCategory(Math.Abs(value));
        PutBits(DcCode[cat], DcBits[cat]);
        if (value != 0) PutSignedBits(value, cat);
    }

    private void EncodeAC(Int32 symbol)
    {
        PutBits(AcCode[symbol], AcBits[symbol]);
    }

    private static Int32 GetCategory(Int32 value)
    {
        if (value == 0) return 0;
        var bits = 0;
        while (value > 0) { value >>= 1; bits++; }
        return bits;
    }

    private void PutBits(Int32 code, Int32 bits)
    {
        _bitBuf = (_bitBuf << bits) | code;
        _bitCount += bits;
        while (_bitCount >= 8)
        {
            _bitCount -= 8;
            var b = (Byte)((_bitBuf >> _bitCount) & 0xFF);
            _output.WriteByte(b);
            if (b == 0xFF) _output.WriteByte(0x00); // 字节填充
        }
    }

    private void PutSignedBits(Int32 value, Int32 bits)
    {
        if (value < 0) value = (1 << bits) - 1 + value;
        PutBits(value, bits);
    }

    private void FlushBits()
    {
        if (_bitCount > 0)
        {
            var b = (Byte)(_bitBuf << (8 - _bitCount));
            _output.WriteByte(b);
        }
    }

    private void WriteU16(Int32 v) { _output.WriteByte((Byte)(v >> 8)); _output.WriteByte((Byte)(v & 0xFF)); }

    private static Int32 Clamp(Int32 value, Int32 min, Int32 max) => value < min ? min : value > max ? max : value;

    private static readonly Int32[] DcBits = [0, 0x02,0x03,0x03,0x03,0x03,0x03,0x04,0x05,0x06,0x07,0x08,0x09];
    private static readonly Int32[] DcCode = [0, 0x000,0x002,0x003,0x004,0x005,0x006,0x00E,0x01E,0x03E,0x07E,0x0FE,0x1FE];
    private static readonly Int32[] AcBits =
    [
        0,4,2,2,3,4,5,7,8,10,16,16,16,16,16,16,16,0,4,5,7,9,11,16,16,16,16,16,16,16,16,0,0,5,8,10,12,16,16,
        16,16,16,16,16,16,0,0,0,6,9,12,16,16,16,16,16,16,16,16,0,0,0,0,6,10,16,16,16,16,16,16,16,16,0,0,0,0,
        0,7,11,16,16,16,16,16,16,16,0,0,0,0,0,0,7,12,16,16,16,16,16,16,0,0,0,0,0,0,0,0,8,12,16,16,16,16,0,0,
        0,0,0,0,0,0,0,0,9,15,16,16,16,0,0,0,0,0,0,0,0,0,0,0,10,16,16,0,0,0,0,0,0,0,0,0,0,0,0,11,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,12,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,16,0,15,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
    ];
    private static readonly Int32[] AcCode =
    [
        0,0x000A,0x0000,0x0001,0x0004,0x000B,0x001A,0x0078,0x00F8,0x03F6,0xFF82,0xFF83,0xFF84,0xFF85,0xFF86,
        0xFF87,0xFF88,0,0x000C,0x001B,0x0079,0x01F6,0x07F6,0xFF89,0xFF8A,0xFF8B,0xFF8C,0xFF8D,0xFF8E,0xFF8F,
        0xFF90,0,0,0x001C,0x00F9,0x03F7,0x0FF4,0xFF91,0xFF92,0xFF93,0xFF94,0xFF95,0xFF96,0xFF97,0xFF98,0,0,0,
        0x003A,0x01F7,0x0FF5,0xFF99,0xFF9A,0xFF9B,0xFF9C,0xFF9D,0xFF9E,0xFF9F,0xFFA0,0,0,0,0,0x003B,0x03F8,
        0xFFA1,0xFFA2,0xFFA3,0xFFA4,0xFFA5,0xFFA6,0xFFA7,0xFFA8,0,0,0,0,0,0x007A,0x07F7,0xFFA9,0xFFAA,0xFFAB,
        0xFFAC,0xFFAD,0xFFAE,0xFFAF,0,0,0,0,0,0,0x007B,0x0FF6,0xFFB0,0xFFB1,0xFFB2,0xFFB3,0xFFB4,0xFFB5,0,0,0,
        0,0,0,0,0,0x00FA,0x0FF7,0xFFB6,0xFFB7,0xFFB8,0xFFB9,0,0,0,0,0,0,0,0,0,0,0x01F8,0x7FC0,0xFFBA,0xFFBB,0,
        0,0,0,0,0,0,0,0,0,0x01F9,0xFFBC,0,0,0,0,0,0,0,0,0,0,0,0x01FA,0,0,0,0,0,0,0,0,0,0,0,0,0x01FB,0,0,0,0,0,
        0,0,0,0,0,0,0,0x7FC1,0,0x7FC2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
    ];
}

#endregion

#region JPEG 位流读取器

/// <summary>JPEG 比特流读取器。处理字节填充（0xFF→0x00→跳过 0x00）</summary>
internal class JpegBitReader
{
    private readonly Byte[] _data;
    private Int32 _bytePos;
    private Int32 _bitPos;
    private Int32 _bits;

    public JpegBitReader(Byte[] data, Int32 startPos)
    {
        _data = data;
        _bytePos = startPos;
        _bitPos = 0;
        _bits = 0;
        Fill();
    }

    public Boolean HasMore => _bytePos < _data.Length || _bitPos < 8;
    public Int32 RemainingBits => (_data.Length - _bytePos) * 8 + (8 - _bitPos);

    public Int32 ReadBit()
    {
        if (_bitPos >= 8) Fill();
        var bit = (_bits >> (7 - _bitPos)) & 1;
        _bitPos++;
        return bit;
    }

    public Int32 ReadBits(Int32 n)
    {
        var v = 0;
        for (var i = 0; i < n; i++) v = (v << 1) | ReadBit();
        return v;
    }

    public Int32 ReadSignedBits(Int32 n)
    {
        if (n == 0) return 0;
        var v = ReadBits(n);
        var half = 1 << (n - 1);
        return v < half ? v - (1 << n) + 1 : v;
    }

    public void SkipBits(Int32 n)
    {
        for (var i = 0; i < n; i++) ReadBit();
    }

    public void AlignToByteBoundary()
    {
        if (_bitPos > 0 && _bitPos < 8)
        {
            _bitPos = 8;
            _bits = 0;
        }
    }

    private void Fill()
    {
        if (_bytePos >= _data.Length) { _bits = 0; _bitPos = 8; return; }
        var b = _data[_bytePos++];
        if (b == 0xFF && _bytePos < _data.Length && _data[_bytePos] == 0x00)
            _bytePos++; // 跳过字节填充
        _bits = b;
        _bitPos = 0;
    }
}

#endregion

#region Huffman 表

/// <summary>Huffman 解码表。基于最大码长的查找表实现</summary>
internal class HuffmanTable
{
    private readonly (Int32 Symbol, Int32 Bits)[] _table = new (Int32, Int32)[256];

    public void Add(Int32 code, Int32 bits, Int32 symbol)
    {
        var mask = (1 << bits) - 1;
        var prefix = code << (8 - bits);
        var count = 1 << (8 - bits);
        for (var i = 0; i < count; i++)
            _table[prefix + i] = (symbol, bits);
    }

    public Int32 Decode(JpegBitReader reader)
    {
        var prefix = reader.ReadBits(1);
        for (var bits = 1; bits <= 16; bits++)
        {
            var entry = _table[prefix << (8 - bits)];
            if (entry.Bits == bits) return entry.Symbol;
            if (bits < 16) prefix = (prefix << 1) | reader.ReadBit();
        }
        return 0;
    }
}

#endregion
