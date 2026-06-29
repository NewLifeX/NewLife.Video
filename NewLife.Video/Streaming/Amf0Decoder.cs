using System.Text;

namespace NewLife.Video.Streaming;

/// <summary>AMF0 (Action Message Format 0) 解码器。用于 RTMP 响应消息的反序列化</summary>
/// <remarks>
/// 支持 AMF0 类型：Number(0x00), Boolean(0x01), String(0x02), Object(0x03), Null(0x05), ECMA Array(0x08)。
/// 提供 TryReadXxx 方法族，失败返回 false 而非抛异常。
/// </remarks>
public class Amf0Decoder
{
    private readonly Byte[] _data;
    private Int32 _offset;

    /// <summary>当前读取位置</summary>
    public Int32 Position => _offset;

    /// <summary>数据总长度</summary>
    public Int32 Length => _data.Length;

    /// <summary>是否已读完</summary>
    public Boolean IsEnd => _offset >= _data.Length;

    /// <summary>创建解码器</summary>
    /// <param name="data">AMF0 编码的字节数组</param>
    /// <param name="offset">起始偏移</param>
    public Amf0Decoder(Byte[] data, Int32 offset = 0)
    {
        _data = data ?? [];
        _offset = offset;
    }

    /// <summary>读取 Number（8 字节大端 IEEE 754 double）</summary>
    public Boolean TryReadNumber(out Double value)
    {
        value = 0;
        if (_offset >= _data.Length || _data[_offset] != 0x00) return false;
        _offset++;

        if (_offset + 8 > _data.Length) return false;
        var bytes = new Byte[8];
        Array.Copy(_data, _offset, bytes, 0, 8);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        value = BitConverter.ToDouble(bytes, 0);
        _offset += 8;
        return true;
    }

    /// <summary>读取 Boolean（1 字节）</summary>
    public Boolean TryReadBoolean(out Boolean value)
    {
        value = false;
        if (_offset >= _data.Length || _data[_offset] != 0x01) return false;
        _offset++;
        if (_offset >= _data.Length) return false;
        value = _data[_offset++] != 0x00;
        return true;
    }

    /// <summary>读取 String（2 字节大端长度 + UTF-8 内容）</summary>
    public Boolean TryReadString(out String value)
    {
        value = null;
        if (_offset >= _data.Length || _data[_offset] != 0x02) return false;
        _offset++;

        if (_offset + 2 > _data.Length) return false;
        var len = ((Int32)_data[_offset] << 8) | _data[_offset + 1];
        _offset += 2;

        if (_offset + len > _data.Length) return false;
        value = Encoding.UTF8.GetString(_data, _offset, len);
        _offset += len;
        return true;
    }

    /// <summary>读取 Null（无数据体）</summary>
    public Boolean TryReadNull()
    {
        if (_offset >= _data.Length || _data[_offset] != 0x05) return false;
        _offset++;
        return true;
    }

    /// <summary>跳过当前值（任意 AMF0 类型）</summary>
    public Boolean SkipValue()
    {
        if (_offset >= _data.Length) return false;
        var type = _data[_offset];
        switch (type)
        {
            case 0x00: // Number
                _offset += 9;
                return _offset <= _data.Length;
            case 0x01: // Boolean
                _offset += 2;
                return _offset <= _data.Length;
            case 0x02: // String
                _offset++;
                if (_offset + 2 > _data.Length) return false;
                var strLen = ((Int32)_data[_offset] << 8) | _data[_offset + 1];
                _offset += 2 + strLen;
                return _offset <= _data.Length;
            case 0x03: // Object
                return SkipObject();
            case 0x05: // Null
                _offset++;
                return true;
            case 0x08: // ECMA Array
                return SkipEcmaArray();
            default:
                return false;
        }
    }

    /// <summary>读取 Object 属性字典</summary>
    public Boolean TryReadObject(out Dictionary<String, Object> props)
    {
        props = [];
        if (_offset >= _data.Length || _data[_offset] != 0x03) return false;
        _offset++;

        while (_offset + 3 <= _data.Length)
        {
            var keyLen = ((Int32)_data[_offset] << 8) | _data[_offset + 1];
            if (keyLen == 0 && _data[_offset + 2] == 0x09)
            {
                _offset += 3; // 跳过结束标记
                return true;
            }

            _offset += 2;
            if (_offset + keyLen > _data.Length) return false;
            var key = Encoding.UTF8.GetString(_data, _offset, keyLen);
            _offset += keyLen;

            if (_offset >= _data.Length) return false;
            var valType = _data[_offset];
            switch (valType)
            {
                case 0x00:
                    if (TryReadNumber(out var num)) props[key] = num;
                    else return false;
                    break;
                case 0x01:
                    if (TryReadBoolean(out var b)) props[key] = b;
                    else return false;
                    break;
                case 0x02:
                    if (TryReadString(out var s)) props[key] = s;
                    else return false;
                    break;
                case 0x05:
                    _offset++;
                    props[key] = null;
                    break;
                default:
                    SkipValue();
                    break;
            }
        }

        return false;
    }

    /// <summary>读取 _result 响应中的 stream ID。格式: _result(tid, null, streamId)</summary>
    public Boolean TryReadCreateStreamResult(out Int32 streamId)
    {
        streamId = -1;
        // 期望: String("_result") + Number(tid) + Null + Number(streamId)
        if (!TryReadString(out var cmd) || cmd != "_result") return false;
        if (!SkipValue()) return false; // 跳过 transaction ID
        if (!TryReadNull()) return false;
        return TryReadNumber(out var d) && (streamId = (Int32)d) >= 0;
    }

    /// <summary>读取 _result / _error 响应，验证 connect 是否成功</summary>
    public Boolean TryReadConnectResult(out Boolean success, out Dictionary<String, Object> info)
    {
        success = false;
        info = [];

        if (!TryReadString(out var cmd)) return false;
        if (cmd == "_error") return true; // success = false

        if (cmd != "_result") return false;

        // 跳过 transaction ID
        if (!SkipValue()) return false;

        // 读取 properties 对象
        if (!TryReadObject(out info)) return false;

        success = true;
        return true;
    }

    private Boolean SkipObject()
    {
        _offset++; // 跳过 0x03 标记
        while (_offset + 3 <= _data.Length)
        {
            var keyLen = ((Int32)_data[_offset] << 8) | _data[_offset + 1];
            if (keyLen == 0 && _data[_offset + 2] == 0x09)
            {
                _offset += 3;
                return true;
            }
            _offset += 2 + keyLen;
            if (!SkipValue()) return false;
        }
        return false;
    }

    private Boolean SkipEcmaArray()
    {
        _offset++; // 跳过 0x08
        _offset += 4; // 跳过 count (4 bytes BE)
        // 之后与 Object 格式相同
        return SkipObject();
    }
}
