using System.Data.SqlTypes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace App
{
    public class TCPClient
    {
        public static async Task StartClient(string serverIp, int serverPort)
        {
            Socket clientSocket = null;
            Stopwatch sw = new Stopwatch();
            try
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                IPAddress ipAddress = IPAddress.Parse(serverIp);
                IPEndPoint remoteEndPoint = new IPEndPoint(ipAddress, serverPort);

                Console.WriteLine($"서버 {remoteEndPoint}에 연결 시도");
                clientSocket.Connect(remoteEndPoint);
                Console.WriteLine($"서버에 연결됨: {clientSocket.RemoteEndPoint}");

                while (true)
                {
                    string input = Console.ReadLine();
                    if (input.Equals("-1")) break;

                    if (string.IsNullOrEmpty(input)) continue;

                    sw.Start();
                    input += "<EOF>";
                    byte[] data = Encoding.UTF8.GetBytes(input);
                    int byteSend = clientSocket.Send(data);

                    byte[] buffer = new byte[1024];
                    int bytesRead = clientSocket.Receive(buffer);

                    sw.Stop();
                    string responseMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"서버로부터 응답 수신: {responseMessage} (RTT: {sw.ElapsedMilliseconds} ms)");

                    sw.Reset();
                }
            }
            catch(ArgumentException ane)
            {
                Console.WriteLine($"Argument Exception: {ane.Message}");
            }
            catch (SocketException se)
            {
                Console.WriteLine($"Socket Exception: {se.Message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}");
            }
            finally
            {
                clientSocket?.Shutdown(SocketShutdown.Both);
                clientSocket?.Close();
                Console.WriteLine($"클라이언트 소켓 닫힘");
            }
        }
    }
}