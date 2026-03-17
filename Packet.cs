using System.Buffers.Binary;
using System.Numerics;

public enum PacketType : byte
{
    // TCP 패킷
    Connected = 1,       // 서버 → 클라이언트: 플레이어 ID 할당
    PlayerJoined = 2,    // 서버 → 클라이언트: 다른 플레이어 접속
    PlayerLeft = 3,      // 서버 → 클라이언트: 다른 플레이어 퇴장
    UdpRegister = 4,     // 클라이언트 → 서버: UDP 엔드포인트 등록

    // UDP 패킷
    Position = 10,       // 서버 → 클라이언트: 위치 데이터
    Input = 11,          // 클라이언트 → 서버: 입력 데이터
}

/// <summary>
/// 패킷 직렬화/역직렬화 유틸리티.
/// 모든 패킷 구조: [PacketType(1)] [Payload(...)]
/// </summary>
public static class PacketSerializer
{
    // ── TCP 패킷 쓰기 (길이 프리픽스 포함: [Length(4)][PacketType(1)][Payload]) ──

    /// <summary>Connected 패킷: 할당된 플레이어 ID 전송</summary>
    public static byte[] WriteConnected(int playerId)
    {
        var buf = new byte[4 + 1 + 4]; // length + type + playerId
        BinaryPrimitives.WriteInt32LittleEndian(buf, 5);
        buf[4] = (byte)PacketType.Connected;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(5), playerId);
        return buf;
    }

    /// <summary>PlayerJoined 패킷</summary>
    public static byte[] WritePlayerJoined(int playerId)
    {
        var buf = new byte[4 + 1 + 4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, 5);
        buf[4] = (byte)PacketType.PlayerJoined;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(5), playerId);
        return buf;
    }

    /// <summary>PlayerLeft 패킷</summary>
    public static byte[] WritePlayerLeft(int playerId)
    {
        var buf = new byte[4 + 1 + 4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, 5);
        buf[4] = (byte)PacketType.PlayerLeft;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(5), playerId);
        return buf;
    }

    // ── UDP 패킷 (길이 프리픽스 없음) ──

    /// <summary>
    /// 클라이언트 → 서버 Position 패킷 (타임스탬프 없음):
    /// [Type(1)][PlayerId(4)][X(4)][Y(4)][Z(4)] = 17 bytes
    /// </summary>
    public static byte[] WritePositionClient(int playerId, float x, float y, float z)
    {
        var buf = new byte[17];
        buf[0] = (byte)PacketType.Position;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(1), playerId);
        BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(5), x);
        BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(9), y);
        BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(13), z);
        return buf;
    }

    /// <summary>클라이언트→서버 패킷 파싱 (17바이트)</summary>
    public static (int playerId, float x, float y, float z) ReadPositionClient(ReadOnlySpan<byte> data)
    {
        int id = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(1));
        float x = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(5));
        float y = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(9));
        float z = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(13));
        return (id, x, y, z);
    }

    /// <summary>
    /// 서버 → 클라이언트 Position 패킷 (틱 번호 포함):
    /// [Type(1)][PlayerId(4)][Tick(4)][X(4)][Y(4)][Z(4)] = 21 bytes
    /// </summary>
    public static byte[] WritePositionServer(int playerId, int tick, float x, float y, float z)
    {
        var buf = new byte[21];
        buf[0] = (byte)PacketType.Position;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(1), playerId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(5), tick);
        BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(9), x);
        BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(13), y);
        BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(17), z);
        return buf;
    }

    /// <summary>
    /// 클라이언트 → 서버 Input 패킷 (틱 번호 포함):
    /// [Type(1)][PlayerId(4)][Tick(4)][H(4)][V(4)] = 17 bytes
    /// </summary>
    public static byte[] WriteInput(int playerId, int tick, float h, float v)
    {
        var buf = new byte[17];
        buf[0] = (byte)PacketType.Input;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(1), playerId);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(5), tick);
        BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(9), h);
        BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(13), v);
        return buf;
    }

    /// <summary>Input 패킷 파싱 (틱 포함)</summary>
    public static (int playerId, int tick, float h, float v) ReadInput(ReadOnlySpan<byte> data)
    {
        int id = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(1));
        int tick = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(5));
        float h = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(9));
        float v = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(13));
        return (id, tick, h, v);
    }

    /// <summary>TCP 수신 버퍼에서 패킷 타입 읽기</summary>
    public static PacketType ReadTcpPacketType(ReadOnlySpan<byte> payload)
    {
        return (PacketType)payload[0];
    }
}
