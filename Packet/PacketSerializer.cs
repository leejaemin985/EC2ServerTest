using System.Text;
using System.Buffers.Binary;

namespace Packet
{
    public static class PacketSerailizer
    {
        private const int LENGTH_SIZE = 4;
        private const int TYPE_SIZE = 2;

        public static byte[] Serialize(Packet packet)
        {
            if (packet == null) throw new ArgumentNullException(nameof(packet));

            byte[] payloadByte;
            using (var payloadStream = new MemoryStream())
            using (var payloadWriter = new BinaryWriter(payloadStream, Encoding.UTF8, true))
            {
                packet.WritePayload(payloadWriter);
                payloadWriter.Flush();
                payloadByte = payloadStream.ToArray();
            }

            int bodyLength = TYPE_SIZE + payloadByte.Length;
            byte[] buffer = new byte[LENGTH_SIZE + bodyLength];

            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(0, LENGTH_SIZE), bodyLength);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(LENGTH_SIZE, TYPE_SIZE), (ushort)packet.type);

            if (payloadByte.Length > 0)
            {
                payloadByte.CopyTo(buffer, LENGTH_SIZE + TYPE_SIZE);
            }

            return buffer;
        }
    }

}