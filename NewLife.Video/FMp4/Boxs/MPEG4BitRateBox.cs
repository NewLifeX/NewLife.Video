using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4
{
    /// <summary>btrt</summary>
    public class MPEG4BitRateBox : Mp4Box, IFmp4MessagePackFormatter
    {
        /// <summary>btrt</summary>
        public MPEG4BitRateBox() : base("btrt") { }

        public UInt32 BufferSizeDB { get; set; }

        public UInt32 MaxBitRate { get; set; }

        public UInt32 AvgBitRate { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            writer.WriteUInt32(BufferSizeDB);
            writer.WriteUInt32(MaxBitRate);
            writer.WriteUInt32(AvgBitRate);
            End(ref writer);
        }
    }
}