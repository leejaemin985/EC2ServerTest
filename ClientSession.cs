using System.Net.Sockets;

namespace App
{
    public sealed class ClientSession : IDisposable
    {
        public readonly Guid id;
        public readonly Socket clientSocket;
        public readonly string remoteEndPoint;

        public readonly string nickName;

        public ClientSession(Socket socket, Guid guid, string nickName)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));

            this.id = guid;
            this.clientSocket = socket;
            remoteEndPoint = socket.RemoteEndPoint?.ToString() ?? "unknown";
            socket.NoDelay = true;
            this.nickName = nickName;
        }



        public void Dispose()
        {

        }
    }
}