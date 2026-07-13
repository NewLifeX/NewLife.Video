using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4
{
    /// <summary>轨道组 Box。包含轨道分组信息的容器</summary>
    public class TrackGroupBox : Mp4Box, IFmp4MessagePackFormatter
    {
        public TrackGroupBox() : base("trgr") { }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            End(ref writer);
        }
    }
}