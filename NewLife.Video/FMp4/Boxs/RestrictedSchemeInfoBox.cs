using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    /// <summary>受限方案信息 Box。包含受限方案信息的容器</summary>
    public class RestrictedSchemeInfoBox : Mp4Box, IFmp4MessagePackFormatter
    {
        public RestrictedSchemeInfoBox(String fmt) : base("rinf") => OriginalFormatBox = new OriginalFormatBox(fmt);

        public OriginalFormatBox OriginalFormatBox { get; set; }
        public SchemeTypeBox SchemeTypeBox { get; set; }
        public SchemeInformationBox SchemeInformationBox { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            OriginalFormatBox?.ToBuffer(ref writer);
            SchemeTypeBox?.ToBuffer(ref writer);
            SchemeInformationBox?.ToBuffer(ref writer);
            End(ref writer);
        }
    }