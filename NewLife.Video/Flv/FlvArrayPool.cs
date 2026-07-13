using System.Buffers;

namespace NewLife.Video.Flv;

/// <summary>FLV 编码数组池。提供复用缓冲区以减少 GC 分配</summary>
public static class FlvArrayPool
{
    /// <summary>默认数组池实例</summary>
    public static ArrayPool<Byte> Instance => ArrayPool<Byte>.Shared;

    /// <summary>从池中租用缓冲区</summary>
    /// <param name="minimumLength">最小长度</param>
    /// <returns>字节数组</returns>
    public static Byte[] Rent(Int32 minimumLength) => Instance.Rent(minimumLength);

    /// <summary>将缓冲区归还池中</summary>
    /// <param name="array">字节数组</param>
    public static void Return(Byte[] array) => Instance.Return(array);
}
