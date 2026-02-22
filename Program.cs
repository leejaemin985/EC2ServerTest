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
                    await TCPServer.StartServerAsync(7777);
                }
                else
                {
                    //await TCPClient.StartClient("43.200.178.250", 7777);
                    await TCPClient.StartClient("127.0.0.1", 7777);
                }
            }
            else
            {
                return;
            }
        }
    }
}