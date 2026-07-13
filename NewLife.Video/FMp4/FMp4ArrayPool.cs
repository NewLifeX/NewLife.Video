using System.Buffers;

namespace NewLife.Video.FMp4;

/// <summary>fMP4 编码数组池。提供复用缓冲区以减少 GC 分配</summary>
public static class Fmp4ArrayPool
{
    private static readonly ArrayPool<Byte> ArrayPool = ArrayPool<Byte>.Create();

    /// <summary>从池中租用缓冲区</summary>
    /// <param name="minimumLength">最小长度</param>
    /// <returns>字节数组</returns>
    public static Byte[] Rent(Int32 minimumLength) => ArrayPool.Rent(minimumLength);

    /// <summary>将缓冲区归还池中</summary>
    /// <param name="array">字节数组</param>
    public static void Return(Byte[] array) => ArrayPool.Return(array);
}