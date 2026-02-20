using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace App
{
    public class UDPClient
    {
        public static void StartClient(string remoteIp, int remotePort)
        {
            Socket udpSocket = null;
            Stopwatch sw = new Stopwatch();
            try
            {
                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                IPAddress iPAddress = IPAddress.Parse(remoteIp);
                IPEndPoint remoteEndPoint = new IPEndPoint(iPAddress, remotePort);
                EndPoint serverEndPoint = new IPEndPoint(IPAddress.Any, 0);

                Console.WriteLine($"UDP 송신 시작. ({remoteEndPoint})");

                while (true)
                {
                    string input = Console.ReadLine();
                    byte[] inputByte = Encoding.UTF8.GetBytes(input);

                    sw.Start();
                    udpSocket.SendTo(inputByte, remoteEndPoint);

                    byte[] byteArr = new byte[1024];

                    if (input.Equals("-1"))
                    {
                        Console.WriteLine("송신자 종료");
                        break;
                    }

                    int byteRead = udpSocket.ReceiveFrom(byteArr, ref serverEndPoint);
                    sw.Stop();

                    string receivedMessage = Encoding.UTF8.GetString(byteArr, 0, byteRead);
                    Console.WriteLine($"데이터 수신 ({remoteEndPoint}): {receivedMessage} ({sw.ElapsedTicks} tick)");

                    sw.Reset();
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
                Console.WriteLine("UDP 송신자 종료");
            }

        }
    }
}