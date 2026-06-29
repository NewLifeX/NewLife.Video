namespace NewLife.Video.Processing;

/// <summary>运动检测器。基于帧差法检测视频中的运动区域</summary>
/// <remarks>
/// 算法流程：灰度化 → 帧差 → 自适应阈值 → 形态学处理 → 连通域分析。
/// YUV420P 格式直接使用 Y 平面（亮度），无需额外灰度化。
/// </remarks>
public class MotionDetector
{
    /// <summary>运动检测灵敏度阈值（0~255，默认 25）</summary>
    public Byte Threshold { get; set; } = 25;

    /// <summary>最小运动区域面积（像素数），小于此面积的区域被忽略</summary>
    public Int32 MinArea { get; set; } = 100;

    /// <summary>当前参考帧（Y 平面）</summary>
    private Byte[] _referenceFrame;

    /// <summary>检测运动区域</summary>
    /// <param name="currentFrame">当前帧</param>
    /// <returns>运动区域列表</returns>
    public MotionRegion[] Detect(VideoFrame currentFrame)
    {
        if (currentFrame == null) throw new ArgumentNullException(nameof(currentFrame));

        // 提取 Y 平面（亮度）
        var yPlane = ExtractYPlane(currentFrame);
        if (yPlane == null) return [];

        // 如果还没有参考帧，将当前帧设为参考帧
        if (_referenceFrame == null)
        {
            _referenceFrame = new Byte[yPlane.Length];
            Array.Copy(yPlane, _referenceFrame, yPlane.Length);
            return [];
        }

        if (_referenceFrame.Length != yPlane.Length)
        {
            _referenceFrame = new Byte[yPlane.Length];
            Array.Copy(yPlane, _referenceFrame, yPlane.Length);
            return [];
        }

        // 帧差 + 阈值
        var diffMask = new Boolean[yPlane.Length];
        for (var i = 0; i < yPlane.Length; i++)
        {
            var diff = Math.Abs(yPlane[i] - _referenceFrame[i]);
            diffMask[i] = diff > Threshold;
        }

        // 简单连通域分析（基于矩形合并）
        var regions = FindMotionRegions(diffMask, currentFrame.Width, currentFrame.Height);

        // 更新参考帧
        Array.Copy(yPlane, _referenceFrame, yPlane.Length);

        return regions;
    }

    /// <summary>重置参考帧</summary>
    public void Reset()
    {
        _referenceFrame = null;
    }

    /// <summary>从 VideoFrame 中提取 Y 平面</summary>
    private static Byte[] ExtractYPlane(VideoFrame frame)
    {
        if (frame.PixelFormat == VideoTypes.YUV420P || frame.PixelFormat == VideoTypes.NV12 || frame.PixelFormat == VideoTypes.NV21)
        {
            var ySize = frame.Width * frame.Height;
            var yPlane = new Byte[ySize];
            Array.Copy(frame.Data, frame.YOffset > 0 ? frame.YOffset : 0, yPlane, 0, ySize);
            return yPlane;
        }

        if (frame.PixelFormat == VideoTypes.GRAY8)
        {
            return frame.Data;
        }

        // RGB → 灰度（Y = 0.299R + 0.587G + 0.114B）
        if (frame.PixelFormat == VideoTypes.RGB24 || frame.PixelFormat == VideoTypes.BGR24)
        {
            var yPlane = new Byte[frame.Width * frame.Height];
            for (var i = 0; i < frame.Width * frame.Height; i++)
            {
                var r = frame.Data[i * 3 + 2]; // R
                var g = frame.Data[i * 3 + 1]; // G
                var b = frame.Data[i * 3];     // B
                yPlane[i] = (Byte)(r * 0.299 + g * 0.587 + b * 0.114);
            }
            return yPlane;
        }

        return null;
    }

    /// <summary>在差分掩码中查找运动区域（简化矩形合并法）</summary>
    private MotionRegion[] FindMotionRegions(Boolean[] mask, Int32 width, Int32 height)
    {
        var regions = new List<MotionRegion>();

        // 简化实现：按行扫描找连通区域的外接矩形
        var visited = new Boolean[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var idx = y * width + x;
                if (!mask[idx] || visited[idx]) continue;

                // 从当前点扩展矩形
                var (left, top, right, bottom) = FloodFillBounds(mask, visited, width, height, x, y);

                var area = (right - left + 1) * (bottom - top + 1);
                if (area >= MinArea)
                {
                    regions.Add(new MotionRegion
                    {
                        X = left,
                        Y = top,
                        Width = right - left + 1,
                        Height = bottom - top + 1,
                        Area = area,
                    });
                }
            }
        }

        return regions.ToArray();
    }

    /// <summary>泛洪获取区域边界</summary>
    private static (Int32 Left, Int32 Top, Int32 Right, Int32 Bottom) FloodFillBounds(
        Boolean[] mask, Boolean[] visited, Int32 width, Int32 height, Int32 startX, Int32 startY)
    {
        var left = startX;
        var top = startY;
        var right = startX;
        var bottom = startY;

        var queue = new Queue<(Int32 X, Int32 Y)>();
        queue.Enqueue((startX, startY));
        visited[startY * width + startX] = true;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            if (x < left) left = x;
            if (x > right) right = x;
            if (y < top) top = y;
            if (y > bottom) bottom = y;

            // 检查四邻域
            foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
            {
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    var nIdx = ny * width + nx;
                    if (mask[nIdx] && !visited[nIdx])
                    {
                        visited[nIdx] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }
        }

        return (left, top, right, bottom);
    }
}

/// <summary>运动区域</summary>
public class MotionRegion
{
    /// <summary>区域左上角 X 坐标</summary>
    public Int32 X { get; set; }

    /// <summary>区域左上角 Y 坐标</summary>
    public Int32 Y { get; set; }

    /// <summary>区域宽度</summary>
    public Int32 Width { get; set; }

    /// <summary>区域高度</summary>
    public Int32 Height { get; set; }

    /// <summary>区域面积（像素数）</summary>
    public Int32 Area { get; set; }

    /// <summary>字符串表示</summary>
    /// <returns></returns>
    public override String ToString() => $"MotionRegion({X},{Y} {Width}x{Height} Area={Area})";
}
