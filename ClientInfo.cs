using System.Net;
using System.Net.Sockets;

namespace App
{
    public sealed class ClientInfo
    {
        public readonly Socket socket;
        public readonly SemaphoreSlim sendLock;

        public ClientInfo(Socket socket)
        {
            this.socket = socket;
            this.sendLock = new(1, 1);
        }
    }
}