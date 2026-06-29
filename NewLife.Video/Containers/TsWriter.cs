using NewLife.Data;

namespace NewLife.Video.Containers;

/// <summary>MPEG-TS 容器写入器。将 H.264/H.265 视频编码数据封装为标准 TS 流</summary>
/// <remarks>
/// 生成标准 188 字节 TS 包，包含 PAT/PMT 节目表、PES 打包、PCR 时钟、自适应域填充。
/// 支持 H.264 (stream_type 0x1B) 和 H.265 (stream_type 0x24)。
/// 每帧封装为一个 PES 包，PTS 基于帧率和帧序号计算。
/// </remarks>
public class TsWriter : IVideoContainerWriter
{
    private readonly FileStream _stream;
    private Int32 _continuityCounter;
    private Int32 _videoPid = 0x100;
    private Int32 _width;
    private Int32 _height;
    private VideoTypes _codecType;
    private Double _frameRate;
    private Int32 _frameCount;
    private Boolean _headerWritten;
    private Int64 _lastPcrPts;

    /// <summary>PCR 发送间隔（TS 时钟 90kHz 单位，约 100ms）</summary>
    private const Int64 PcrInterval = 9000; // 90kHz * 0.1s

    public TsWriter(String filePath)
    {
        _stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    public void WriteMetadata(Int32 width, Int32 height, VideoTypes codecType, Double frameRate)
    {
        _width = width;
        _height = height;
        _codecType = codecType;
        _frameRate = frameRate > 0 ? frameRate : 25.0;
        if (!_headerWritten) WritePatPmt();
    }

    public void WriteFrame(VPacket packet)
    {
        if (packet?.Data == null) return;
        if (!_headerWritten) WritePatPmt();

        var data = packet.Data.ToArray();

        // 计算 PTS（90kHz 时钟单位）
        var pts = (Int64)(_frameCount * 90000.0 / _frameRate);

        // 构建 PES 包头
        var pesHeader = BuildPesHeader(data.Length, pts);
        var fullData = new Byte[pesHeader.Length + data.Length];
        Array.Copy(pesHeader, 0, fullData, 0, pesHeader.Length);
        Array.Copy(data, 0, fullData, pesHeader.Length, data.Length);

        // 将 PES 包分片写入 TS 包
        var offset = 0;
        var payloadStart = true;

        while (offset < fullData.Length)
        {
            var tsPacket = new Byte[188];
            tsPacket[0] = 0x47; // sync byte

            // PID
            tsPacket[1] = (Byte)(payloadStart ? 0x40 : 0x00); // PUSI
            tsPacket[1] |= (Byte)((_videoPid >> 8) & 0x1F);
            tsPacket[2] = (Byte)(_videoPid & 0xFF);

            // 自适应域控制 + 连续性计数器
            var needPcr = pts - _lastPcrPts >= PcrInterval;
            var remainingSpace = 184; // TS 包有效载荷区
            var hasAdaptation = false;

            if (needPcr)
            {
                hasAdaptation = true;
                remainingSpace -= 8; // PCR 占 8 字节（含自适应域长度和标志）
                _lastPcrPts = pts;
            }

            // 如果需要自适应域填充（最后一个分片不足 184 字节）
            var payloadLen = Math.Min(remainingSpace, fullData.Length - offset);
            if (payloadLen < remainingSpace)
                hasAdaptation = true;

            // AFC: 01=仅有效载荷, 10=仅自适应域, 11=两者都有
            tsPacket[3] = (Byte)((_continuityCounter & 0x0F) | (hasAdaptation ? 0x30 : 0x10));
            _continuityCounter = (_continuityCounter + 1) & 0x0F;

            var tsOffset = 4;
            var adaptationLenPos = tsOffset;

            if (hasAdaptation)
            {
                var adaptationLen = 0;

                if (needPcr)
                {
                    adaptationLen = 7; // 1(flags) + 6(PCR)
                    tsPacket[tsOffset++] = (Byte)adaptationLen;
                    tsPacket[tsOffset++] = 0x10; // PCR_flag
                    // PCR = PTS (简化，PCR ≈ PTS)
                    var pcrBase = pts; // 90kHz 单位
                    var pcrExt = 0u;
                    tsPacket[tsOffset++] = (Byte)(pcrBase >> 25);
                    tsPacket[tsOffset++] = (Byte)(pcrBase >> 17);
                    tsPacket[tsOffset++] = (Byte)(pcrBase >> 9);
                    tsPacket[tsOffset++] = (Byte)(pcrBase >> 1);
                    tsPacket[tsOffset++] = (Byte)(((pcrBase & 1) << 7) | 0x7E | (Byte)(pcrExt >> 8));
                    tsPacket[tsOffset++] = (Byte)(pcrExt & 0xFF);
                }

                // 自适应域填充（stuffing）
                if (payloadLen < remainingSpace)
                {
                    var stuffLen = remainingSpace - payloadLen - (adaptationLen > 0 ? 7 : 0);
                    if (stuffLen > 0)
                    {
                        if (adaptationLen == 0)
                        {
                            tsPacket[adaptationLenPos] = (Byte)(stuffLen - 1);
                            tsOffset = adaptationLenPos + 1;
                        }
                        // 填充 0xFF
                        for (var i = 0; i < stuffLen && tsOffset < 188; i++)
                            tsPacket[tsOffset++] = 0xFF;
                    }
                }
                else if (!needPcr)
                {
                    // 仅填充标志无实际自适应域内容
                    tsPacket[adaptationLenPos] = 0;
                    tsOffset = adaptationLenPos + 1;
                }
            }

            // 有效载荷
            Array.Copy(fullData, offset, tsPacket, tsOffset, payloadLen);
            _stream.Write(tsPacket, 0, 188);

            offset += payloadLen;
            payloadStart = false;
        }

        _frameCount++;
    }

    public void Flush() => _stream.Flush();

    private void WritePatPmt()
    {
        var videoPidHi = (Byte)(_videoPid >> 8);
        var videoPidLo = (Byte)(_videoPid & 0xFF);

        // 根据编码类型确定 stream_type：H.264=0x1B, H.265=0x24
        var streamType = _codecType == VideoTypes.H265 ? (Byte)0x24 : (Byte)0x1B;

        // PAT packet
        var pat = BuildTsPacket(0, 0, BuildPatSection());
        _stream.Write(pat, 0, 188);

        // PMT packet（PCR PID 与视频 PID 相同）
        var pmt = BuildTsPacket(_videoPid, 0, BuildPmtSection(streamType, videoPidHi, videoPidLo));
        _stream.Write(pmt, 0, 188);

        _headerWritten = true;
    }

    private static Byte[] BuildTsPacket(Int32 pid, Int32 continuityCounter, Byte[] section)
    {
        var packet = new Byte[188];
        packet[0] = 0x47;
        packet[1] = (Byte)(0x40 | ((pid >> 8) & 0x1F));
        packet[2] = (Byte)(pid & 0xFF);
        packet[3] = (Byte)(0x10 | (continuityCounter & 0x0F));
        packet[4] = 0x00; // pointer_field

        var copyLen = Math.Min(section.Length, 183);
        Array.Copy(section, 0, packet, 5, copyLen);

        // 填充剩余空间
        for (var i = 5 + copyLen; i < 188; i++)
            packet[i] = 0xFF;

        return packet;
    }

    private static Byte[] BuildPatSection()
    {
        // PAT section
        var section = new Byte[20];
        section[0] = 0x00; // table_id = PAT
        section[1] = 0xB0; // section_syntax_indicator + reserved
        section[2] = 13;   // section_length hi
        section[3] = 0x00; // transport_stream_id hi
        section[4] = 0x01; // transport_stream_id lo
        section[5] = 0xC1; // version + current_next_indicator
        section[6] = 0x00; // section_number
        section[7] = 0x00; // last_section_number
        section[8] = 0x00; // program_number hi
        section[9] = 0x01; // program_number lo
        section[10] = 0xE1; // program_map_PID hi (0x100 >> 8 = 0x01, with reserved bits: 0xE0 | 0x01)
        section[11] = 0x00; // program_map_PID lo (0x100 & 0xFF)
        // CRC (placeholder)
        section[12] = 0; section[13] = 0; section[14] = 0; section[15] = 0;
        return section;
    }

    private static Byte[] BuildPmtSection(Byte streamType, Byte videoPidHi, Byte videoPidLo)
    {
        var section = new Byte[24];
        section[0] = 0x02; // table_id = PMT
        section[1] = 0xB0;
        section[2] = 18;   // section_length hi (12+5+ES_info)
        section[3] = 0x00; // program_number hi
        section[4] = 0x01; // program_number lo
        section[5] = 0xC1; // version
        section[6] = 0x00; // section_number
        section[7] = 0x00; // last_section_number
        section[8] = (Byte)(0xE0 | videoPidHi);  // PCR_PID
        section[9] = videoPidLo;
        section[10] = 0xF0; // program_info_length hi
        section[11] = 0x00; // program_info_length lo
        section[12] = streamType;              // stream_type
        section[13] = (Byte)(0xE0 | videoPidHi); // elementary_PID
        section[14] = videoPidLo;
        section[15] = 0xF0; // ES_info_length hi
        section[16] = 0x00; // ES_info_length lo
        // CRC placeholder
        section[17] = 0; section[18] = 0; section[19] = 0; section[20] = 0;
        return section;
    }

    /// <summary>构建 PES 包头（含 PTS）</summary>
    private static Byte[] BuildPesHeader(Int32 dataLen, Int64 pts)
    {
        // PES start_code(3) + stream_id(1) + PES_packet_length(2) + flags(3) + PTS(5) = 14
        var header = new Byte[14];
        header[0] = 0x00;
        header[1] = 0x00;
        header[2] = 0x01; // packet_start_code_prefix
        header[3] = 0xE0; // stream_id = video
        // PES_packet_length: 0 表示未指定长度（视频流常用）
        header[4] = 0x00;
        header[5] = 0x00;
        // PES flags: '10' + PES_scrambling_control(00) + PES_priority(0) + data_alignment_indicator(0) + copyright(0) + original_or_copy(0)
        // + PTS_DTS_flags(10=PTS only) + ESCR(0) + ES_rate(0) + DSM_trick_mode(0) + additional_copy_info(0) + CRC(0) + extension(0)
        header[6] = 0x80; // '10' + all zeros + '10' (PTS only) + rest zeros
        header[7] = 0x80; // '10' then zeros
        header[8] = 0x05; // PES_header_data_length = 5 (PTS only)

        // PTS: 33 bits, format = 0011LLL LLLLLLLL LLLLLLLL LLLLLLLL LLLLLLLL (L = PTS bits)
        //       marker bits: 4(0011) + byte[0] + marker(1) + byte[1] + marker(1) + ...
        header[9] = (Byte)(0x21 | ((pts >> 29) & 0x0E) | ((pts >> 28) & 0x01));
        header[10] = (Byte)((pts >> 20) & 0xFF);
        header[11] = (Byte)(0x01 | ((pts >> 13) & 0xFE));
        header[12] = (Byte)((pts >> 5) & 0xFF);
        header[13] = (Byte)(0x01 | ((pts << 1) & 0xFE));

        return header;
    }

    public void Dispose() { if (_disposed) return; _disposed = true; Flush(); _stream?.Dispose(); }
    private Boolean _disposed;
}
