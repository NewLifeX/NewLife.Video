using NewLife.Buffers;
using NewLife.Data;
using NewLife.Video.Containers;

namespace NewLife.Video.FMp4;

/// <summary>MP4 (ISO BMFF) 容器写入器。将 H.264/H.265 编码数据封装为标准 MP4 文件</summary>
/// <remarks>
/// 生成可播放的 ftyp+moov+mdat 完整结构，自动从首个关键帧提取 SPS/PPS 写入 avcC/hvcC 配置盒。
/// 帧数据暂存内存，Dispose 时一次性写入文件。
/// 高吞吐场景建议分片 MP4 (fMP4)。
/// </remarks>
public class Mp4Writer : IVideoContainerWriter
{
    private readonly String _filePath;
    private Int32 _width;
    private Int32 _height;
    private VideoTypes _codecType;
    private Double _frameRate;
    private Int32 _timeScale;

    private readonly List<Byte[]> _frames = [];
    private readonly List<UInt32> _frameSizes = [];
    private readonly List<UInt32> _frameDts = [];
    private readonly List<Int32> _keyFrameIndices = [];
    private Int32 _count;
    private Boolean _disposed;

    private Byte[] _sps;
    private Byte[] _pps;
    private Byte[] _vps;
    private Boolean _paramsExtracted;

    private readonly List<Int64> _stcoPositions = [];

    public Mp4Writer(String filePath) => _filePath = filePath;

    public void WriteMetadata(Int32 width, Int32 height, VideoTypes codecType, Double frameRate)
    {
        _width = width;
        _height = height;
        _codecType = codecType;
        _frameRate = frameRate;
        _timeScale = (Int32)(frameRate * 1000);
    }

    public void WriteFrame(VPacket packet)
    {
        if (packet?.Data == null) return;
        var data = packet.Data.ToArray();

        // 从首个关键帧提取参数集
        if (!_paramsExtracted && packet.IsKeyFrame)
        {
            ExtractParameterSets(data);
            _paramsExtracted = true;
        }

        // Annex-B 转长度前缀样本；参数集已完整进入 avcC/hvcC 时从样本中剔除
        var sample = BuildAvccSample(data);

        _frames.Add(sample);
        _frameSizes.Add((UInt32)sample.Length);

        if (packet.IsKeyFrame)
            _keyFrameIndices.Add(_count + 1); // MP4 stss 使用 1-based index

        var delta = _timeScale > 0 ? (UInt32)(_timeScale / _frameRate) : 3000u;
        _frameDts.Add((UInt32)(_count * delta));
        _count++;
    }

    public void Flush() { }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        WriteFile();
    }

    private void WriteFile()
    {
        using var fs = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        WriteFtyp(fs);
        WriteMoov(fs);
        WriteMdat(fs);
    }

    #region 参数集提取

    /// <summary>从关键帧数据中提取 H.264 SPS/PPS 或 H.265 VPS/SPS/PPS</summary>
    private void ExtractParameterSets(Byte[] data)
    {
        if (_codecType == VideoTypes.H265)
            ExtractHevcParameterSets(data);
        else
            ExtractAvcParameterSets(data);
    }

    private void ExtractAvcParameterSets(Byte[] data)
    {
        var i = 0;
        while (i < data.Length - 4)
        {
            var startCodeLen = 0;
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (data[i + 2] == 1)
                    startCodeLen = 3;
                else if (data[i + 2] == 0 && i + 3 < data.Length && data[i + 3] == 1)
                    startCodeLen = 4;
            }

            if (startCodeLen == 0) { i++; continue; }

            var nalStart = i + startCodeLen;
            if (nalStart >= data.Length) break;

            var nalType = data[nalStart] & 0x1F;

            var nalEnd = nalStart + 1;
            while (nalEnd < data.Length - 3)
            {
                if (data[nalEnd] == 0 && data[nalEnd + 1] == 0 &&
                    (data[nalEnd + 2] == 1 || (data[nalEnd + 2] == 0 && nalEnd + 3 < data.Length && data[nalEnd + 3] == 1)))
                    break;
                nalEnd++;
            }
            if (nalEnd > data.Length) nalEnd = data.Length;

            var nalData = new Byte[nalEnd - nalStart];
            Array.Copy(data, nalStart, nalData, 0, nalData.Length);

            if (nalType == 7) _sps = nalData;
            else if (nalType == 8) _pps = nalData;

            if (_sps != null && _pps != null) break;
            i = nalEnd;
        }
    }

    private void ExtractHevcParameterSets(Byte[] data)
    {
        var i = 0;
        while (i < data.Length - 4)
        {
            var startCodeLen = 0;
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (data[i + 2] == 1)
                    startCodeLen = 3;
                else if (data[i + 2] == 0 && i + 3 < data.Length && data[i + 3] == 1)
                    startCodeLen = 4;
            }

            if (startCodeLen == 0) { i++; continue; }

            var nalStart = i + startCodeLen;
            if (nalStart + 1 >= data.Length) break;

            var nalType = (data[nalStart] >> 1) & 0x3F;

            var nalEnd = nalStart + 1;
            while (nalEnd < data.Length - 3)
            {
                if (data[nalEnd] == 0 && data[nalEnd + 1] == 0 &&
                    (data[nalEnd + 2] == 1 || (data[nalEnd + 2] == 0 && nalEnd + 3 < data.Length && data[nalEnd + 3] == 1)))
                    break;
                nalEnd++;
            }
            if (nalEnd > data.Length) nalEnd = data.Length;

            var nalData = new Byte[nalEnd - nalStart];
            Array.Copy(data, nalStart, nalData, 0, nalData.Length);

            if (nalType == 32) _vps = nalData;
            else if (nalType == 33) _sps = nalData;
            else if (nalType == 34) _pps = nalData;

            if (_vps != null && _sps != null && _pps != null) break;
            i = nalEnd;
        }
    }

    /// <summary>参数集是否已完整写入配置盒（此时样本内参数集可安全剔除）</summary>
    private Boolean HasConfigParameterSets()
    {
        if (!_paramsExtracted) return false;
        if (_codecType == VideoTypes.H265) return _vps != null && _sps != null && _pps != null;
        return _sps != null && _pps != null;
    }

    /// <summary>将 Annex-B 数据转换为长度前缀样本（AVCC/HEVC 格式），参数集已入配置盒时从样本中剔除</summary>
    private Byte[] BuildAvccSample(Byte[] data)
    {
        var nals = SplitAnnexBNals(data);
        if (nals.Count == 0)
        {
            // 无起始码：按单个 NAL 处理
            var single = new Byte[4 + data.Length];
            WriteBe32(single, 0, data.Length);
            Array.Copy(data, 0, single, 4, data.Length);
            return single;
        }

        var strip = HasConfigParameterSets();

        var total = 0;
        foreach (var (start, len) in nals)
        {
            if (strip && IsParameterSet(data, start, len)) continue;
            total += 4 + len;
        }
        // 整帧仅含参数集时保留，避免空样本
        if (total == 0)
        {
            foreach (var (_, len) in nals)
                total += 4 + len;
            strip = false;
        }

        var sample = new Byte[total];
        var offset = 0;
        foreach (var (start, len) in nals)
        {
            if (strip && IsParameterSet(data, start, len)) continue;
            WriteBe32(sample, offset, len);
            Array.Copy(data, start, sample, offset + 4, len);
            offset += 4 + len;
        }

        return sample;
    }

    /// <summary>按 Annex-B 起始码切分 NAL 单元，返回各 NAL 在源数据中的偏移与长度</summary>
    private static List<(Int32 Start, Int32 Length)> SplitAnnexBNals(Byte[] data)
    {
        var nals = new List<(Int32, Int32)>();
        var n = data.Length;
        var i = 0;
        var lastStart = -1;

        while (i < n)
        {
            var scLen = 0;
            if (i + 4 <= n && data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 0 && data[i + 3] == 1) scLen = 4;
            else if (i + 3 <= n && data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 1) scLen = 3;

            if (scLen > 0)
            {
                if (lastStart >= 0 && i > lastStart) nals.Add((lastStart, i - lastStart));
                lastStart = i + scLen;
                i += scLen;
            }
            else
            {
                i++;
            }
        }

        if (lastStart >= 0 && lastStart < n) nals.Add((lastStart, n - lastStart));
        return nals;
    }

    /// <summary>判断 NAL 是否为参数集（H.264 SPS/PPS；H.265 VPS/SPS/PPS）</summary>
    private Boolean IsParameterSet(Byte[] data, Int32 start, Int32 length)
    {
        if (length <= 0) return false;
        var b = data[start];
        if (_codecType == VideoTypes.H265)
        {
            var t = (b >> 1) & 0x3F;
            return t is 32 or 33 or 34;
        }
        var nalType = b & 0x1F;
        return nalType is 7 or 8;
    }

    private static void WriteBe32(Byte[] buffer, Int32 offset, Int32 value)
    {
        buffer[offset] = (Byte)(value >> 24);
        buffer[offset + 1] = (Byte)(value >> 16);
        buffer[offset + 2] = (Byte)(value >> 8);
        buffer[offset + 3] = (Byte)value;
    }

    /// <summary>构建 AVCDecoderConfigurationRecord 或 HEVCDecoderConfigurationRecord</summary>
    private Byte[] BuildDecoderConfigurationRecord()
    {
        if (_codecType == VideoTypes.H265)
            return BuildHevcDecoderConfigurationRecord();
        return BuildAvcDecoderConfigurationRecord();
    }

    private Byte[] BuildAvcDecoderConfigurationRecord()
    {
        var sps = _sps ?? new Byte[] { 0x67, 0x42, 0x00, 0x1E, 0xAB, 0x40, 0x80 };
        var pps = _pps ?? new Byte[] { 0x68, 0xCE, 0x3C, 0x80 };

        using var ms = new MemoryStream();
        ms.WriteByte(1); // configurationVersion

        if (sps.Length > 3)
        {
            ms.WriteByte(sps[1]); // AVCProfileIndication
            ms.WriteByte(sps[2]); // profile_compatibility
            ms.WriteByte(sps[3]); // AVCLevelIndication
        }
        else
        {
            ms.WriteByte(0x42); ms.WriteByte(0x00); ms.WriteByte(0x1E);
        }

        ms.WriteByte(0xFF); // reserved(6) + lengthSizeMinusOne(2) = 3
        ms.WriteByte((Byte)(0xE0 | 1)); // reserved(3) + 1 SPS

        var spsLen = (UInt16)sps.Length;
        ms.WriteByte((Byte)(spsLen >> 8));
        ms.WriteByte((Byte)(spsLen & 0xFF));
        ms.Write(sps, 0, sps.Length);

        ms.WriteByte(1); // 1 PPS
        var ppsLen = (UInt16)pps.Length;
        ms.WriteByte((Byte)(ppsLen >> 8));
        ms.WriteByte((Byte)(ppsLen & 0xFF));
        ms.Write(pps, 0, pps.Length);

        return ms.ToArray();
    }

    private Byte[] BuildHevcDecoderConfigurationRecord()
    {
        var vps = _vps ?? new Byte[] { 0x40, 0x01, 0x0C, 0x01, 0xFF, 0xFF, 0x01, 0x60, 0x00, 0x00, 0x03, 0x00, 0xB0, 0x00, 0x00, 0x03, 0x00, 0x00, 0x03, 0x00, 0xB9 };
        var sps = _sps ?? new Byte[] { 0x42, 0x01, 0x01, 0x01, 0x60, 0x00, 0x00, 0x03, 0x00, 0xB0, 0x00, 0x00, 0x03, 0x00, 0x00, 0x03, 0x00, 0xB9, 0xA0 };
        var pps = _pps ?? new Byte[] { 0x44, 0x01, 0xC0, 0x72, 0xB0, 0x46, 0x40 };

        using var ms = new MemoryStream();
        ms.WriteByte(1); // configurationVersion

        var profileSpace = sps.Length > 1 ? (sps[1] >> 6) & 0x03 : 0;
        var tierFlag = sps.Length > 1 ? (sps[1] >> 5) & 0x01 : 0;
        var profileIdc = sps.Length > 1 ? sps[1] & 0x1F : 1;
        ms.WriteByte((Byte)((profileSpace << 6) | (tierFlag << 5) | profileIdc));

        ms.WriteByte(0x60); ms.WriteByte(0x00); ms.WriteByte(0x00); ms.WriteByte(0x00);
        for (var i = 0; i < 6; i++) ms.WriteByte(0x00);
        var levelIdc = sps.Length > 3 ? sps[3] : (Byte)0x7A;
        ms.WriteByte(levelIdc);
        ms.WriteByte(0xF0); ms.WriteByte(0x00); // min_spatial_segmentation
        ms.WriteByte(0xFC); // parallelismType
        ms.WriteByte(0xFC); // chromaFormatIdc
        ms.WriteByte(0xF8); // bitDepthLumaMinus8
        ms.WriteByte(0xF8); // bitDepthChromaMinus8
        ms.WriteByte(0x00); ms.WriteByte(0x00); // avgFrameRate
        ms.WriteByte(0x0F); // constantFrameRate + numTemporalLayers + temporalIdNested + lengthSizeMinusOne

        var arrayCount = (Byte)((_vps != null ? 1 : 0) + (_sps != null ? 1 : 0) + (_pps != null ? 1 : 0));
        ms.WriteByte(arrayCount);

        if (_vps != null)
        {
            ms.WriteByte(32); ms.WriteByte(0x00); ms.WriteByte(1);
            ms.WriteByte((Byte)(_vps.Length >> 8)); ms.WriteByte((Byte)(_vps.Length & 0xFF));
            ms.Write(_vps, 0, _vps.Length);
        }
        if (_sps != null)
        {
            ms.WriteByte(33); ms.WriteByte(0x00); ms.WriteByte(1);
            ms.WriteByte((Byte)(_sps.Length >> 8)); ms.WriteByte((Byte)(_sps.Length & 0xFF));
            ms.Write(_sps, 0, _sps.Length);
        }
        if (_pps != null)
        {
            ms.WriteByte(34); ms.WriteByte(0x00); ms.WriteByte(1);
            ms.WriteByte((Byte)(_pps.Length >> 8)); ms.WriteByte((Byte)(_pps.Length & 0xFF));
            ms.Write(_pps, 0, _pps.Length);
        }

        return ms.ToArray();
    }

    #endregion

    #region 盒

    private void WriteFtyp(FileStream fs)
    {
        B(fs, "ftyp");
        A(fs, "isom"); U4(fs, 512);
        A(fs, "isom"); A(fs, "iso2"); A(fs, "avc1"); A(fs, "mp41");
        E(fs);
    }

    private void WriteMoov(FileStream fs)
    {
        B(fs, "moov");
        WriteMvhd(fs);
        WriteTrak(fs);
        E(fs);
    }

    private void WriteMvhd(FileStream fs)
    {
        B(fs, "mvhd");
        U4(fs, 0); U4(fs, 0); U4(fs, 0);
        U4(fs, (UInt32)_timeScale);
        U4(fs, CalcDurMs());
        U4(fs, 0x00010000); // rate
        U2(fs, 0x0100);     // volume
        Z(fs, 10);
        Matrix(fs);
        Z(fs, 24);
        U4(fs, 2);          // next track id
        E(fs);
    }

    private void WriteTrak(FileStream fs)
    {
        B(fs, "trak");
        WriteTkhd(fs);
        WriteMdia(fs);
        E(fs);
    }

    private void WriteTkhd(FileStream fs)
    {
        B(fs, "tkhd");
        U4(fs, 0x00000007); U4(fs, 0); U4(fs, 0);
        U4(fs, 1); U4(fs, 0);
        U4(fs, CalcDurMs());
        Z(fs, 8); U2(fs, 0); U2(fs, 0); U2(fs, 0x0100); U2(fs, 0);
        Matrix(fs);
        U4(fs, (UInt32)(_width << 16));
        U4(fs, (UInt32)(_height << 16));
        E(fs);
    }

    private void WriteMdia(FileStream fs)
    {
        B(fs, "mdia");
        WriteMdhd(fs); WriteHdlr(fs); WriteMinf(fs);
        E(fs);
    }

    private void WriteMdhd(FileStream fs)
    {
        B(fs, "mdhd");
        U4(fs, 0); U4(fs, 0); U4(fs, 0);
        U4(fs, (UInt32)_timeScale);
        U4(fs, CalcDurTs());
        U2(fs, 0x55C4); U2(fs, 0);
        E(fs);
    }

    private void WriteHdlr(FileStream fs)
    {
        B(fs, "hdlr");
        U4(fs, 0); A(fs, "vide"); Z(fs, 12);
        A(fs, "VideoHandler"); fs.WriteByte(0);
        E(fs);
    }

    private void WriteMinf(FileStream fs)
    {
        B(fs, "minf");

        B(fs, "vmhd"); U4(fs, 1); U2(fs, 0); Z(fs, 6); E(fs);

        B(fs, "dinf");
        B(fs, "dref"); U4(fs, 0); U4(fs, 1);
        U4(fs, 12); A(fs, "url "); U4(fs, 1);
        E(fs); E(fs);

        WriteStbl(fs);
        E(fs);
    }

    private void WriteStbl(FileStream fs)
    {
        B(fs, "stbl");

        // stsd — 写入 VisualSampleEntry + avcC/hvcC 配置盒
        var configRecord = BuildDecoderConfigurationRecord();
        var configBoxSize = configRecord != null ? 8 + configRecord.Length : 0; // 4(size)+4(type)+data
        // H.265 仅当参数集完整进入 hvcC 时使用 hvc1；否则用 hev1（允许样本内带参数集）
        var codecFourCc = _codecType == VideoTypes.H265 ? (HasConfigParameterSets() ? "hvc1" : "hev1") : "avc1";
        var configBoxFourCc = _codecType == VideoTypes.H265 ? "hvcC" : "avcC";
        var entrySize = 86 + configBoxSize; // VisualSampleEntry(86) + config box

        B(fs, "stsd"); U4(fs, 0); U4(fs, 1);
        U4(fs, (UInt32)entrySize);
        A(fs, codecFourCc);
        Z(fs, 6); U2(fs, 1); U2(fs, 0); U2(fs, 0); Z(fs, 12);
        U2(fs, (UInt16)_width); U2(fs, (UInt16)_height);
        U4(fs, 0x00480000); U4(fs, 0x00480000); U4(fs, 0); U2(fs, 1);
        Z(fs, 32); U2(fs, 0x0018); U2(fs, 0xFFFF);

        // 写入 avcC / hvcC 配置盒
        if (configRecord != null)
        {
            U4(fs, (UInt32)(8 + configRecord.Length)); // box size
            A(fs, configBoxFourCc);
            fs.Write(configRecord, 0, configRecord.Length);
        }

        E(fs); // end stsd

        // stts
        var runs = BuildStts();
        if (runs.Count > 0)
        {
            B(fs, "stts"); U4(fs, 0); U4(fs, (UInt32)runs.Count);
            foreach (var (c, d) in runs) { U4(fs, c); U4(fs, d); }
            E(fs);
        }

        // stsz
        B(fs, "stsz"); U4(fs, 0); U4(fs, 0); U4(fs, (UInt32)_frameSizes.Count);
        foreach (var s in _frameSizes) U4(fs, s);
        E(fs);

        // stco — 先写占位偏移，mdat 写入后回填绝对文件偏移
        _stcoPositions.Clear();
        B(fs, "stco"); U4(fs, 0); U4(fs, (UInt32)_frameSizes.Count);
        foreach (var s in _frameSizes) { _stcoPositions.Add(fs.Position); U4(fs, 0); }
        E(fs);

        // stsc
        B(fs, "stsc"); U4(fs, 0); U4(fs, 1); U4(fs, 1); U4(fs, 1); U4(fs, 1);
        E(fs);

        // stss — 仅标记关键帧为同步采样
        B(fs, "stss"); U4(fs, 0); U4(fs, (UInt32)_keyFrameIndices.Count);
        foreach (var idx in _keyFrameIndices) U4(fs, (UInt32)idx);
        E(fs);

        E(fs);
    }

    private void WriteMdat(FileStream fs)
    {
        B(fs, "mdat");
        var dataStart = fs.Position; // 样本数据起点：stco 绝对偏移回填基准
        foreach (var f in _frames) fs.Write(f, 0, f.Length);
        E(fs);

        // 回填 stco：绝对偏移 = mdat 样本数据起点 + 各帧累计长度
        var offset = dataStart;
        for (var i = 0; i < _stcoPositions.Count; i++)
        {
            fs.Seek(_stcoPositions[i], SeekOrigin.Begin);
            U4(fs, (UInt32)offset);
            offset += _frameSizes[i];
        }
    }

    private List<(UInt32 C, UInt32 D)> BuildStts()
    {
        var runs = new List<(UInt32, UInt32)>();
        if (_frameDts.Count == 0) return runs;
        var def = _timeScale > 0 ? (UInt32)(_timeScale / _frameRate) : 3000u;
        UInt32 last = 0; UInt32 cnt = 0;
        for (var i = 0; i < _frameDts.Count; i++)
        {
            var d = i == 0 ? 0u : _frameDts[i] - _frameDts[i - 1];
            if (d == 0) d = def;
            if (i == 0 || d != last) { if (cnt > 0) runs.Add((cnt, last)); last = d; cnt = 1; }
            else cnt++;
        }
        if (cnt > 0) runs.Add((cnt, last));
        return runs;
    }

    private UInt32 CalcDurMs() => _count == 0 || _frameRate <= 0 ? 0u : (UInt32)(_count / _frameRate * 1000);
    private UInt32 CalcDurTs() => _count == 0 || _frameRate <= 0 ? 0u : (UInt32)(_count * _timeScale / _frameRate);
    private static void Matrix(FileStream fs)
    {
        U4(fs, 0x00010000); U4(fs, 0); U4(fs, 0);
        U4(fs, 0); U4(fs, 0x00010000); U4(fs, 0);
        U4(fs, 0); U4(fs, 0); U4(fs, 0x40000000);
    }

    #endregion

    #region 流工具

    private readonly Stack<Int64> _stack = new();

    private void B(FileStream fs, String t) { _stack.Push(fs.Position); U4(fs, 0); A(fs, t); }
    private void E(FileStream fs)
    {
        var end = fs.Position; var start = _stack.Pop(); var sz = (UInt32)(end - start);
        fs.Seek(start, SeekOrigin.Begin); U4(fs, sz); fs.Seek(end, SeekOrigin.Begin);
    }

    /// <summary>写入大端 UInt32 到流</summary>
    private static void U4(FileStream fs, UInt32 v)
    {
        var buf = new Byte[4];
        var w = new SpanWriter(buf) { IsLittleEndian = false };
        w.Write(v);
        fs.Write(buf, 0, 4);
    }

    /// <summary>写入大端 UInt16 到流</summary>
    private static void U2(FileStream fs, UInt16 v)
    {
        var buf = new Byte[2];
        var w = new SpanWriter(buf) { IsLittleEndian = false };
        w.Write(v);
        fs.Write(buf, 0, 2);
    }

    /// <summary>写入 ASCII 字符串到流</summary>
    private static void A(FileStream fs, String s)
    {
        var buf = System.Text.Encoding.ASCII.GetBytes(s);
        fs.Write(buf, 0, buf.Length);
    }

    private static readonly Byte[] _zeroBuf = new Byte[256];

    /// <summary>写入 n 个零字节到流</summary>
    private static void Z(FileStream fs, Int32 n)
    {
        var buf = _zeroBuf;
        var remaining = n;
        while (remaining > 0)
        {
            var chunk = Math.Min(remaining, buf.Length);
            fs.Write(buf, 0, chunk);
            remaining -= chunk;
        }
    }

    #endregion
}
