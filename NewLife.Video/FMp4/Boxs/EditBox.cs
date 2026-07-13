using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4
{
    /// <summary>编辑 Box。包含编辑列表的容器</summary>
    public class EditBox : Mp4Box, IFmp4MessagePackFormatter
    {
        public EditBox() : base("edts") { }

        public List<EditListBox> EditListBox { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            End(ref writer);
        }
    }
}
