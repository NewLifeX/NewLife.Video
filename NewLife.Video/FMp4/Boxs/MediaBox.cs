using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4
{
    /// <summary>
    /// mdia
    /// </summary>
    public class MediaBox : Mp4Box, IFmp4MessagePackFormatter
    {
        /// <summary>
        /// mdia
        /// </summary>
        public MediaBox() : base("mdia")
        {
        }

        /// <summary>
        /// mdhd
        /// </summary>
        public MediaHeaderBox MediaHeaderBox { get; set; }

        /// <summary>
        /// hdlr
        /// </summary>
        public HandlerBox HandlerBox { get; set; }

        /// <summary>
        /// minf
        /// </summary>
        public MediaInformationBox MediaInformationBox { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            MediaHeaderBox.ToBuffer(ref writer);
            HandlerBox.ToBuffer(ref writer);
            MediaInformationBox.ToBuffer(ref writer);
            End(ref writer);
        }
    }
}
