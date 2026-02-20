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
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
                listener.Bind(localEndPoint);

                listener.Listen(10);
                Console.WriteLine($"TCP 서버 시작. 포트 {port}에서 연결 대기");

                while (true)
                {
                    Console.WriteLine("클라이언트 연결 요청 대기");
                    Socket clientSocket = listener.Accept();
                    Console.WriteLine($"클라이언트 연결 수락: {clientSocket.RemoteEndPoint}");

                    HandleClient(clientSocket);
                }
            }
            catch (SocketException se)
            {
                Console.WriteLine($"소켓 예외 발생: {se.Message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"예외 발생: {e.Message}");
            }
            finally
            {
                listener?.Close();
                Console.WriteLine("TCP 서버 종료");
            }
        }

        private static void HandleClient(Socket clientSocket)
        {
            byte[] buffer = new byte[1024];
            int bytesRead;
            StringBuilder dataReceived = new StringBuilder();

            try
            {
                while (true)
                {
                    dataReceived.Clear();
                    bytesRead = 0;

                    while ((bytesRead = clientSocket.Receive(buffer)) > 0)
                    {
                        dataReceived.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                        if (dataReceived.ToString().IndexOf("<EOF>") > -1)
                        {
                            break;
                        }
                    }

                    if (bytesRead == 0)
                    {
                        Console.WriteLine($"클라이언트 연결 종료: {clientSocket.RemoteEndPoint}");
                        break;
                    }

                    string receivedMessage = dataReceived.ToString().Replace("<EOF>", "");
                    Console.WriteLine($"데이터 수신 ({clientSocket.RemoteEndPoint}): {receivedMessage}");

                    string responseMessage = $"서버가 메세지 수신 완료: {receivedMessage}";
                    byte[] responseBytes = Encoding.UTF8.GetBytes(responseMessage);
                    clientSocket.Send(responseBytes);
                    Console.WriteLine($"응답 전송 ({clientSocket.RemoteEndPoint}): {responseMessage}");
                }
            }
            catch (SocketException se)
            {
                Console.WriteLine($"SocketException handling client {clientSocket.RemoteEndPoint}: {se.ErrorCode} - {se.Message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"예외 발생 handling client {clientSocket.RemoteEndPoint}: {e.Message}");
            }
            finally
            {
                clientSocket.Shutdown(SocketShutdown.Both);

                clientSocket.Close();
                Console.WriteLine($"클라이언트 소켓 닫힘: {clientSocket.RemoteEndPoint}");
            }
        }

    }
}