
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Buffers.Binary;

namespace App
{
    public class TCPClient
    {
        private static CancellationTokenSource connectionCts;

        public static async Task StartClient(string serverIp, int port)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            connectionCts = new();

            try
            {
                await socket.ConnectAsync(IPAddress.Parse(serverIp), port);
                socket.NoDelay = true;

                Console.WriteLine($"서버 연결됨 ({serverIp} : {port})");

                CancellationToken cts = connectionCts.Token;
                var sendTask = SendLoop(socket, cts);
                var receiveTask = ReceiveLoop(socket, cts);
                await Task.WhenAny(sendTask, receiveTask);
                connectionCts.Cancel();
                await Task.WhenAll(sendTask, receiveTask);

            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] {e.Message}");
            }
            finally
            {

                try { socket?.Shutdown(SocketShutdown.Both); } catch { }
                socket?.Close();
            }

        }

        private static async Task SendLoop(Socket socket, CancellationToken cts)
        {
            try
            {
                while (cts.IsCancellationRequested == false)
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
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] {e}");
            }
        }

        private static async Task ReceiveLoop(Socket socket, CancellationToken cts)
        {
            try
            {
                byte[] lengthBuf = new byte[4];
                while (cts.IsCancellationRequested == false)
                {
                    int byteRead = await ReadExactAsync(socket, lengthBuf, 4);
                    if (byteRead == 0) break;

                    int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuf);
                    if (payloadLength <= 0 || payloadLength > 1024 * 1024)
                    {
                        throw new InvalidOperationException($"Invalid payload Length (length: {payloadLength})");
                    }

                    byte[] payload = new byte[payloadLength];
                    byteRead = await ReadExactAsync(socket, payload, payloadLength);
                    if (byteRead == 0) break;

                    string msg = Encoding.UTF8.GetString(payload);
                    Console.WriteLine($"[RECV] {msg}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] {e}");
            }

        }

        private static async Task<int> ReadExactAsync(Socket socket, byte[] buffer, int size)
        {
            int totalRead = 0;
            while (totalRead < size)
            {
                int read = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer, totalRead, size - totalRead),
                    SocketFlags.None);

                if (read == 0) return 0;
                totalRead += read;
            }

            return totalRead;
        }
    }
}