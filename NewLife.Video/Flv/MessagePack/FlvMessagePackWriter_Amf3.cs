using NewLife.Video.Flv.Metadata;
using System.Runtime.CompilerServices;

namespace NewLife.Video.Flv.MessagePack
{
    public ref partial struct FlvMessagePackWriter
    {
        public void WriteAmf3(Amf3 amf3)
        {
            WriteByte(amf3.DataType);
            if (amf3.Amf3Metadatas != null && amf3.Amf3Metadatas.Count > 0)
            {
                WriteInt32(amf3.Amf3Metadatas.Count);
                foreach (var item in amf3.Amf3Metadatas)
                {
                    //根据数据类型
                    WriteArray(item.ToBuffer());
                }
                //always 9
                WriteByte(0);
                WriteByte(0);
                WriteByte(9);
            }
            else
            {
                WriteInt32(0);
            }
        }
    }
}
