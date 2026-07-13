namespace NewLife.Video.Flv.Metadata
{
    public class Amf3
    {
        /// <summary>AMF3数据类型</summary>
        public Byte DataType { get; set; } = 0x08;

        /// <summary>元素个数</summary>
        public UInt32 Count { get; set; }

        /// <summary></summary>
        public List<IAmf3Metadata> Amf3Metadatas { get; set; }
    }
}
