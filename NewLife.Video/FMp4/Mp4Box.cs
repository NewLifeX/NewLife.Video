using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    /// <summary>条目数</summary>
    public abstract class Mp4Box
    {
        private const Int32 FixedSizeLength = 4;

        public Mp4Box(String boxType) => BoxType = boxType;

        /// <summary>盒子大小</summary>
        public UInt32 Size { get; set; }

        /// <summary>盒子类型</summary>
        public String BoxType { get; set; }

        /// <summary>条目数</summary>
        public Int32 SizePosition;

        /// <summary>条目数</summary>
        /// <param name="writer"></param>
        public void Start(ref Fmp4MessagePackWriter writer)
        {
            writer.Skip(FixedSizeLength, out SizePosition);
            writer.WriteASCII(BoxType);
        }

        /// <summary>条目数</summary>
        /// <param name="writer"></param>
        public void End(ref Fmp4MessagePackWriter writer) => writer.WriteUInt32Return((UInt32)(writer.GetCurrentPosition() - SizePosition), SizePosition);
    }