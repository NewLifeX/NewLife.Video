using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4
{
    /// <summary>mdat</summary>
    public class MediaDataBox : Mp4Box, IFmp4MessagePackFormatter
    {
        /// <summary>mdat</summary>
        public MediaDataBox() : base("mdat") { }

        /// <summary>
        /// 过滤掉AUD/SPS/PPS NAL <br/>
        /// 然后将其他NAL写入Data中 <br/>
        /// Filter out AUD/SPS/PPS NAL units from your stream<br/>
        /// Write you converted NAL units into the MDAT box<br/>
        /// </summary>
        public List<Byte[]> Data { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);

            if (Data != null && Data.Count > 0)
            {
                foreach (var nalu in Data)
                {
                    if (nalu != null && nalu.Length > 0)
                    {
                        writer.WriteInt32(nalu.Length);
                        writer.WriteArray(nalu);
                    }
                }
            }
            End(ref writer);
        }
    }
}