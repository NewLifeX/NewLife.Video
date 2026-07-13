using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    /// <summary>stco</summary>
    public class ChunkOffsetBox : FullBox, IFmp4MessagePackFormatter
    {
        /// <summary>stco</summary>
        /// <param name="version"></param>
        /// <param name="flags"></param>
        public ChunkOffsetBox(Byte version = 0, UInt32 flags = 0) : base("stco", version, flags) { }

        /// <summary>条目数</summary>
        public UInt32 EntryCount { get; set; }

        /// <summary>length:EntryCount</summary>
        public List<UInt32> ChunkOffset { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            WriterFullBoxToBuffer(ref writer);
            if (ChunkOffset != null && ChunkOffset.Count > 0)
            {
                writer.WriteUInt32((UInt32)ChunkOffset.Count);
                foreach (var item in ChunkOffset)
                {
                    writer.WriteUInt32(item);
                }
            }
            else
            {
                writer.WriteUInt32(0);
            }
            End(ref writer);
        }
    }