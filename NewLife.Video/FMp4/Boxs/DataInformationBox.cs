using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    /// <summary>dinf</summary>
    public class DataInformationBox : Mp4Box, IFmp4MessagePackFormatter
    {
        /// <summary>dinf</summary>
        public DataInformationBox() : base("dinf") { }

        /// <summary>dref</summary>
        public DataReferenceBox DataReferenceBox { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            DataReferenceBox.ToBuffer(ref writer);
            End(ref writer);
        }
    }