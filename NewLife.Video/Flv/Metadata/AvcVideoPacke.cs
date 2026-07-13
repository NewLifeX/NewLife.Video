using NewLife.Video.Flv.Enums;

namespace NewLife.Video.Flv.Metadata
{
    /// <summary></summary>
    public class AvcVideoPacke
    {
        /// <summary></summary>
        public AvcPacketType AvcPacketType { get; set; }

        /// <summary></summary>
        public UInt32 CompositionTime { get; set; }

        /// <summary></summary>
        public AVCDecoderConfigurationRecord AVCDecoderConfiguration { get; set; }

        /// <summary></summary>
        public List<Byte[]> MultiData { get; set; }
    }
}
