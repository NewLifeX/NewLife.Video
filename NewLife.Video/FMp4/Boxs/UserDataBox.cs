using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4
{
    public class UserDataBox : Mp4Box, IFmp4MessagePackFormatter
    {
        public UserDataBox() : base("udta") { }

        public Byte[] Data { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);

            if (Data != null && Data.Length > 0) writer.WriteArray(Data);

            End(ref writer);
        }
    }
}