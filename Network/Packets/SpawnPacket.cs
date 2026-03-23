/// <summary>
/// 서버 → 클라이언트: 오브젝트 생성 통보.
/// [NetId(4)][ObjectType(2)][OwnerId(4)][PosX(4)][PosY(4)][PosZ(4)][RotX(4)][RotY(4)][RotZ(4)][RotW(4)]
/// </summary>
public class SpawnPacket : Packet
{
    public override PacketType Type => PacketType.Spawn;
    public override PacketChannel Channel => PacketChannel.Tcp;

    public uint NetId;
    public ushort ObjectType;
    public int OwnerId;
    public float PosX, PosY, PosZ;
    public float RotX, RotY, RotZ, RotW;

    public override void Serialize(PacketWriter writer)
    {
        writer.WriteUInt(NetId);
        writer.WriteUShort(ObjectType);
        writer.WriteInt(OwnerId);
        writer.WriteFloat(PosX);
        writer.WriteFloat(PosY);
        writer.WriteFloat(PosZ);
        writer.WriteFloat(RotX);
        writer.WriteFloat(RotY);
        writer.WriteFloat(RotZ);
        writer.WriteFloat(RotW);
    }

    public override void Deserialize(PacketReader reader)
    {
        NetId = reader.ReadUInt();
        ObjectType = reader.ReadUShort();
        OwnerId = reader.ReadInt();
        PosX = reader.ReadFloat();
        PosY = reader.ReadFloat();
        PosZ = reader.ReadFloat();
        RotX = reader.ReadFloat();
        RotY = reader.ReadFloat();
        RotZ = reader.ReadFloat();
        RotW = reader.ReadFloat();
    }
}
