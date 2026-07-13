

using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    /// <summary>条目数</summary>
    public class FragmentBox : IFmp4MessagePackFormatter
    {
        /// <summary>moof</summary>
        public MovieFragmentBox MovieFragmentBox { get; set; }

        /// <summary>mdat</summary>
        public MediaDataBox MediaDataBox { get; set; }

        /// <summary>条目数</summary>
        /// <param name="writer"></param>
        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            if (MovieFragmentBox != null)
            {
                MovieFragmentBox.ToBuffer(ref writer);
            }
            if (MediaDataBox != null)
            {
                MediaDataBox.ToBuffer(ref writer);
            }
        }
    }
