
namespace Packet.Session
{
    public sealed class ChatPacket : SessionPacket
    {
        public override SessionPacketType sessionPacketType => SessionPacketType.CHAT;

        public string message { get; private set; }

        public ChatPacket()
        {
            message = String.Empty;
        }

        public ChatPacket(string message)
        {
            this.message = message;
        }

        public override void WritePayload(BinaryWriter writer)
        {
            writer.Write(message);
        }

        public override void ReadPayload(BinaryReader reader)
        {
            message = reader.ReadString();
        }
    }
}