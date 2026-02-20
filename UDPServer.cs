using System.Net;
using System.Net.Sockets;
using System.Text;

namespace App
{
    public class UDPServer
    {
        public static void StartServer(int port)
        {
            Socket udpSocket = null;
            try
            {
                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
                udpSocket.Bind(localEndPoint);
                Console.WriteLine($"UDP 수신 시작. 포트 {port}에서 데이터 대기 중");

                byte[] buffer = new byte[1024];
                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                while (true)
                {
                    int bytesRead = udpSocket.ReceiveFrom(buffer, ref remoteEndPoint);
                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    if (receivedMessage.Equals($"-1"))
                    {
                        Console.WriteLine("송신자 종료");
                        break;
                    }

                    Console.WriteLine($"데이터 수신 ({remoteEndPoint}): {receivedMessage}");

                    udpSocket.SendTo(buffer, remoteEndPoint);
                    Console.WriteLine("데이터 다시 송신");
                }

            }
            catch (SocketException se)
            {
                Console.WriteLine($"SocketException: {se.Message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}");
            }
            finally
            {
                udpSocket?.Close();
                Console.WriteLine("UDP 수신자 종료");
            }

        }
    }
}