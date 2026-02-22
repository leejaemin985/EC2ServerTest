using System;

namespace App
{
    class Program
    {
        static async Task Main(string[] args)
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
                    await TCPClient.StartClient("43.200.178.250", 7777);
                }
            }
            else
            {
                return;
            }
        }
    }
}