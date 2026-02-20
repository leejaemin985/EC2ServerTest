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

            const string ipAddress = "43.200.178.250";
            const int PORT_TCP = 7777;
            const int PORT_UDP = 8888;

            if (isTcp)
            {
                if (isServer)
                {
                    Console.WriteLine("Start TCP Server");
                    TCPServer.StartServer(PORT_TCP);
                }
                else
                {
                    Console.WriteLine("Start TCP Client");
                    TCPClient.StartClient(ipAddress, PORT_TCP);
                }
            }
            else
            {
                if (isServer)
                {
                    Console.WriteLine("Start UDP Server");
                    UDPServer.StartServer(PORT_UDP);
                }
                else
                {
                    Console.WriteLine("Start UDP Client");
                    UDPClient.StartClient(ipAddress, PORT_UDP);
                }
            }

        }
    }
}