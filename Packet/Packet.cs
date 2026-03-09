namespace Packet
{
    public abstract class Packet
    {
        public abstract ushort type { get; }

        public abstract void WritePayload(BinaryWriter writer);
        public abstract void ReadPayload(BinaryReader reader);

    }
}