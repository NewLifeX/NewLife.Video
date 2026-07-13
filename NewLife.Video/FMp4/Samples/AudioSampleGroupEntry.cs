namespace NewLife.Video.FMp4.Samples
{
    public abstract class AudioSampleGroupEntry : SampleGroupDescriptionEntry
    {
        /// <summary>条目数</summary>
        /// <param name="groupingType"></param>
        public AudioSampleGroupEntry(UInt32 groupingType) : base(groupingType) { }
    }
}