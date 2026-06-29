using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>MPEG-TS 容器写入器骨架。将视频编码数据封装为 TS 流</summary>
public class TsWriter : IVideoContainerWriter
{
    private readonly FileStream _stream;
    private Int32 _continuityCounter;
    private Int32 _videoPid = 0x100;
    private Int32 _width;
    private Int32 _height;
    private Boolean _headerWritten;

    public TsWriter(String filePath)
    {
        _stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    public void WriteMetadata(Int32 width, Int32 height, VideoTypes codecType, Double frameRate)
    {
        _width = width;
        _height = height;
        if (!_headerWritten) WritePatPmt();
    }

    public void WriteFrame(VPacket packet)
    {
        if (packet?.Data == null) return;
        if (!_headerWritten) WritePatPmt();

        var data = packet.Data.ReadBytes();
        var offset = 0;
        var payloadStart = true;

        while (offset < data.Length)
        {
            var tsPacket = new Byte[188];
            tsPacket[0] = 0x47; // sync byte
            tsPacket[1] = (Byte)(payloadStart ? 0x40 : 0x00);
            tsPacket[1] |= (Byte)((_videoPid >> 8) & 0x1F);
            tsPacket[2] = (Byte)(_videoPid & 0xFF);
            tsPacket[3] = (Byte)(0x10 | (_continuityCounter & 0x0F)); // payload only
            _continuityCounter = (_continuityCounter + 1) & 0x0F;

            var payloadLen = Math.Min(184, data.Length - offset);
            Array.Copy(data, offset, tsPacket, 4, payloadLen);

            if (payloadLen < 184)
                tsPacket[3] |= 0x20; // adaptation field present
            if (payloadLen < 184 && payloadLen < 183)
            {
                tsPacket[4] = (Byte)(183 - payloadLen); // adaptation field length
                tsPacket[4 + tsPacket[4] + 1] = 0xFF;
            }

            _stream.Write(tsPacket, 0, 188);
            offset += payloadLen;
            payloadStart = false;
        }
    }

    public void Flush() => _stream.Flush();

    private void WritePatPmt()
    {
        var videoPidHi = (Byte)(_videoPid >> 8);
        var videoPidLo = (Byte)(_videoPid & 0xFF);

        // PAT packet
        var pat = new Byte[188];
        pat[0] = 0x47; pat[1] = 0x40; pat[2] = 0x00; pat[3] = 0x10;
        pat[4] = 0x00; // pointer field
        pat[5] = 0x00; // table_id = PAT
        pat[6] = 0xB0; pat[7] = 13; // section length
        pat[8] = 0x00; pat[9] = 0x01; // transport stream id
        pat[10] = 0xC1; pat[11] = 0x00; pat[12] = 0x00; // version
        pat[13] = 0x00; pat[14] = 0x01; // section/last section
        pat[15] = 0x00; pat[16] = 0x01; // program number
        pat[17] = (Byte)(0xE0 | videoPidHi); pat[18] = videoPidLo; // PMT PID
        _stream.Write(pat, 0, 188);

        // PMT packet
        var pmt = new Byte[188];
        pmt[0] = 0x47; pmt[1] = 0x40; pmt[2] = videoPidHi; pmt[3] = (Byte)(videoPidLo | 0x10);
        pmt[4] = 0x00;
        pmt[5] = 0x02; // table_id = PMT
        pmt[6] = 0xB0; pmt[7] = 20; // section length
        pmt[8] = 0x00; pmt[9] = 0x01; // program number
        pmt[10] = 0xC1; pmt[11] = 0x00; pmt[12] = 0x00;
        pmt[13] = 0x00; pmt[14] = 0x00; // section/last section
        pmt[15] = (Byte)(0xE0 | videoPidHi); pmt[16] = videoPidLo; // PCR PID
        pmt[17] = 0xF0; pmt[18] = 0x00; // program info length = 0
        pmt[19] = 0x1B; // stream type = H.264
        pmt[20] = (Byte)(0xE0 | videoPidHi); pmt[21] = videoPidLo; // elementary PID
        pmt[22] = 0xF0; pmt[23] = 0x00; // ES info length = 0
        _stream.Write(pmt, 0, 188);

        _headerWritten = true;
    }

    public void Dispose() { if (_disposed) return; _disposed = true; Flush(); _stream?.Dispose(); }
    private Boolean _disposed;
}
