using System.Data.SqlTypes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;

namespace App
{
    public class TCPClient
    {
        public static async Task StartClient(string serverIp, int port)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            await socket.ConnectAsync(IPAddress.Parse(serverIp), port);
            socket.NoDelay = true;

            Console.WriteLine($"서버 연결됨 ({serverIp} : {port})");



        }
    }
}