using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace App
{
    public class TCPServer
    {
        public static void StartServer(int port)
        {
            Socket listener = null;

            try
            {
                //TCP 소켓 설정 (IPv4: Internetwork, TCP는 Stream방식을 사용함)
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                //Any: 모든 IP로부터 받음 설정.
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
                listener.Bind(localEndPoint);

                //접속 가능한 유저 수 설정
                listener.Listen(10);
                Console.WriteLine($"TCP 서버 시작 ({listener.RemoteEndPoint})");

                while (true)
                {
                    Socket clientSocket = listener.Accept();
                    clientSocket.NoDelay = true;

                    Console.WriteLine($"클라이언트 연결됨. ({clientSocket.RemoteEndPoint})");
                    _ = HandleClient(clientSocket);
                }


            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] {e.Message}");
            }
            finally
            {
                listener?.Close();
            }
        }

        private static async Task HandleClient(Socket clientSocket)
        {
            try
            {
                byte[] lengthBuf = new byte[4];
                while (true)
                {
                    int byteRead = await ReadExactAsync(clientSocket, lengthBuf, 4);
                    if (byteRead == 0) break;

                    //앞 LengthBuff 컨버팅하여 페이로드의 총 길이 구함
                    int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuf);

                    if (payloadLength <= 0 || payloadLength > 1024 * 1024)
                    {
                        throw new InvalidOperationException($"Invalid payload Length (length: {payloadLength})");
                    }

                    byte[] payload = new byte[payloadLength];
                    byteRead = await ReadExactAsync(clientSocket, payload, payloadLength);
                    if (byteRead == 0) break;

                    string msg = Encoding.UTF8.GetString(payload);
                    Console.WriteLine($"[RECV] {clientSocket.RemoteEndPoint} msg: {msg}");
                }

            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] {e.Message}");
            }
            finally
            {
                clientSocket?.Shutdown(SocketShutdown.Both);
                clientSocket?.Close();
            }
        }

        //소켓으로부터 size만큼 읽어들인후 buffer에 대입하고 저장된 길이를 반환.
        private static async Task<int> ReadExactAsync(Socket socket, byte[] buffer, int size)
        {
            int totalRead = 0;
            while (totalRead < size)
            {

                //ArraySegment => buffer를 분할하여 사용하기 위함. totalRead인덱스 부터 size-totalRead인덱스 만큼 잘라서 이곳에 receive함.
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