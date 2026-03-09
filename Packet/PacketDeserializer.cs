using System.Buffers.Binary;
using System.Text;

namespace Packet
{
    public static class PacketDeserializer
    {
        private const int LENGTH_SIZE = 4;
        private const int TYPE_SIZE = 2;

        private const int HEADER_SIZE = LENGTH_SIZE + TYPE_SIZE;

        public static Packet Deserialize(byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (buffer.Length < HEADER_SIZE)
            {
                throw new InvalidOperationException("Buffer is too small");
            }

            int bodyLength = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(0, LENGTH_SIZE));

            if (bodyLength < TYPE_SIZE)
            {
                throw new InvalidOperationException("Invalid body length");
            }

            if (buffer.Length < LENGTH_SIZE + bodyLength)
            {
                throw new InvalidOperationException("Incomplete packet");
            }

            ushort packetType = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(LENGTH_SIZE, TYPE_SIZE));

            int payloadLength = bodyLength - TYPE_SIZE;
            Packet packet = PacketFactory.Create(packetType);
            using (var payloadStream = new MemoryStream(
                buffer,
                HEADER_SIZE,
                payloadLength,
                writable: false))
            using (var reader = new BinaryReader(payloadStream, Encoding.UTF8, true))
            {
                packet.ReadPayload(reader);
            }

            return packet;
        }
    }
}