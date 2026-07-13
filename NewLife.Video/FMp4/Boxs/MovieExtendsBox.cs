using NewLife.Video.FMp4.Interfaces;
using NewLife.Video.FMp4.MessagePack;

namespace NewLife.Video.FMp4;
    /// <summary>mvex</summary>
    public class MovieExtendsBox : Mp4Box, IFmp4MessagePackFormatter
    {
        /// <summary>mvex</summary>
        public MovieExtendsBox() : base("mvex") { }

        /// <summary>mehd</summary>
        public MovieExtendsHeaderBox MovieExtendsHeaderBox { get; set; }

        /// <summary>trex</summary>
        public List<TrackExtendsBox> TrackExtendsBoxs { get; set; }

        public void ToBuffer(ref Fmp4MessagePackWriter writer)
        {
            Start(ref writer);
            MovieExtendsHeaderBox?.ToBuffer(ref writer);

            if (TrackExtendsBoxs != null)
            {
                foreach (var item in TrackExtendsBoxs)
                {
                    item.ToBuffer(ref writer);
                }
            }
            End(ref writer);
        }
    }
