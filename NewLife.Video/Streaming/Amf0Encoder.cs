using System.Text;

namespace NewLife.Video.Streaming;

/// <summary>AMF0 (Action Message Format 0) 编码器。用于 RTMP 命令消息的序列化</summary>
/// <remarks>
/// 支持 AMF0 基本类型：Number, Boolean, String, Object, Null, ECMA Array。
/// RTMP connect/createStream/publish 等命令均依赖此编码器。
/// </remarks>
public class Amf0Encoder
{
    private readonly MemoryStream _stream;

    public Amf0Encoder() => _stream = new MemoryStream(256);

    public Int32 Length => (Int32)_stream.Length;
    public Byte[] ToArray() => _stream.ToArray();

    public void WriteNumber(Double value)
    {
        _stream.WriteByte(0x00); // type marker
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        _stream.Write(bytes, 0, 8);
    }

    public void WriteBoolean(Boolean value)
    {
        _stream.WriteByte(0x01);
        _stream.WriteByte(value ? (Byte)0x01 : (Byte)0x00);
    }

    public void WriteString(String value)
    {
        _stream.WriteByte(0x02);
        var b = Encoding.UTF8.GetBytes(value ?? "");
        WriteUInt16BE((UInt16)b.Length);
        _stream.Write(b, 0, b.Length);
    }

    public void WriteObject(String key, Amf0Encoder value)
    {
        _stream.WriteByte(0x03); // object marker
        // key-value pairs
        var keyBytes = Encoding.UTF8.GetBytes(key);
        WriteUInt16BE((UInt16)keyBytes.Length);
        _stream.Write(keyBytes, 0, keyBytes.Length);
        // nested value
        var nested = value.ToArray();
        _stream.Write(nested, 0, nested.Length);
        // object end marker
        WriteUInt24BE(0x000009); // UTF-8-empty + object end
    }

    public void WriteObjectBegin()
    {
        _stream.WriteByte(0x03);
    }

    public void WriteObjectProperty(String name, String value)
    {
        var keyBytes = Encoding.UTF8.GetBytes(name);
        WriteUInt16BE((UInt16)keyBytes.Length);
        _stream.Write(keyBytes, 0, keyBytes.Length);
        // string value
        _stream.WriteByte(0x02);
        var valBytes = Encoding.UTF8.GetBytes(value ?? "");
        WriteUInt16BE((UInt16)valBytes.Length);
        _stream.Write(valBytes, 0, valBytes.Length);
    }

    public void WriteObjectProperty(String name, Double value)
    {
        var keyBytes = Encoding.UTF8.GetBytes(name);
        WriteUInt16BE((UInt16)keyBytes.Length);
        _stream.Write(keyBytes, 0, keyBytes.Length);
        WriteNumber(value);
    }

    public void WriteObjectProperty(String name, Boolean value)
    {
        var keyBytes = Encoding.UTF8.GetBytes(name);
        WriteUInt16BE((UInt16)keyBytes.Length);
        _stream.Write(keyBytes, 0, keyBytes.Length);
        WriteBoolean(value);
    }

    public void WriteObjectEnd()
    {
        WriteUInt24BE(0x000009); // UTF-8-empty terminator
    }

    public void WriteNull()
    {
        _stream.WriteByte(0x05);
    }

    public void WriteEcmaArray(Int32 count)
    {
        _stream.WriteByte(0x08); // ECMA array marker
        WriteUInt32BE((UInt32)count);
    }

    private void WriteUInt16BE(UInt16 v)
    {
        _stream.WriteByte((Byte)(v >> 8));
        _stream.WriteByte((Byte)v);
    }

    private void WriteUInt24BE(Int32 v)
    {
        _stream.WriteByte((Byte)(v >> 16));
        _stream.WriteByte((Byte)(v >> 8));
        _stream.WriteByte((Byte)v);
    }

    private void WriteUInt32BE(UInt32 v)
    {
        _stream.WriteByte((Byte)(v >> 24));
        _stream.WriteByte((Byte)(v >> 16));
        _stream.WriteByte((Byte)(v >> 8));
        _stream.WriteByte((Byte)v);
    }
}
