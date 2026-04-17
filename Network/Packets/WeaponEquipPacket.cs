/// <summary>
/// 서버 → 클라이언트: 무기 장착 통보.
/// [NetId(4)][WeaponId(string)]
/// </summary>
public class WeaponEquipPacket : Packet
{
    public override PacketType Type => PacketType.WeaponEquip;
    public override PacketChannel Channel => PacketChannel.Tcp;

    public uint NetId;
    public string WeaponId = "";

    public override void Serialize(PacketWriter writer)
    {
        writer.WriteUInt(NetId);
        writer.WriteString(WeaponId);
    }

    public override void Deserialize(PacketReader reader)
    {
        NetId = reader.ReadUInt();
        WeaponId = reader.ReadString();
    }
}
