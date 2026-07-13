using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4
{
    /// <summary>原始格式 Box。标识受保护数据的原始编码格式</summary>
    public class OriginalFormatBox : Mp4Box, IFmp4MessagePackFormatter
    {
        public OriginalFormatBox(String codingname) : base("frma") => DataFormat = codingname;

        public String DataFormat { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            writer.WriteASCII(DataFormat);
            End(ref writer);
        }
    }
}