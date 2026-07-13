using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4.Samples;
    /// <summary>SampleEntry</summary>
    public abstract class SampleEntry : Mp4Box, IFmp4MessagePackFormatter
    {
        /// <summary>SampleEntry</summary>
        /// <param name="boxType"></param>
        public SampleEntry(String boxType) : base(boxType) { }

        public Byte[] Reserved { get; set; } = new Byte[6];

        public UInt16 DataReferenceIndex { get; set; }

        /// <summary>条目数</summary>
        /// <param name="writer"></param>
        protected void WriterSampleEntryToBuffer(ref Fmp4MessagePackWriter writer)
        {
            foreach (var item in Reserved)
            {
                writer.WriteByte(item);
            }
            writer.WriteUInt16(DataReferenceIndex);
        }

        public abstract void ToBuffer(ref Fmp4MessagePackWriter writer);
    }