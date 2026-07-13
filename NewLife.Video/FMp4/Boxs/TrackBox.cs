using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4
{
    /// <summary>trak</summary>
    public class TrackBox : Mp4Box, IFmp4MessagePackFormatter
    {
        /// <summary>trak</summary>
        public TrackBox() : base("trak") { }

        /// <summary>tkhd</summary>
        public TrackHeaderBox TrackHeaderBox { get; set; }

        /// <summary>mdia</summary>
        public MediaBox MediaBox { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);

            TrackHeaderBox?.ToBuffer(ref writer);
            MediaBox?.ToBuffer(ref writer);

            End(ref writer);
        }
    }
}