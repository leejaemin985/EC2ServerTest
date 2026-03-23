/// <summary>
/// 서버 → 클라이언트: 오브젝트 제거 통보.
/// [NetId(4)]
/// </summary>
public class DespawnPacket : Packet
{
    public override PacketType Type => PacketType.Despawn;
    public override PacketChannel Channel => PacketChannel.Tcp;

    public uint NetId;

    public override void Serialize(PacketWriter writer)
    {
        writer.WriteUInt(NetId);
    }

    public override void Deserialize(PacketReader reader)
    {
        NetId = reader.ReadUInt();
    }
}
