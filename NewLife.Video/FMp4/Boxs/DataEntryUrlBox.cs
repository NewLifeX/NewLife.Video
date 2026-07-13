using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    public class DataEntryUrlBox : DataEntryBox
    {
        public DataEntryUrlBox(Byte version, UInt32 flags) : base("url ", version, flags) { }

        public DataEntryUrlBox(UInt32 flags) : this(0, flags) { }

        public String Location { get; set; }

        public override void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            WriterFullBoxToBuffer(ref writer);
            if (!String.IsNullOrEmpty(Location))
            {
                writer.WriteUTF8(Location);
            }
            End(ref writer);
        }
    }
