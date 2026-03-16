public class Program
{
    public static async Task Main(string[] args)
    {
        int tcpPort = 7777;
        int udpPort = 8888;

        Console.WriteLine("=== 유니티 실시간 위치 동기화 서버 ===");
        Console.WriteLine($"TCP 포트: {tcpPort} | UDP 포트: {udpPort}");
        Console.WriteLine();

        var server = new GameServer(tcpPort, udpPort);

        // Ctrl+C 또는 'q' 입력으로 종료
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            server.Stop();
        };

        var serverTask = server.StartAsync();

        // 콘솔 입력 대기 (별도 스레드)
        _ = Task.Run(() =>
        {
            while (true)
            {
                var key = Console.ReadLine();
                if (key == null)
                {
                    // stdin이 닫힌 경우 (백그라운드 실행) — 대기
                    Thread.Sleep(Timeout.Infinite);
                    return;
                }
                if (key.ToLower() == "q")
                {
                    server.Stop();
                    break;
                }
            }
        });

        try
        {
            await serverTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"서버 오류: {ex.Message}");
        }
    }
}
