/// <summary>
/// 서버→클라: PlayerId 배정 통지.
/// [PlayerId(4)]
/// </summary>
public class PlayerIdAssignPacket : Packet
{
    public override PacketType Type => PacketType.PlayerIdAssign;
    public override PacketChannel Channel => PacketChannel.Tcp;

    public int PlayerId;

    public override void Serialize(PacketWriter writer)
    {
        writer.WriteInt(PlayerId);
    }

    public override void Deserialize(PacketReader reader)
    {
        PlayerId = reader.ReadInt();
    }
}
