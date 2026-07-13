namespace NewLife.Video.Codecs;

/// <summary>H.264 NAL Unit 头结构</summary>
/// <remarks>
/// NAL Unit Header 占 1 字节：
/// - bit 7: forbidden_zero_bit (F)
/// - bit 6-5: nal_ref_idc (NRI)
/// - bit 4-0: nal_unit_type
/// </remarks>
public struct NALUHeader
{
    /// <summary>从单字节构造 NAL 头</summary>
    /// <param name="value">NAL 头字节</param>
    public NALUHeader(Byte value)
    {
        ForbiddenZeroBit = (value & 0x80) >> 7;
        NalRefIdc = (value & 0x60) >> 5;
        NalUnitType = (NalUnitType)(value & 0x1f);
        KeyFrame = NalUnitType == NalUnitType.IDR;
    }

    /// <summary>从 NAL 数据区首字节构造 NAL 头</summary>
    /// <param name="value">至少包含 1 字节的缓冲区</param>
    public NALUHeader(ReadOnlySpan<Byte> value)
    {
        ForbiddenZeroBit = (value[0] & 0x80) >> 7;
        NalRefIdc = (value[0] & 0x60) >> 5;
        NalUnitType = (NalUnitType)(value[0] & 0x1f);
        KeyFrame = NalUnitType == NalUnitType.IDR;
    }

    /// <summary>禁止位（必须为 0）</summary>
    public Int32 ForbiddenZeroBit { get; set; }

    /// <summary>NAL 引用级别</summary>
    public Int32 NalRefIdc { get; set; }

    /// <summary>NAL 单元类型</summary>
    public NalUnitType NalUnitType { get; set; }

    /// <summary>是否关键帧（IDR）</summary>
    public Boolean KeyFrame { get; set; }
}

/// <summary>H.264 NAL 单元类型</summary>
public enum NalUnitType : Int32
{
    /// <summary>未指定</summary>
    None = 0,

    /// <summary>非 IDR 图像片</summary>
    SLICE = 1,

    /// <summary>片分区 A</summary>
    DPA = 2,

    /// <summary>片分区 B</summary>
    DPB = 3,

    /// <summary>片分区 C</summary>
    DPC = 4,

    /// <summary>IDR（即时解码刷新）图像片</summary>
    IDR = 5,

    /// <summary>补充增强信息</summary>
    SEI = 6,

    /// <summary>序列参数集</summary>
    SPS = 7,

    /// <summary>图像参数集</summary>
    PPS = 8,

    /// <summary>访问单元分隔符</summary>
    AUD = 9,

    /// <summary>序列结束</summary>
    EOSEQ = 10,

    /// <summary>码流结束</summary>
    EOSTREAM = 11,

    /// <summary>填充数据</summary>
    FILL = 12,
}
