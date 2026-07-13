using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    /// <summary>mehd</summary>
    public class MovieExtendsHeaderBox : FullBox, IFmp4MessagePackFormatter
    {
        /// <summary>mehd</summary>
        /// <param name="version"></param>
        /// <param name="flags"></param>
        public MovieExtendsHeaderBox(Byte version, UInt32 flags = 0) : base("mehd", version, flags) { }

        public UInt32 FragmentDuration { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            WriterFullBoxToBuffer(ref writer);
            writer.WriteUInt32(FragmentDuration);
            End(ref writer);
        }
    }
