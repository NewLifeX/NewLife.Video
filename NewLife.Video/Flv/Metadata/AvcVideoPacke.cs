using NewLife.Video.Flv.Enums;

namespace NewLife.Video.Flv.Metadata;
    /// <summary>AVC 视频数据包</summary>
    public class AvcVideoPacke
    {
        /// <summary>AVC 视频数据包</summary>
        public AvcPacketType AvcPacketType { get; set; }

        /// <summary>AVC 视频数据包</summary>
        public UInt32 CompositionTime { get; set; }

        /// <summary>AVC 视频数据包</summary>
        public AVCDecoderConfigurationRecord AVCDecoderConfiguration { get; set; }

        /// <summary>AVC 视频数据包</summary>
        public List<Byte[]> MultiData { get; set; }
    }
