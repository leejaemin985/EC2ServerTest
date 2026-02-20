using System;
using System.Net.Sockets;

namespace App
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args == null || args.Length < 2)
            {
                Console.WriteLine("Invalid Command");
                return;
            }

            bool isTcp = args[0].Equals("tcp");
            bool isServer = args[1].Equals("s");

            const string ipAddress = "127.0.0.1";
            const int port = 7777;

            if (isTcp)
            {
                if (isServer)
                {
                    Console.WriteLine("Start TCP Server");
                    TCPServer.StartServer(port);
                }
                else
                {
                    Console.WriteLine("Start TCP Client");
                    TCPClient.StartClient(ipAddress, port);
                }
            }
            else
            {
                if (isServer)
                {
                    Console.WriteLine("Start UDP Server");
                    UDPServer.StartServer(port);
                }
                else
                {
                    Console.WriteLine("Start UDP Client");
                    UDPClient.StartClient(ipAddress, port);
                }
            }

        }
    }
}