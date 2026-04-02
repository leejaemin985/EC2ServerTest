/// <summary>
/// 서버 → 클라이언트: 플레이어 상태 변경 통보.
/// [NetId(4)][State(1)]
/// </summary>
public class PlayerStatePacket : Packet
{
    public override PacketType Type => PacketType.PlayerState;
    public override PacketChannel Channel => PacketChannel.Tcp;

    public uint NetId;
    public byte State;

    public override void Serialize(PacketWriter writer)
    {
        writer.WriteUInt(NetId);
        writer.WriteByte(State);
    }

    public override void Deserialize(PacketReader reader)
    {
        NetId = reader.ReadUInt();
        State = reader.ReadByte();
    }
}
