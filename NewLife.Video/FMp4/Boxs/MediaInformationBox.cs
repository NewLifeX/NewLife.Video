using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4
{
    /// <summary>minf</summary>
    public class MediaInformationBox : Mp4Box, IFmp4MessagePackFormatter
    {
        /// <summary>minf</summary>
        public MediaInformationBox() : base("minf") { }

        /// <summary>vmhd</summary>
        public VideoMediaHeaderBox VideoMediaHeaderBox { get; set; }

        /// <summary>dinf</summary>
        public DataInformationBox DataInformationBox { get; set; }

        /// <summary>stbl</summary>
        public SampleTableBox SampleTableBox { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);

            VideoMediaHeaderBox?.ToBuffer(ref writer);
            DataInformationBox.ToBuffer(ref writer);
            SampleTableBox.ToBuffer(ref writer);

            End(ref writer);
        }
    }
}
