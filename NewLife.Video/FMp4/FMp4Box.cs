using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4
{
    /// <summary>
    /// Fmp4
    /// stream data 
    /// ftyp
    /// moov
    /// moof 1
    /// mdat 1
    /// ...
    /// moof n
    /// mdat n
    /// mfra
    /// </summary>
    public class Fmp4Box : IFmp4MessagePackFormatter
    {
        /// <summary>ftyp</summary>
        public FileTypeBox FileTypeBox { get; set; }

        /// <summary>moov</summary>
        public MovieBox MovieBox { get; set; }

        /// <summary>fboxs</summary>
        public List<FragmentBox> FragmentBoxs { get; set; }

        /// <summary>mfra</summary>
        public MovieFragmentRandomAccessBox MovieFragmentRandomAccessBox { get; set; }

        /// <summary></summary>
        /// <param name="writer"></param>
        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            FileTypeBox.ToBuffer(ref writer);
            MovieBox.ToBuffer(ref writer);
            if (FragmentBoxs != null && FragmentBoxs.Count > 0)
            {
                foreach (var item in FragmentBoxs)
                {
                    item.MovieFragmentBox.ToBuffer(ref writer);
                    item.MediaDataBox.ToBuffer(ref writer);
                }
            }

            MovieFragmentRandomAccessBox?.ToBuffer(ref writer);
        }
    }
}