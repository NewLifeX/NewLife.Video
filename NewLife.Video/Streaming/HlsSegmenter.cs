namespace NewLife.Video.Streaming;

/// <summary>HLS 分段器。生成 m3u8 播放列表和 TS 分段文件</summary>
/// <remarks>
/// HLS (HTTP Live Streaming) 将视频流切分为短小的 TS 分段，通过 m3u8 播放列表索引。
/// 典型分段时长 2~10 秒。
/// </remarks>
public class HlsSegmenter
{
    private readonly String _outputDir;
    private readonly String _baseName;
    private readonly Int32 _segmentDurationMs;
    private readonly List<HlsSegment> _segments = [];
    private MemoryStream _currentSegment;
    private Int32 _sequenceNumber;
    private Int64 _currentSegmentStartPts;
    private Int32 _targetDuration;

    /// <summary>输出目录</summary>
    public String OutputDir => _outputDir;

    /// <summary>分段列表</summary>
    public IReadOnlyList<HlsSegment> Segments => _segments;

    /// <summary>目标分段时长（秒）</summary>
    public Int32 TargetDurationSeconds => _targetDuration;

    /// <summary>创建 HLS 分段器</summary>
    /// <param name="outputDir">输出目录</param>
    /// <param name="baseName">基础文件名（不含扩展名）</param>
    /// <param name="segmentDurationMs">分段时长（毫秒，默认 4000ms）</param>
    public HlsSegmenter(String outputDir, String baseName, Int32 segmentDurationMs = 4000)
    {
        _outputDir = outputDir ?? throw new ArgumentNullException(nameof(outputDir));
        _baseName = baseName ?? throw new ArgumentNullException(nameof(baseName));
        _segmentDurationMs = segmentDurationMs;
        _targetDuration = (segmentDurationMs + 999) / 1000;

        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
    }

    /// <summary>添加视频数据（TS 包）</summary>
    /// <param name="data">TS 数据</param>
    /// <param name="pts">显示时间戳（毫秒）</param>
    /// <param name="isKeyFrame">是否为关键帧（决定是否开始新分段）</param>
    public void AddData(Byte[] data, Int64 pts, Boolean isKeyFrame)
    {
        if (_currentSegment == null || (isKeyFrame && pts - _currentSegmentStartPts >= _segmentDurationMs))
        {
            FinalizeSegment(pts);
            _currentSegment = new MemoryStream();
            _currentSegmentStartPts = pts;
        }

        _currentSegment.Write(data, 0, data.Length);
    }

    /// <summary>完成当前分段</summary>
    private void FinalizeSegment(Int64 endPts)
    {
        if (_currentSegment == null || _currentSegment.Length == 0) return;

        var durationMs = endPts - _currentSegmentStartPts;
        var fileName = $"{_baseName}_{_sequenceNumber:D5}.ts";
        var filePath = Path.Combine(_outputDir, fileName);

        File.WriteAllBytes(filePath, _currentSegment.ToArray());
        _currentSegment.Dispose();
        _currentSegment = null;

        var seg = new HlsSegment
        {
            FileName = fileName,
            Duration = Math.Max(durationMs / 1000.0, 0.1),
            SequenceNumber = _sequenceNumber,
        };
        _segments.Add(seg);
        _sequenceNumber++;

        _targetDuration = Math.Max(_targetDuration, (Int32)(seg.Duration + 0.999));
    }

    /// <summary>生成 m3u8 播放列表</summary>
    /// <param name="maxSegments">播放列表中保留的最大分段数（0=全部）</param>
    /// <returns>m3u8 内容</returns>
    public String GeneratePlaylist(Int32 maxSegments = 0)
    {
        FinalizeSegment(Int64.MaxValue);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:3");
        sb.AppendLine($"#EXT-X-TARGETDURATION:{_targetDuration}");
        sb.AppendLine($"#EXT-X-MEDIA-SEQUENCE:{Math.Max(0, _sequenceNumber - (_segments.Count))}");

        var segments = maxSegments > 0
            ? _segments.Skip(Math.Max(0, _segments.Count - maxSegments))
            : _segments;

        foreach (var seg in segments)
        {
            sb.AppendLine($"#EXTINF:{seg.Duration:F3},");
            sb.AppendLine(seg.FileName);
        }

        sb.AppendLine("#EXT-X-ENDLIST");
        return sb.ToString();
    }

    /// <summary>写入 m3u8 播放列表文件</summary>
    /// <returns>播放列表文件路径</returns>
    public String WritePlaylist(Int32 maxSegments = 0)
    {
        var playlist = GeneratePlaylist(maxSegments);
        var path = Path.Combine(_outputDir, $"{_baseName}.m3u8");
        File.WriteAllText(path, playlist, System.Text.Encoding.UTF8);
        return path;
    }

    /// <summary>清理旧分段（保留最近 N 个）</summary>
    /// <param name="keepCount">保留数量</param>
    public void Cleanup(Int32 keepCount)
    {
        while (_segments.Count > keepCount)
        {
            var seg = _segments[0];
            var path = Path.Combine(_outputDir, seg.FileName);
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            _segments.RemoveAt(0);
        }
    }
}

/// <summary>HLS 分段信息</summary>
public class HlsSegment
{
    /// <summary>文件名</summary>
    public String FileName { get; set; }

    /// <summary>时长（秒）</summary>
    public Double Duration { get; set; }

    /// <summary>序列号</summary>
    public Int32 SequenceNumber { get; set; }

    /// <summary>字符串表示</summary>
    public override String ToString() => $"#{SequenceNumber} {FileName} ({Duration:F1}s)";
}
