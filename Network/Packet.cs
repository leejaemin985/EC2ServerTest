using System.Buffers.Binary;

/// <summary>
/// 패킷 전송 채널.
/// </summary>
public enum PacketChannel : byte
{
    Tcp,
    Udp,
}

/// <summary>
/// 패킷 타입 식별자.
/// 각 패킷 클래스가 고유한 타입을 가진다.
/// </summary>
public enum PacketType : ushort
{
    // 연결 / 세션 (0 ~ 999)
    Connected = 1,
    Disconnected = 2,

    // 게임 오브젝트 (1000 ~ 1999)
    Spawn = 1000,
    Despawn = 1001,
    Transform = 1002,

    // 입력 (2000 ~ 2999)
    Input = 2000,

    // 전투 / 액션 (3000 ~ 3999)

    // 상태 / 동기화 (4000 ~ 4999)

    // 채팅 / 소셜 (5000 ~ 5999)

    // 시스템 / 관리 (6000 ~ 6999)
}

/// <summary>
/// 모든 패킷의 베이스 클래스.
/// 직렬화/역직렬화 인터페이스를 정의한다.
/// </summary>
public abstract class Packet
{
    /// <summary>패킷 타입 식별자</summary>
    public abstract PacketType Type { get; }

    /// <summary>이 패킷이 사용할 채널</summary>
    public abstract PacketChannel Channel { get; }

    /// <summary>이 패킷이 발생한 시점의 틱</summary>
    public int Tick { get; set; }

    /// <summary>페이로드를 바이트 배열로 직렬화한다. (타입/틱 헤더 제외)</summary>
    public abstract void Serialize(PacketWriter writer);

    /// <summary>바이트 배열에서 페이로드를 역직렬화한다. (타입/틱 헤더 제외)</summary>
    public abstract void Deserialize(PacketReader reader);
}

/// <summary>
/// 바이너리 쓰기 유틸리티. 패킷 직렬화에 사용.
/// </summary>
public class PacketWriter
{
    private byte[] _buffer;
    private int _pos;

    public PacketWriter(int capacity = 256)
    {
        _buffer = new byte[capacity];
        _pos = 0;
    }

    public int Length => _pos;

    private void EnsureCapacity(int additional)
    {
        if (_pos + additional > _buffer.Length)
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _pos + additional));
    }

    public void WriteByte(byte value)
    {
        EnsureCapacity(1);
        _buffer[_pos++] = value;
    }

    public void WriteUShort(ushort value)
    {
        EnsureCapacity(2);
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_pos), value);
        _pos += 2;
    }

    public void WriteInt(int value)
    {
        EnsureCapacity(4);
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_pos), value);
        _pos += 4;
    }

    public void WriteUInt(uint value)
    {
        EnsureCapacity(4);
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(_pos), value);
        _pos += 4;
    }

    public void WriteFloat(float value)
    {
        EnsureCapacity(4);
        BinaryPrimitives.WriteSingleLittleEndian(_buffer.AsSpan(_pos), value);
        _pos += 4;
    }

    public void WriteString(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        WriteUShort((ushort)bytes.Length);
        EnsureCapacity(bytes.Length);
        bytes.CopyTo(_buffer.AsSpan(_pos));
        _pos += bytes.Length;
    }

    /// <summary>패킷 헤더(타입) + 페이로드를 포함한 최종 바이트 배열을 반환한다.</summary>
    public byte[] ToArray() => _buffer.AsSpan(0, _pos).ToArray();

    /// <summary>버퍼를 초기화한다.</summary>
    public void Reset() => _pos = 0;
}

/// <summary>
/// 바이너리 읽기 유틸리티. 패킷 역직렬화에 사용.
/// </summary>
public class PacketReader
{
    private readonly ReadOnlyMemory<byte> _buffer;
    private int _pos;

    public PacketReader(ReadOnlyMemory<byte> buffer)
    {
        _buffer = buffer;
        _pos = 0;
    }

    public int Remaining => _buffer.Length - _pos;

    public byte ReadByte() => _buffer.Span[_pos++];

    public ushort ReadUShort()
    {
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Span.Slice(_pos));
        _pos += 2;
        return value;
    }

    public int ReadInt()
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.Span.Slice(_pos));
        _pos += 4;
        return value;
    }

    public uint ReadUInt()
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Span.Slice(_pos));
        _pos += 4;
        return value;
    }

    public float ReadFloat()
    {
        var value = BinaryPrimitives.ReadSingleLittleEndian(_buffer.Span.Slice(_pos));
        _pos += 4;
        return value;
    }

    public string ReadString()
    {
        ushort length = ReadUShort();
        var value = System.Text.Encoding.UTF8.GetString(_buffer.Span.Slice(_pos, length));
        _pos += length;
        return value;
    }
}
