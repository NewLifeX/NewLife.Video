using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    /// <summary>轨道引用 Box。包含轨道间引用关系的容器</summary>
    public class TrackReferenceBox : Mp4Box, IFmp4MessagePackFormatter
    {
        public TrackReferenceBox() : base("tref") { }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            End(ref writer);
        }
    }