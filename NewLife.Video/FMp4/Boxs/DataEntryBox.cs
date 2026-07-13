using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4
{
    public abstract class DataEntryBox : FullBox, IFmp4MessagePackFormatter
    {
        public DataEntryBox(String boxType, Byte version, UInt32 flags) : base(boxType, version, flags) { }

        public abstract void ToBuffer(ref Fmp4MessagePackWriter writer);
    }
}