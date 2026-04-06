using LJMCollision;

/// <summary>
/// 서버 → 클라이언트: 전체 오브젝트 Transform 스냅샷 (배치).
/// [Count(2)][NetId(4) PosX(4) PosY(4) PosZ(4) RotX(4) RotY(4) RotZ(4) RotW(4)] × Count
/// </summary>
public class TransformPacket : Packet
{
    public override PacketType Type => PacketType.Transform;
    public override PacketChannel Channel => PacketChannel.Udp;

    public struct Entry
    {
        public uint NetId;
        public Vec3 Position;
        public Quat Rotation;
    }

    readonly List<Entry> _entries = new();

    public int Count => _entries.Count;

    public void Add(uint netId, Vec3 position, Quat rotation)
    {
        _entries.Add(new Entry { NetId = netId, Position = position, Rotation = rotation });
    }

    public void Clear() => _entries.Clear();

    public override void Serialize(PacketWriter writer)
    {
        writer.WriteUShort((ushort)_entries.Count);
        foreach (var e in _entries)
        {
            writer.WriteUInt(e.NetId);
            writer.WriteFloat(e.Position.X);
            writer.WriteFloat(e.Position.Y);
            writer.WriteFloat(e.Position.Z);
            writer.WriteFloat(e.Rotation.X);
            writer.WriteFloat(e.Rotation.Y);
            writer.WriteFloat(e.Rotation.Z);
            writer.WriteFloat(e.Rotation.W);
        }
    }

    public override void Deserialize(PacketReader reader)
    {
        _entries.Clear();
        ushort count = reader.ReadUShort();
        for (int i = 0; i < count; i++)
        {
            _entries.Add(new Entry
            {
                NetId = reader.ReadUInt(),
                Position = new Vec3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat()),
                Rotation = new Quat(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat()),
            });
        }
    }
}
