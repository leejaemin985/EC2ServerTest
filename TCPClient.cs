using System.Data.SqlTypes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using System.Buffers.Binary;

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

            while (true)
            {
                string? line = Console.ReadLine();
                if (line == null) break;
                if (line.Equals("/quit", StringComparison.OrdinalIgnoreCase)) break;

                byte[] payload = Encoding.UTF8.GetBytes(line);

                byte[] packet = new byte[4 + payload.Length];
                BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(0, 4), payload.Length);
                payload.CopyTo(packet.AsSpan(4));

                await socket.SendAsync(packet, SocketFlags.None);
            }

            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch { }
            finally
            {
                socket?.Close();
            }

        }
    }
}