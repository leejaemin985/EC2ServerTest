/// <summary>
/// 클라이언트 → 서버: 발사 요청.
/// [Yaw(4)][Pitch(4)]
/// 서버가 해당 플레이어의 위치 + 조준 방향으로 히트스캔/투사체 처리.
/// </summary>
public class ShootPacket : Packet
{
    public override PacketType Type => PacketType.Shoot;
    public override PacketChannel Channel => PacketChannel.Udp;

    public float Yaw;
    public float Pitch;

    public override void Serialize(PacketWriter writer)
    {
        writer.WriteFloat(Yaw);
        writer.WriteFloat(Pitch);
    }

    public override void Deserialize(PacketReader reader)
    {
        Yaw = reader.ReadFloat();
        Pitch = reader.ReadFloat();
    }
}
