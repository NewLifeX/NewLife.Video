using NewLife.Buffers;
using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>MP4 (ISO BMFF) 容器写入器。将 H.264/H.265 编码数据封装为标准 MP4 文件</summary>
/// <remarks>
/// 生成立即可播放的 ftyp+moov+mdat 完整结构。
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
    private Int32 _count;
    private Boolean _disposed;

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
        _frames.Add(data);
        _frameSizes.Add((UInt32)data.Length);
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

        // stsd
        B(fs, "stsd"); U4(fs, 0); U4(fs, 1);
        U4(fs, 86);
        A(fs, _codecType == VideoTypes.H265 ? "hvc1" : "avc1");
        Z(fs, 6); U2(fs, 1); U2(fs, 0); U2(fs, 0); Z(fs, 4); U4(fs, 0);
        U2(fs, (UInt16)_width); U2(fs, (UInt16)_height);
        U4(fs, 0x00480000); U4(fs, 0x00480000); U4(fs, 0); U2(fs, 1);
        Z(fs, 32); U2(fs, 0x0018); U2(fs, 0xFFFF);
        E(fs);

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

        // stco
        B(fs, "stco"); U4(fs, 0); U4(fs, (UInt32)_frameSizes.Count);
        UInt32 off = 0;
        foreach (var s in _frameSizes) { U4(fs, off); off += s; }
        E(fs);

        // stsc
        B(fs, "stsc"); U4(fs, 0); U4(fs, 1); U4(fs, 1); U4(fs, 1); U4(fs, 1);
        E(fs);

        // stss (all frames as sync)
        B(fs, "stss"); U4(fs, 0); U4(fs, (UInt32)_frameSizes.Count);
        for (UInt32 i = 1; i <= _frameSizes.Count; i++) U4(fs, i);
        E(fs);

        E(fs);
    }

    private void WriteMdat(FileStream fs)
    {
        B(fs, "mdat");
        foreach (var f in _frames) fs.Write(f, 0, f.Length);
        E(fs);
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

    /// <summary>写入 n 个零字节到流</summary>
    private static void Z(FileStream fs, Int32 n)
    {
        var buf = new Byte[n];
        fs.Write(buf, 0, n);
    }

    #endregion
}
