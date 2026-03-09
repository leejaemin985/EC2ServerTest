namespace Packet.Session
{
    public enum SessionPacketType : ushort
    {
        JOIN = 1,
        CHAT = 2,
        LEAVEROOM = 3
    }

    public abstract class SessionPacket : Packet
    {
        public abstract SessionPacketType sessionPacketType { get; }
        public override ushort type => (ushort)sessionPacketType;
    }
}