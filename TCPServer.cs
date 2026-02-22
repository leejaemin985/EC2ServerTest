using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace App
{
    public class TCPServer
    {
        private static readonly ConcurrentDictionary<int, Socket> clients = new();
        private static int nextClientId = 0;

        private static CancellationTokenSource cts;

        public static async Task StartServerAsync(int port)
        {
            Socket listener = null;
            if (cts != null)
            {
                cts.Cancel();
            }
            cts = new();

            try
            {
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var endPoint = new IPEndPoint(IPAddress.Any, port);
                listener.Bind(endPoint);
                listener.Listen(100);

                while (!cts.IsCancellationRequested)
                {
                    Socket clientSocket = await listener.AcceptAsync();
                    clientSocket.NoDelay = true;

                    int clientId = Interlocked.Increment(ref nextClientId);
                    clients[clientId] = clientSocket;
                    Console.WriteLine($"클라이언트 연결됨. ID: {clientId} ({clientSocket.RemoteEndPoint})");

                    _ = Task.Run(() => HandleClientAsync(clientId, cts.Token));

                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("서버 종료 (Operation Cancel Exception)");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] {e}");
            }
            finally
            {
                try { listener?.Close(); } catch { }
                foreach (var kv in clients.Values)
                {
                    SafeClose(kv);
                }

                Console.WriteLine("서버를 종료합니다.");
            }
        }

        private static void SafeClose(Socket s)
        {
            if (s == null) return;
            try { s.Shutdown(SocketShutdown.Both); } catch { }
            try { s.Close(); } catch { }
        }

        private static async Task HandleClientAsync(int clientId, CancellationToken token)
        {
            if (!clients.TryGetValue(clientId, out Socket clientSocket))
                return;
            byte[] lengthBuf = new byte[4];

            try
            {
                while (!token.IsCancellationRequested)
                {
                    int read = await ReadExactAsync(clientSocket, lengthBuf, 4, token);
                    if (read == 0) break;

                    int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuf);
                    if (payloadLength <= 0 || payloadLength > 1024 * 1024)
                    {
                        throw new InvalidOperationException($"Invalid payload length: {payloadLength}");
                    }

                    byte[] payload = new byte[payloadLength];
                    read = await ReadExactAsync(clientSocket, payload, payloadLength, token);
                    if (read == 0) break;

                    string msg = Encoding.UTF8.GetString(payload);
                    Console.WriteLine($"[RECV] {clientId}: {msg}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] {e.Message}");
            }

        }

        private static async Task SendAllAsync(Socket socket, byte[] buffer, CancellationToken token)
        {
            int sent = 0;
            while (sent < buffer.Length)
            {
                int n = socket.SendAsync(
                    new ArraySegment<byte>(buffer, sent, buffer - sent),
                    SocketFlags.None,
                    token);

                if (n == 0) return 0;
                sent += n;
            }
        }

        private static async Task BroadcastAsync(int senderId, byte[] payload, CancellationToken token, bool includeSender)
        {
            byte[] header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);

            foreach (var kv in clients)
            {
                int targetId = kv.Key;
                if (!includeSender && targetId == senderId)
                    continue;

                var target = kv.Value;
                try
                {
                    await target.SendLock.WaitAsync(token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[Error] {e.Message}");
                    if (clients.TryRemove(targetId, out var removed))
                    {
                        SafeClose(removed);
                        Console.WriteLine($"브로드 캐스트 중 제거됨. ({targetId})")
                    }
                }
                finally
                {
                    //try { target.SendLock.Realse(); } catch { }
                }
            }
        }

        private static async Task<int> ReadExactAsync(Socket socket, byte[] buffer, int size, CancellationToken token)
        {
            int totalRead = 0;

            while (totalRead < size)
            {
                int read = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer, totalRead, size - totalRead),
                    SocketFlags.None,
                    token);

                if (read == 0) return 0;
                totalRead += read;
            }

            return totalRead;
        }

    }
}