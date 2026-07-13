using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    /// <summary>dref</summary>
    public class DataReferenceBox : FullBox, IFmp4MessagePackFormatter
    {
        /// <summary>dref</summary>
        /// <param name="version"></param>
        /// <param name="flags"></param>
        public DataReferenceBox(Byte version = 0, UInt32 flags = 0) : base("dref", version, flags) { }

        public UInt32 EntryCount { get; set; }

        /// <summary>
        /// length:EntryCount
        /// </summary>
        public List<DataEntryBox> DataEntryBoxes { get; set; } = new List<DataEntryBox>();

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            WriterFullBoxToBuffer(ref writer);
            if (DataEntryBoxes != null && DataEntryBoxes.Count > 0)
            {
                writer.WriteUInt32((UInt32)DataEntryBoxes.Count);
                foreach (var item in DataEntryBoxes)
                {
                    item.ToBuffer(ref writer);
                }
            }
            else
            {
                writer.WriteUInt32(0);
            }
            End(ref writer);
        }
    }