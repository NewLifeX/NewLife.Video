using System.Buffers.Binary;

namespace NewLife.Video.Codecs;

/// <summary>Exp-Golomb 指数哥伦布编码读取器。用于 H.264 SPS 参数集解析</summary>
/// <remarks>
/// ref struct，零分配设计，基于 ReadOnlySpan&lt;Byte&gt; 输入。
/// 支持 H.264 SPS 完整解析（profile/level/width/height）。
/// </remarks>
public ref struct ExpGolombReader
{
    /// <summary>源数据缓冲区</summary>
    public ReadOnlySpan<Byte> SrcBuffer { get; }

    /// <summary>剩余可用字节数</summary>
    public Int32 BytesAvailable { get; private set; }

    /// <summary>当前工作字</summary>
    public Int32 Word { get; private set; }

    /// <summary>剩余可用位数</summary>
    public Int32 BitsAvailable { get; private set; }

    /// <summary>实例化 Exp-Golomb 读取器</summary>
    /// <param name="srcBuffer">H.264 SPS NAL 单元数据（不含起始码）</param>
    public ExpGolombReader(ReadOnlySpan<Byte> srcBuffer)
    {
        SrcBuffer = srcBuffer;
        BytesAvailable = srcBuffer.Length;
        Word = 0;
        BitsAvailable = 0;
    }

    /// <summary>解析 SPS（Sequence Parameter Set）</summary>
    /// <returns>SPS 信息（profile/level/width/height）</returns>
    public SPSInfo ReadSPS()
    {
        var sarScale = 1;
        UInt32 frameCropLeftOffset = 0;
        UInt32 frameCropRightOffset = 0;
        UInt32 frameCropTopOffset = 0;
        UInt32 frameCropBottomOffset = 0;
        ReadByte();
        //profile_idc
        var profileIdc = ReadByte();
        //constraint_set[0-4]_flag, u(5)
        var profileCompat = ReadBits(5);
        //reserved_zero_3bits
        SkipBits(3);
        //level_idc u(8)
        var levelIdc = ReadByte();
        //seq_parameter_set_id
        SkipUEG();
        if (profileIdc == 100 ||
            profileIdc == 110 ||
            profileIdc == 122 ||
            profileIdc == 244 ||
            profileIdc == 44 ||
            profileIdc == 83 ||
            profileIdc == 86 ||
            profileIdc == 118 ||
            profileIdc == 128)
        {
            var chromaFormatIdc = ReadUEG();
            if (chromaFormatIdc == 3)
            {
                SkipBits(1); // separate_colour_plane_flag
            }
            SkipUEG(); // bit_depth_luma_minus8
            SkipUEG(); // bit_depth_chroma_minus8
            SkipBits(1); // qpprime_y_zero_transform_bypass_flag
            if (ReadBoolean())
            { // seq_scaling_matrix_present_flag
                var scalingListCount = (chromaFormatIdc != 3) ? 8 : 12;
                for (var i = 0; i < scalingListCount; i++)
                {
                    if (ReadBoolean())
                    {
                        if (i < 6)
                            SkipScalingList(16);
                        else
                            SkipScalingList(64);
                    }
                }
            }
        }
        SkipUEG(); // log2_max_frame_num_minus4
        var picOrderCntType = ReadUEG();
        if (picOrderCntType == 0)
            ReadUEG(); //log2_max_pic_order_cnt_lsb_minus4
        else if (picOrderCntType == 1)
        {
            SkipBits(1); // delta_pic_order_always_zero_flag
            SkipEG(); // offset_for_non_ref_pic
            SkipEG(); // offset_for_top_to_bottom_field
            var numRefFramesInPicOrderCntCycle = ReadUEG();
            for (var i = 0; i < numRefFramesInPicOrderCntCycle; i++)
                SkipEG(); // offset_for_ref_frame[ i ]
        }
        SkipUEG(); // max_num_ref_frames
        SkipBits(1); // gaps_in_frame_num_value_allowed_flag
        var picWidthInMbsMinus1 = ReadUEG();
        var picHeightInMapUnitsMinus1 = ReadUEG();
        var frameMbsOnlyFlag = ReadBits(1);
        if (frameMbsOnlyFlag == 0)
            SkipBits(1); // mb_adaptive_frame_field_flag
        SkipBits(1); // direct_8x8_inference_flag
        if (ReadBoolean())
        {
            // frame_cropping_flag
            frameCropLeftOffset = ReadUEG();
            frameCropRightOffset = ReadUEG();
            frameCropTopOffset = ReadUEG();
            frameCropBottomOffset = ReadUEG();
        }
        if (ReadBoolean())
        {
            // vui_parameters_present_flag
            if (ReadBoolean())
            {
                // aspect_ratio_info_present_flag
                Byte[] sarRatio = null;
                var aspectRatioIdc = ReadByte();
                switch (aspectRatioIdc)
                {
                    case 1: sarRatio = [1, 1]; break;
                    case 2: sarRatio = [12, 11]; break;
                    case 3: sarRatio = [10, 11]; break;
                    case 4: sarRatio = [16, 11]; break;
                    case 5: sarRatio = [40, 33]; break;
                    case 6: sarRatio = [24, 11]; break;
                    case 7: sarRatio = [20, 11]; break;
                    case 8: sarRatio = [32, 11]; break;
                    case 9: sarRatio = [80, 33]; break;
                    case 10: sarRatio = [18, 11]; break;
                    case 11: sarRatio = [15, 11]; break;
                    case 12: sarRatio = [64, 33]; break;
                    case 13: sarRatio = [160, 99]; break;
                    case 14: sarRatio = [4, 3]; break;
                    case 15: sarRatio = [3, 2]; break;
                    case 16: sarRatio = [2, 1]; break;
                    case 255:
                        sarRatio = [(Byte)(ReadByte() << 8 | ReadByte()), (Byte)(ReadByte() << 8 | ReadByte())];
                        break;
                }
                if (sarRatio != null)
                    sarScale = sarRatio[0] / sarRatio[1];
            }
        }
        var width = (Int32)((((picWidthInMbsMinus1 + 1) * 16) - frameCropLeftOffset * 2 - frameCropRightOffset * 2) * sarScale);
        var height = (Int32)(((2 - frameMbsOnlyFlag) * (picHeightInMapUnitsMinus1 + 1) * 16) - ((frameMbsOnlyFlag == 1U ? 2 : 4) * (frameCropTopOffset + frameCropBottomOffset)));
        return new SPSInfo { profileIdc = profileIdc, levelIdc = levelIdc, profileCompat = profileCompat, width = width, height = height };
    }

    /// <summary>加载下一个 4 字节到工作字</summary>
    public void LoadWord()
    {
        var position = SrcBuffer.Length - BytesAvailable;
        var tmpAvailableBytes = BytesAvailable - 4;
        var availableBytes = Math.Min(4, BytesAvailable);
        var workingBytes = ReadOnlySpan<Byte>.Empty;
        if (tmpAvailableBytes < 0)
        {
            var buffer = new Byte[4];
            for (var i = 0; i < BytesAvailable; i++)
                buffer[i] = SrcBuffer[position + i];
            workingBytes = buffer;
        }
        else
        {
            workingBytes = SrcBuffer.Slice(position, 4);
        }
        Word = BinaryPrimitives.ReadInt32BigEndian(workingBytes);
        BitsAvailable = availableBytes * 8;
        BytesAvailable -= availableBytes;
    }

    /// <summary>跳过指定数量的位</summary>
    /// <param name="count">跳过的位数</param>
    public void SkipBits(Int32 count)
    {
        if (BitsAvailable > count)
        {
            Word <<= count;
            BitsAvailable -= count;
        }
        else
        {
            count -= BitsAvailable;
            var skipBytes = count >> 3;
            count -= (skipBytes >> 3);
            LoadWord();
            Word <<= count;
            BitsAvailable -= count;
        }
    }

    /// <summary>读取指定数量的位</summary>
    /// <param name="size">读取的位数（≤ 32）</param>
    /// <returns>读取的值</returns>
    public UInt32 ReadBits(Int32 size)
    {
        var bits = Math.Min(BitsAvailable, size);
        var valu = (UInt32)Word >> (32 - bits);
        if (size > 32)
            throw new OverflowException("Cannot read more than 32 bits at a time");

        BitsAvailable -= bits;
        if (BitsAvailable > 0)
            Word <<= bits;
        else if (BytesAvailable > 0)
            LoadWord();

        bits = size - bits;
        if (bits > 0)
            return (valu << bits) | ReadBits(bits);

        return valu;
    }

    /// <summary>跳过前导零位计数</summary>
    public Int32 SkipLZ()
    {
        Int32 leadingZeroCount;
        for (leadingZeroCount = 0; leadingZeroCount < BitsAvailable; ++leadingZeroCount)
        {
            if (0 != (Word & (0x80000000 >> leadingZeroCount)))
            {
                Word <<= leadingZeroCount;
                BitsAvailable -= leadingZeroCount;
                return leadingZeroCount;
            }
        }
        LoadWord();
        return (leadingZeroCount + SkipLZ());
    }

    /// <summary>跳过无符号 Exp-Golomb 编码值</summary>
    public void SkipUEG() => SkipBits(1 + SkipLZ());

    /// <summary>跳过有符号 Exp-Golomb 编码值</summary>
    public void SkipEG() => SkipBits(1 + SkipLZ());

    /// <summary>读取无符号 Exp-Golomb 编码值</summary>
    public UInt32 ReadUEG()
    {
        var clz = SkipLZ();
        return ReadBits(clz + 1) - 1;
    }

    /// <summary>读取有符号 Exp-Golomb 编码值</summary>
    public Int32 ReadEG()
    {
        var valu = (Int32)ReadUEG();
        if ((0x01 & valu) == 1)
            return (1 + valu) >> 1;
        else
            return -1 * (valu >> 1);
    }

    /// <summary>读取 1 位布尔值</summary>
    public Boolean ReadBoolean() => 1 == ReadBits(1);

    /// <summary>读取 1 字节</summary>
    public Byte ReadByte() => (Byte)ReadBits(8);

    /// <summary>读取 2 字节</summary>
    public UInt16 ReadUShort() => (UInt16)ReadBits(16);

    /// <summary>读取 4 字节</summary>
    public UInt32 ReadUInt() => ReadBits(32);

    /// <summary>跳过缩放列表（H.264 SPS scaling_list）</summary>
    /// <param name="count">列表条目数</param>
    public void SkipScalingList(Int32 count)
    {
        Int32 lastScale = 8, nextScale = 8, j, deltaScale;
        for (j = 0; j < count; j++)
        {
            if (nextScale != 0)
            {
                deltaScale = ReadEG();
                nextScale = (lastScale + deltaScale + 256) % 256;
            }
            lastScale = (nextScale == 0) ? lastScale : nextScale;
        }
    }
}

/// <summary>H.264 SPS（Sequence Parameter Set）解析结果</summary>
public class SPSInfo
{
    /// <summary>profile_idc</summary>
    public Byte profileIdc { get; set; }

    /// <summary>level_idc</summary>
    public Byte levelIdc { get; set; }

    /// <summary>profile_compatibility</summary>
    public UInt32 profileCompat { get; set; }

    /// <summary>视频宽度（像素）</summary>
    public Int32 width { get; set; }

    /// <summary>视频高度（像素）</summary>
    public Int32 height { get; set; }
}
