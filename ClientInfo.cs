using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace App
{
    public sealed class ClientInfo
    {
        public readonly Socket socket;
        public readonly Semaphore semaphore;

        public ClientInfo(Socket socket)
        {
            this.socket = socket;
            this.semaphore = new(1, 1);
        }
    }
}