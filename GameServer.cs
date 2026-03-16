using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

public class GameServer
{
    private readonly int _tcpPort;
    private readonly int _udpPort;
    private readonly ClientManager _clients = new();
    private TcpListener? _tcpListener;
    private UdpClient? _udpServer;
    private CancellationTokenSource? _cts;
    private readonly System.Diagnostics.Stopwatch _serverClock = new();

    // 서버 권한 이동: 플레이어별 위치 저장
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, float[]> _positions = new();
    private const float MoveSpeed = 5f;

    public GameServer(int tcpPort = 7777, int udpPort = 7778)
    {
        _tcpPort = tcpPort;
        _udpPort = udpPort;
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _serverClock.Start();

        Console.WriteLine($"[서버] 시작 시도... TCP={_tcpPort}, UDP={_udpPort}");

        try
        {
            // TCP 서버 시작
            _tcpListener = new TcpListener(IPAddress.Any, _tcpPort);
            _tcpListener.Start();
            Console.WriteLine($"[TCP] 서버 시작 성공 - 0.0.0.0:{_tcpPort}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TCP] 서버 시작 실패: {ex.Message}");
            throw;
        }

        try
        {
            // UDP 서버 시작
            _udpServer = new UdpClient(_udpPort);
            Console.WriteLine($"[UDP] 서버 시작 성공 - 0.0.0.0:{_udpPort}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UDP] 서버 시작 실패: {ex.Message}");
            throw;
        }

        // TCP 접속 수락 + UDP 수신을 동시에 실행
        var tcpTask = AcceptTcpClientsAsync(_cts.Token);
        var udpTask = ReceiveUdpAsync(_cts.Token);

        Console.WriteLine($"[서버] 가동 중 — TCP/UDP 수신 대기 중...");
        Console.WriteLine($"[서버] 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        await Task.WhenAll(tcpTask, udpTask);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _tcpListener?.Stop();
        _udpServer?.Close();
        Console.WriteLine("서버 종료됨");
    }

    /// <summary>TCP 클라이언트 접속 수락 루프</summary>
    private async Task AcceptTcpClientsAsync(CancellationToken ct)
    {
        Console.WriteLine("[TCP] 클라이언트 접속 대기 시작...");
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _tcpListener!.AcceptTcpClientAsync(ct);
                var remoteEp = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                Console.WriteLine($"[TCP] 새 연결 수신! from {remoteEp} ({DateTime.Now:HH:mm:ss})");

                int playerId = _clients.AddClient(tcpClient);
                Console.WriteLine($"[TCP] 플레이어 {playerId} 등록 완료 (현재 접속: {_clients.Count}명)");

                // 접속한 클라이언트에게 ID 알려주기
                await _clients.SendTcpAsync(playerId, PacketSerializer.WriteConnected(playerId));

                // 기존 플레이어들에게 새 플레이어 알림
                await _clients.BroadcastTcpAsync(
                    PacketSerializer.WritePlayerJoined(playerId), excludeId: playerId);

                // 새 클라이언트에게 기존 플레이어들 알림
                foreach (var existing in _clients.GetAllClients())
                {
                    if (existing.PlayerId != playerId)
                    {
                        await _clients.SendTcpAsync(playerId,
                            PacketSerializer.WritePlayerJoined(existing.PlayerId));
                    }
                }

                // 이 클라이언트의 TCP 수신을 별도 태스크로 처리
                _ = HandleTcpClientAsync(playerId, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP] 접속 수락 오류: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[TCP] StackTrace: {ex.StackTrace}");
            }
        }
    }

    /// <summary>개별 TCP 클라이언트 수신 처리</summary>
    private async Task HandleTcpClientAsync(int playerId, CancellationToken ct)
    {
        var client = _clients.GetClient(playerId);
        if (client == null) return;

        var buffer = new byte[1024];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // 길이 프리픽스(4바이트) 읽기
                int bytesRead = await ReadExactAsync(client.Stream, buffer, 0, 4, ct);
                if (bytesRead == 0) break;

                int packetLength = BinaryPrimitives.ReadInt32LittleEndian(buffer);
                if (packetLength <= 0 || packetLength > 1024) break;

                // 페이로드 읽기
                bytesRead = await ReadExactAsync(client.Stream, buffer, 0, packetLength, ct);
                if (bytesRead == 0) break;

                var packetType = PacketSerializer.ReadTcpPacketType(buffer);
                HandleTcpPacket(playerId, packetType, buffer.AsSpan(0, packetLength));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"[TCP] 플레이어 {playerId} 수신 오류: {ex.GetType().Name}: {ex.Message}");
        }

        // 연결 해제 처리
        Console.WriteLine($"[TCP] 플레이어 {playerId} 접속 해제 ({DateTime.Now:HH:mm:ss})");
        _clients.RemoveClient(playerId);
        await _clients.BroadcastTcpAsync(PacketSerializer.WritePlayerLeft(playerId));
    }

    private void HandleTcpPacket(int playerId, PacketType type, ReadOnlySpan<byte> data)
    {
        switch (type)
        {
            case PacketType.UdpRegister:
                // 클라이언트가 UDP 포트를 알려줌: [Type(1)][Port(4)]
                int udpPort = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(1));
                var client = _clients.GetClient(playerId);
                if (client != null)
                {
                    var tcpEp = client.TcpClient.Client.RemoteEndPoint as IPEndPoint;
                    client.UdpEndPoint = new IPEndPoint(tcpEp!.Address, udpPort);
                    Console.WriteLine($"[UDP] 플레이어 {playerId} UDP 등록: {client.UdpEndPoint}");
                }
                break;
        }
    }

    /// <summary>UDP 수신 루프 — 입력 받아서 서버에서 이동 처리 후 결과 브로드캐스트</summary>
    private async Task ReceiveUdpAsync(CancellationToken ct)
    {
        long lastTick = _serverClock.ElapsedMilliseconds;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udpServer!.ReceiveAsync(ct);
                var data = result.Buffer;
                if (data.Length < 1) continue;

                var packetType = (PacketType)data[0];

                if (packetType == PacketType.Input && data.Length >= 13)
                {
                    var (playerId, h, v) = PacketSerializer.ReadInput(data);

                    // UDP 엔드포인트 자동등록
                    var client = _clients.GetClient(playerId);
                    if (client != null && client.UdpEndPoint == null)
                    {
                        client.UdpEndPoint = result.RemoteEndPoint;
                        Console.WriteLine($"[UDP] 플레이어 {playerId} UDP 자동등록: {result.RemoteEndPoint}");
                    }

                    // 서버에서 이동 처리
                    long now = _serverClock.ElapsedMilliseconds;
                    float dt = (now - lastTick) / 1000f;
                    if (dt > 0.1f) dt = 0.1f; // 최대 100ms
                    lastTick = now;

                    if (!_positions.ContainsKey(playerId))
                        _positions[playerId] = new float[] { 0, 0, 0 };

                    var pos = _positions[playerId];
                    pos[0] += h * MoveSpeed * dt; // X
                    pos[2] += v * MoveSpeed * dt; // Z

                    // 결과를 모든 클라이언트에게 전송 (본인 포함!)
                    int serverTimeMs = (int)now;
                    var outPacket = PacketSerializer.WritePositionServer(playerId, serverTimeMs, pos[0], pos[1], pos[2]);
                    _clients.BroadcastUdp(_udpServer, outPacket, excludeId: -1);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[UDP] 수신 오류: {ex.Message}");
            }
        }
    }

    /// <summary>스트림에서 정확히 count 바이트 읽기</summary>
    private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead), ct);
            if (read == 0) return 0; // 연결 끊김
            totalRead += read;
        }
        return totalRead;
    }
}
