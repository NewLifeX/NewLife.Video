using NewLife.Video.Flv.Enums;

namespace NewLife.Video.Flv.Metadata;
    public class AacPacke
    {
        public AacPacke(AACPacketType packetType, Byte[] data = null)
        {
            AACPacketType = packetType;
            if (packetType == AACPacketType.AudioSpecificConfig)
            {
                Data.Add(0);
                Data.AddRange(new AudioSpecificConfig().ToArray());
            }
            else
            {
                Data.Add(1);
                Data.AddRange(data ?? throw new ArgumentNullException(nameof(data)));
            }
        }
        public AACPacketType AACPacketType { get; private set; }

        private List<Byte> Data { get; set; } = new List<Byte>();

        /// <summary>元数据</summary>
        public Byte[] RawData => Data.ToArray();
    }
