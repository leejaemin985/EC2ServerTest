using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace App
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args == null || args.Length < 2)
            {
                Console.WriteLine("[Error] Invalid Command");
                return;
            }

            if (args[0].ToLower().Equals("tcp"))
            {
                if (args[1].ToLower().Equals("s"))
                {
                    TCPServer.StartServer(7777);
                }
                else
                {
                    TCPClient.StartClient("127.0.0.1", 7777);
                }
            }
            else
            {
                //Console.WriteLine("");
                return;
            }
        }
    }
}