using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    /// <summary>子轨道 Box。包含子轨道定义的容器</summary>
    public class SubTrackBox : Mp4Box, IFmp4MessagePackFormatter
    {
        public SubTrackBox() : base("strk") { }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            End(ref writer);
        }
    }