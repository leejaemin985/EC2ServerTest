/// <summary>
/// 서버 → 클라이언트: 피격 결과 통보.
/// [ShooterNetId(4)][VictimNetId(4)][HitX(4)][HitY(4)][HitZ(4)][Damage(4)]
/// </summary>
public class HitPacket : Packet
{
    public override PacketType Type => PacketType.Hit;
    public override PacketChannel Channel => PacketChannel.Tcp;

    public uint ShooterNetId;
    public uint VictimNetId;
    public float HitX, HitY, HitZ;
    public float Damage;

    public override void Serialize(PacketWriter writer)
    {
        writer.WriteUInt(ShooterNetId);
        writer.WriteUInt(VictimNetId);
        writer.WriteFloat(HitX);
        writer.WriteFloat(HitY);
        writer.WriteFloat(HitZ);
        writer.WriteFloat(Damage);
    }

    public override void Deserialize(PacketReader reader)
    {
        ShooterNetId = reader.ReadUInt();
        VictimNetId = reader.ReadUInt();
        HitX = reader.ReadFloat();
        HitY = reader.ReadFloat();
        HitZ = reader.ReadFloat();
        Damage = reader.ReadFloat();
    }
}
