using Packet.Session;

namespace Packet
{
    public static class PacketFactory
    {

        private static readonly Dictionary<ushort, Func<Packet>> creators = new()
        {
            { (ushort)SessionPacketType.CHAT, () => new ChatPacket() },
        };

        public static Packet Create(ushort type)
        {
            if (creators.TryGetValue(type, out Func<Packet>? creator))
            {
                return creator();
            }

            throw new InvalidOperationException($"Unknown packet type: {type}");
        }
    }
}