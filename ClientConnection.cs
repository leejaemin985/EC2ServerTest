using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace App
{
    public sealed class ClientConnection : IDisposable
    {
        public readonly Guid id;
        public readonly Socket clientSocket;
        public readonly string RemoteEndPoint;

        private readonly SemaphoreSlim sendLock = new(1, 1);

        public ClientConnection(Socket socket, Guid id)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));

            this.id = id;
            this.clientSocket = socket;
            RemoteEndPoint = socket.RemoteEndPoint?.ToString() ?? "unknown";
            socket.NoDelay = true;
        }

        public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            await sendLock.WaitAsync(ct);

            try
            {
                int length = data.Length;
                byte[] buffer = new byte[4 + length];

                System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(0, 4), length);
                data.CopyTo(buffer.AsMemory(4));

                int sentTotal = 0;
                while (sentTotal < buffer.Length)
                {
                    int sent = await clientSocket.SendAsync(buffer.AsMemory(sentTotal), SocketFlags.None, ct);
                    if (sent <= 0) throw new SocketException((int)SocketError.ConnectionReset);
                    sentTotal += sent;
                }
            }
            finally
            {
                sendLock.Release();
            }
        }

        public void Dispose()
        {
            try { clientSocket.Shutdown(SocketShutdown.Both); } catch { }
            try { clientSocket.Close(); } catch { }
            sendLock.Dispose();
        }


    }
}
