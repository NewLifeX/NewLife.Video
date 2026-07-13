using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    /// <summary>音频媒体头 Box。包含音频轨道的平衡设置</summary>
    public class SoundMediaHeaderBox : FullBox, IFmp4MessagePackFormatter
    {
        public SoundMediaHeaderBox(Byte version = 0, UInt32 flags = 0) : base("smhd", version, flags) { }

        public UInt16 Balance { get; set; }

        public UInt16 Reserved { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            WriterFullBoxToBuffer(ref writer);
            writer.WriteUInt16(Balance);
            writer.WriteUInt16(Reserved);
            End(ref writer);
        }
    }