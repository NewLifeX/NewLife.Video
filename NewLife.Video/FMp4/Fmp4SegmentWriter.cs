using NewLife.Video.Containers;
using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;

/// <summary>fMP4/CMAF 分段写入器。基于 ISO BMFF Box 模型，生成 ftyp+moov+循环(moof+mdat)的分段 MP4 结构</summary>
/// <remarks>
/// 适用于直播录制、CMAF 兼容流和 DASH 分段场景。
/// 与传统的 Mp4Writer（缓冲全部帧后一次性写入）不同，Fmp4SegmentWriter 按关键帧边界
/// 自动分段，每段包含 moof（片段元数据）+ mdat（媒体数据）对，支持边录制边播放。
/// </remarks>
public class Fmp4SegmentWriter : IVideoContainerWriter
{
    private readonly Stream _stream;
    private readonly Int32 _segmentFrames;
    private readonly Boolean _leaveOpen;

    private Int32 _width;
    private Int32 _height;
    private VideoTypes _codecType;
    private Double _frameRate;
    private Int32 _sequenceNumber;
    private Int64 _baseMediaDecodeTime;

    private Boolean _headerWritten;
    private Boolean _disposed;

    private readonly List<VPacket> _segmentFramesList = [];
    private Int64 _basePts = -1;

    /// <summary>已写入的分段数</summary>
    public Int32 SegmentCount => _sequenceNumber;

    public Fmp4SegmentWriter(String filePath, Int32 segmentFrames = 30)
    {
        if (String.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
        _stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        _segmentFrames = segmentFrames;
    }

    public Fmp4SegmentWriter(Stream stream, Int32 segmentFrames = 30, Boolean leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _segmentFrames = segmentFrames;
        _leaveOpen = leaveOpen;
    }

    public void WriteMetadata(Int32 width, Int32 height, VideoTypes codecType, Double frameRate)
    {
        _width = width;
        _height = height;
        _codecType = codecType;
        _frameRate = frameRate;

        if (!_headerWritten)
        {
            WriteInitSegment();
            _headerWritten = true;
        }
    }

    public void WriteFrame(VPacket packet)
    {
        if (packet?.Data == null) return;
        if (!_headerWritten) throw new InvalidOperationException("必须先调用 WriteMetadata");

        if (_basePts < 0) _basePts = packet.PTS;

        _segmentFramesList.Add(packet);

        var triggerFlush = false;
        if (_segmentFramesList.Count >= _segmentFrames)
            triggerFlush = true;
        if (packet.IsKeyFrame && _segmentFramesList.Count >= _segmentFrames / 2)
            triggerFlush = true;

        if (triggerFlush)
            FlushSegment();
    }

    public void Flush()
    {
        if (_segmentFramesList.Count > 0)
            FlushSegment();
        _stream.Flush();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_segmentFramesList.Count > 0)
                FlushSegment();
            _stream.Flush();
        }
        finally
        {
            if (!_leaveOpen)
                _stream.Dispose();
        }
    }

    #region 初始化

    /// <summary>写入初始化段：ftyp + moov（含 mvex）</summary>
    private void WriteInitSegment()
    {
        var buffer = Fmp4ArrayPool.Rent(4096);
        try
        {
            var w = new Fmp4MessagePackWriter(buffer);
            var timescale = (UInt32)Math.Max(_frameRate * 1000, 1000);

            // === ftyp ===
            WriteBoxHeader(ref w, "ftyp", 24);
            w.WriteArray("iso5"u8);
            w.WriteInt32(0x200);     // minor version
            w.WriteInt32(0x6D7034);  // compatible: mp4
            w.WriteInt32(0x69736F35); // iso5
            w.WriteInt32(0x6D703431); // mp41
            // ftyp 无额外数据
            var ftypSize = w.GetCurrentPosition();
            // 回填 ftyp 大小
            // 简化：此处直接写固定大小 ftyp

            // === moov ===
            var moovStart = w.GetCurrentPosition();
            // 预留 moov size 位置，稍后回填
            w.Skip(4, out var moovSizePos);
            w.WriteArray("moov"u8);

            // mvhd
            WriteMvhd(ref w, timescale);

            // trak
            WriteTrak(ref w, timescale);

            // mvex
            WriteMvex(ref w, timescale);

            // 回填 moov size
            var moovSize = w.GetCurrentPosition() - moovStart;
            w.WriteInt32Return((Int32)moovSize, moovSizePos);

            _stream.Write(buffer, 0, w.GetCurrentPosition());
        }
        finally
        {
            Fmp4ArrayPool.Return(buffer);
        }
    }

    private void WriteMvhd(ref Fmp4MessagePackWriter w, UInt32 timescale)
    {
        var mvhdStart = w.GetCurrentPosition();
        w.Skip(4, out var sizePos);
        w.WriteArray("mvhd"u8);
        w.WriteInt32(0);        // version(1) + flags(3) = 0
        w.WriteInt32(0);        // creation time
        w.WriteInt32(0);        // modification time
        w.WriteInt32((Int32)timescale);
        w.WriteInt32(0);        // duration (live, 0)
        w.WriteInt32(0x00010000); // rate 1.0
        w.WriteInt16(0x0100);   // volume 1.0
        w.Skip(10, out _);     // reserved
        w.WriteInt32(0x00010000); // matrix[0]
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteInt32(0x00010000);
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteInt32(0x40000000); // matrix[8]
        w.Skip(24, out _);     // pre-defined
        w.WriteInt32(2);        // next track id
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - mvhdStart), sizePos);
    }

    private void WriteTrak(ref Fmp4MessagePackWriter w, UInt32 timescale)
    {
        var trakStart = w.GetCurrentPosition();
        w.Skip(4, out var sizePos);
        w.WriteArray("trak"u8);

        // tkhd
        var tkhdStart = w.GetCurrentPosition();
        w.Skip(4, out var tkhdSizePos);
        w.WriteArray("tkhd"u8);
        w.WriteInt32(0x00000003); // version=0, flags=enabled+inMovie
        w.WriteInt32(0);          // creation time
        w.WriteInt32(0);          // modification time
        w.WriteInt32(1);          // track id
        w.WriteInt32(0);          // reserved
        w.WriteInt32(0);          // duration
        w.WriteInt32(0);          // reserved
        w.WriteInt32(0);          // layer
        w.WriteInt16(0);          // alternate group
        w.WriteInt16(0x0100);     // volume
        w.WriteInt32(0x00010000); // matrix[0]
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteInt32(0x00010000);
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteInt32(0x40000000);
        w.WriteInt32((Int32)((UInt32)_width << 16)); // width
        w.WriteInt32((Int32)((UInt32)_height << 16)); // height
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - tkhdStart), tkhdSizePos);

        // mdia
        var mdiaStart = w.GetCurrentPosition();
        w.Skip(4, out var mdiaSizePos);
        w.WriteArray("mdia"u8);

        // mdhd
        var mdhdStart = w.GetCurrentPosition();
        w.Skip(4, out var mdhdSizePos);
        w.WriteArray("mdhd"u8);
        w.WriteInt32(0);        // version=0, flags=0
        w.WriteInt32(0);        // creation time
        w.WriteInt32(0);        // modification time
        w.WriteInt32(1000);     // timescale (ms)
        w.WriteInt32(0);        // duration
        w.WriteInt16(0x15C7);   // language: und
        w.WriteInt16(0);        // quality
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - mdhdStart), mdhdSizePos);

        // hdlr
        var hdlrStart = w.GetCurrentPosition();
        w.Skip(4, out var hdlrSizePos);
        w.WriteArray("hdlr"u8);
        w.WriteInt32(0);        // version=0, flags=0
        w.WriteInt32(0);        // pre-defined
        w.WriteInt32(0x76696465); // handler_type: vide
        w.WriteInt32(0);        // reserved
        w.WriteInt32(0);        // reserved
        w.WriteInt32(0);        // reserved
        w.WriteArray("VideoHandler"u8);
        w.WriteByte(0);         // null terminator
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - hdlrStart), hdlrSizePos);

        // minf
        var minfStart = w.GetCurrentPosition();
        w.Skip(4, out var minfSizePos);
        w.WriteArray("minf"u8);

        // vmhd
        var vmhdStart = w.GetCurrentPosition();
        w.Skip(4, out var vmhdSizePos);
        w.WriteArray("vmhd"u8);
        w.WriteInt32(0x00000001); // version=0, flags=1
        w.WriteInt16(0);          // graphics mode
        w.WriteInt16(0);          // opcolor
        w.WriteInt16(0);
        w.WriteInt16(0);
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - vmhdStart), vmhdSizePos);

        // dinf
        var dinfStart = w.GetCurrentPosition();
        w.Skip(4, out var dinfSizePos);
        w.WriteArray("dinf"u8);
        // dref
        var drefStart = w.GetCurrentPosition();
        w.Skip(4, out var drefSizePos);
        w.WriteArray("dref"u8);
        w.WriteInt32(0);          // version=0, flags=0
        w.WriteInt32(1);          // entry count
        // url
        var urlStart = w.GetCurrentPosition();
        w.Skip(4, out var urlSizePos);
        w.WriteArray("url "u8);
        w.WriteInt32(0x00000001); // version=0, flags=1 (self-contained)
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - urlStart), urlSizePos);
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - drefStart), drefSizePos);
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - dinfStart), dinfSizePos);

        // stbl
        WriteStbl(ref w);

        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - minfStart), minfSizePos);
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - mdiaStart), mdiaSizePos);
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - trakStart), sizePos);
    }

    private void WriteStbl(ref Fmp4MessagePackWriter w)
    {
        var stblStart = w.GetCurrentPosition();
        w.Skip(4, out var sizePos);
        w.WriteArray("stbl"u8);

        // stsd
        var stsdStart = w.GetCurrentPosition();
        w.Skip(4, out var stsdSizePos);
        w.WriteArray("stsd"u8);
        w.WriteInt32(0);          // version=0, flags=0
        w.WriteInt32(1);          // entry count

        var isHevc = _codecType == VideoTypes.H265;
        var codecName = isHevc ? "hvc1" : "avc1";
        var entryStart = w.GetCurrentPosition();
        w.Skip(4, out var entrySizePos);
        w.WriteASCII(codecName);

        w.Skip(6, out _);         // reserved
        w.WriteInt16(1);          // data reference index

        // VisualSampleEntry fields
        w.WriteInt16(0);          // pre-defined
        w.WriteInt16(0);          // reserved
        w.Skip(12, out _);        // pre-defined
        w.WriteInt16((Int16)_width);
        w.WriteInt16((Int16)_height);
        w.WriteInt32(0x00480000); // horiz resolution (72dpi)
        w.WriteInt32(0x00480000); // vert resolution
        w.WriteInt32(0);          // data size (0 = variable)
        w.WriteInt16(1);          // frame count
        var nameBytes = System.Text.Encoding.ASCII.GetBytes("VideoHandler");
        Array.Resize(ref nameBytes, 32);
        w.WriteArray(nameBytes);
        w.WriteInt16(0x0018);     // depth
        w.WriteInt16(-1);         // pre-defined

        // avcC/hvcC config record placeholder (empty)
        if (isHevc)
        {
            var hvcCStart = w.GetCurrentPosition();
            w.Skip(4, out var hvcCSizePos);
            w.WriteArray("hvcC"u8);
            w.WriteByte(1); // configuration version
            w.Skip(13, out _);
            w.WriteInt32Return((Int32)(w.GetCurrentPosition() - hvcCStart), hvcCSizePos);
        }
        else
        {
            var avcCStart = w.GetCurrentPosition();
            w.Skip(4, out var avcCSizePos);
            w.WriteArray("avcC"u8);
            w.WriteByte(1); // configuration version
            w.WriteByte(0x42); // profile (baseline)
            w.WriteByte(0);  // profile compat
            w.WriteByte(0x1E); // level 3.0
            w.WriteByte(0xFF); // reserved(6) + lengthSizeMinusOne(2) = 0xFF (4 byte length)
            w.WriteByte(0xE1); // reserved(3) + numSPS(5) = 1
            w.WriteInt16(0);  // SPS length (placeholder)
            w.WriteInt32Return((Int32)(w.GetCurrentPosition() - avcCStart), avcCSizePos);
        }
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - entryStart), entrySizePos);
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - stsdStart), stsdSizePos);

        // stts (0 entries)
        var sttsStart = w.GetCurrentPosition();
        w.Skip(4, out var sttsSizePos);
        w.WriteArray("stts"u8);
        w.WriteInt32(0); // version=0, flags=0
        w.WriteInt32(0); // entry count
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - sttsStart), sttsSizePos);

        // stsc (0 entries)
        var stscStart = w.GetCurrentPosition();
        w.Skip(4, out var stscSizePos);
        w.WriteArray("stsc"u8);
        w.WriteInt32(0); // version=0, flags=0
        w.WriteInt32(0); // entry count
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - stscStart), stscSizePos);

        // stsz (0 sizes)
        var stszStart = w.GetCurrentPosition();
        w.Skip(4, out var stszSizePos);
        w.WriteArray("stsz"u8);
        w.WriteInt32(0); // version=0, flags=0
        w.WriteInt32(0); // sample size (0 = variable)
        w.WriteInt32(0); // sample count
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - stszStart), stszSizePos);

        // stco (0 entries)
        var stcoStart = w.GetCurrentPosition();
        w.Skip(4, out var stcoSizePos);
        w.WriteArray("stco"u8);
        w.WriteInt32(0); // version=0, flags=0
        w.WriteInt32(0); // entry count
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - stcoStart), stcoSizePos);

        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - stblStart), sizePos);
    }

    private void WriteMvex(ref Fmp4MessagePackWriter w, UInt32 timescale)
    {
        var mvexStart = w.GetCurrentPosition();
        w.Skip(4, out var sizePos);
        w.WriteArray("mvex"u8);

        // mehd
        var mehdStart = w.GetCurrentPosition();
        w.Skip(4, out var mehdSizePos);
        w.WriteArray("mehd"u8);
        w.WriteInt32(0); // version=0, flags=0
        w.WriteInt32(0); // fragment duration
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - mehdStart), mehdSizePos);

        // trex
        var trexStart = w.GetCurrentPosition();
        w.Skip(4, out var trexSizePos);
        w.WriteArray("trex"u8);
        w.WriteInt32(0);  // version=0, flags=0
        w.WriteInt32(1);  // track id
        w.WriteInt32(1);  // default sample description index
        w.WriteInt32(0);  // default sample duration
        w.WriteInt32(0);  // default sample size
        w.WriteInt32(0x00010000); // default sample flags (non-keyframe)
        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - trexStart), trexSizePos);

        w.WriteInt32Return((Int32)(w.GetCurrentPosition() - mvexStart), sizePos);
    }

    #endregion

    #region 分段写入

    /// <summary>刷新当前分段：写入 moof + mdat</summary>
    private void FlushSegment()
    {
        if (_segmentFramesList.Count == 0) return;
        _sequenceNumber++;

        // 收集 NAL 数据并计算样本参数
        var sampleDataList = new List<Byte[]>();
        var durations = new List<UInt32>();
        var keyFrameFlags = new List<Boolean>();

        foreach (var frame in _segmentFramesList)
        {
            var rawData = frame.Data.TryGetArray(out var seg2) && seg2.Offset == 0
                ? seg2.Array : ToByteArray(frame.Data);

            var nalData = ConvertToLengthPrefixed(rawData);
            if (nalData != null && nalData.Length > 0)
            {
                sampleDataList.Add(nalData);
                var dur = (UInt32)(_frameRate > 0 ? 1000.0 / _frameRate : 33);
                if (frame.PTS > _basePts)
                    dur = (UInt32)Math.Max(1, frame.PTS - _basePts);
                durations.Add(dur);
                keyFrameFlags.Add(frame.IsKeyFrame);
            }
        }

        if (sampleDataList.Count == 0) { _segmentFramesList.Clear(); return; }

        var totalDataSize = 0;
        for (var i = 0; i < sampleDataList.Count; i++) totalDataSize += sampleDataList[i].Length;
        var bufferSize = Math.Max(8192, totalDataSize + 2048);
        var buffer = Fmp4ArrayPool.Rent(bufferSize);
        try
        {
            var w = new Fmp4MessagePackWriter(buffer);

            // moof
            var moofStart = w.GetCurrentPosition();
            w.Skip(4, out var moofSizePos);
            w.WriteArray("moof"u8);

            // mfhd
            var mfhdStart = w.GetCurrentPosition();
            w.Skip(4, out var mfhdSizePos);
            w.WriteArray("mfhd"u8);
            w.WriteInt32(0);                 // version=0, flags=0
            w.WriteInt32((Int32)_sequenceNumber); // sequence number
            w.WriteInt32Return((Int32)(w.GetCurrentPosition() - mfhdStart), mfhdSizePos);

            // traf
            var trafStart = w.GetCurrentPosition();
            w.Skip(4, out var trafSizePos);
            w.WriteArray("traf"u8);

            // tfhd
            var tfhdStart = w.GetCurrentPosition();
            w.Skip(4, out var tfhdSizePos);
            w.WriteArray("tfhd"u8);
            w.WriteInt32(0x02000000); // version=0, flags=default-base-is-moof
            w.WriteInt32(1);          // track id
            w.WriteInt32Return((Int32)(w.GetCurrentPosition() - tfhdStart), tfhdSizePos);

            // tfdt
            var tfdtStart = w.GetCurrentPosition();
            w.Skip(4, out var tfdtSizePos);
            w.WriteArray("tfdt"u8);
            w.WriteInt32(0);                 // version=0, flags=0
            w.WriteInt32((Int32)_baseMediaDecodeTime); // base media decode time
            w.WriteInt32Return((Int32)(w.GetCurrentPosition() - tfdtStart), tfdtSizePos);

            // trun (flags: data-offset | first-sample-flags | sample-duration | sample-size | sample-flags | sample-cts)
            var trunStart = w.GetCurrentPosition();
            w.Skip(4, out var trunSizePos);
            w.WriteArray("trun"u8);
            w.WriteInt32(0x000f01);  // version=0, flags=0x000f01
            w.WriteInt32(sampleDataList.Count); // sample count

            // data offset (占位，mdat 写入后回填)
            var dataOffsetPos = w.GetCurrentPosition();
            w.WriteInt32(0);

            // first sample flags
            w.WriteInt32(keyFrameFlags.Count > 0 && keyFrameFlags[0] ? 0x02000000 : 0x01010000);

            // sample details
            for (var i = 0; i < sampleDataList.Count; i++)
            {
                w.WriteInt32((Int32)durations[i]); // sample duration
                w.WriteInt32(sampleDataList[i].Length); // sample size
                w.WriteInt32(keyFrameFlags[i] ? 0x02000000 : 0x01010000); // sample flags
                w.WriteInt32(0); // composition time offset
            }

            // 回填 trun size
            w.WriteInt32Return((Int32)(w.GetCurrentPosition() - trunStart), trunSizePos);
            // 回填 traf size
            w.WriteInt32Return((Int32)(w.GetCurrentPosition() - trafStart), trafSizePos);
            // 回填 moof size
            w.WriteInt32Return((Int32)(w.GetCurrentPosition() - moofStart), moofSizePos);

            // mdat
            var mdatStart = w.GetCurrentPosition();
            w.Skip(4, out var mdatSizePos);
            w.WriteArray("mdat"u8);

            foreach (var data in sampleDataList)
            {
                w.WriteArray(data);
            }

            // 回填 mdat size
            w.WriteInt32Return((Int32)(w.GetCurrentPosition() - mdatStart), mdatSizePos);

            // 回填 trun data offset = moof size
            var moofTotalSize = w.GetCurrentPosition() - moofStart;
            // data offset = moof box size (从 moof 开始到 mdat 开始前)
            w.WriteInt32Return(moofTotalSize, dataOffsetPos);

            // 写入文件
            _stream.Write(buffer, 0, w.GetCurrentPosition());
        }
        finally
        {
            Fmp4ArrayPool.Return(buffer);
        }

        // 更新状态
        for (var i = 0; i < durations.Count; i++) _baseMediaDecodeTime += durations[i];
        _segmentFramesList.Clear();
    }

    #endregion

    #region 辅助

    /// <summary>将 NAL 起始码格式转换为 4 字节长度前缀格式</summary>
    private static Byte[] ConvertToLengthPrefixed(Byte[] rawData)
    {
        var nalUnits = new List<Byte[]>();
        var i = 0;

        while (i < rawData.Length - 3)
        {
            var startLen = 0;
            if (rawData[i] == 0 && rawData[i + 1] == 0)
            {
                if (rawData[i + 2] == 1) startLen = 3;
                else if (rawData[i + 2] == 0 && i + 3 < rawData.Length && rawData[i + 3] == 1) startLen = 4;
            }

            if (startLen > 0)
            {
                var nalStart = i + startLen;
                var nalEnd = rawData.Length;
                for (var j = nalStart; j < rawData.Length - 3; j++)
                {
                    if (rawData[j] == 0 && rawData[j + 1] == 0 && (rawData[j + 2] == 1 || (rawData[j + 2] == 0 && j + 3 < rawData.Length && rawData[j + 3] == 1)))
                    { nalEnd = j; break; }
                }
                var nalData = new Byte[nalEnd - nalStart];
                Array.Copy(rawData, nalStart, nalData, 0, nalData.Length);
                nalUnits.Add(nalData);
                i = nalEnd;
            }
            else i++;
        }

        if (nalUnits.Count == 0) return null;

        var totalSize = 0;
        for (var j = 0; j < nalUnits.Count; j++) totalSize += 4 + nalUnits[j].Length;
        var result = new Byte[totalSize];
        var offset = 0;
        foreach (var nal in nalUnits)
        {
            result[offset++] = (Byte)(nal.Length >> 24);
            result[offset++] = (Byte)(nal.Length >> 16);
            result[offset++] = (Byte)(nal.Length >> 8);
            result[offset++] = (Byte)nal.Length;
            Array.Copy(nal, 0, result, offset, nal.Length);
            offset += nal.Length;
        }
        return result;
    }

    /// <summary>写入 Box 头部（固定大小，稍后需回填实际大小）</summary>
    private static void WriteBoxHeader(ref Fmp4MessagePackWriter w, String boxType, Int32 fixedSize)
    {
        w.WriteInt32(fixedSize);
        w.WriteArray(System.Text.Encoding.ASCII.GetBytes(boxType));
    }

    /// <summary>从 IPacket 获取字节数组</summary>
    private static Byte[] ToByteArray(NewLife.Data.IPacket packet)
    {
        if (packet.TryGetArray(out var seg))
        {
            var data = new Byte[seg.Count];
            Array.Copy(seg.Array, seg.Offset, data, 0, seg.Count);
            return data;
        }
        return new Byte[0];
    }

    #endregion
}
