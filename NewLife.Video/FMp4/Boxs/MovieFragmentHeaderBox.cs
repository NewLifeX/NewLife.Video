using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    /// <summary>mfhd</summary>
    public class MovieFragmentHeaderBox : FullBox, IFmp4MessagePackFormatter
    {
        /// <summary>mfhd</summary>
        /// <param name="version"></param>
        /// <param name="flags"></param>
        public MovieFragmentHeaderBox(Byte version = 0, UInt32 flags = 0) : base("mfhd", version, flags) { }

        public UInt32 SequenceNumber { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            WriterFullBoxToBuffer(ref writer);
            writer.WriteUInt32(SequenceNumber);
            End(ref writer);
        }
    }
