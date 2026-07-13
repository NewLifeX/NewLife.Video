using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    /// <summary>子轨道定义 Box。包含子轨道具体信息的容器</summary>
    public class SubTrackDefinitionBox : Mp4Box, IFmp4MessagePackFormatter
    {
        public SubTrackDefinitionBox() : base("strd") { }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            End(ref writer);
        }
    }