using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4.Interfaces
{
    public interface IFmp4MessagePackFormatter
    {
        void ToBuffer(ref Fmp4MessagePackWriter writer);
    }
}
