using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4
{
    /// <summary>uri</summary>
    public class URIBox : FullBox, IFmp4MessagePackFormatter
    {
        /// <summary>uri</summary>
        /// <param name="version"></param>
        /// <param name="flags"></param>
        public URIBox(Byte version = 0, UInt32 flags = 0) : base("uri ", version, flags) { }

        public String TheURI { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);

            WriterFullBoxToBuffer(ref writer);
            writer.WriteUTF8(TheURI ?? "\0");

            End(ref writer);
        }
    }
}