using System.Buffers.Binary;
using System.Collections.Concurrent;
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

    // 틱 기반 게임 루프
    private const int TickRate = 30;
    private const float FixedDt = 1f / TickRate;
    private const float MoveSpeed = 5f;
    private int _currentTick;

    // 플레이어별 상태
    private readonly ConcurrentDictionary<int, float[]> _positions = new();
    private readonly ConcurrentDictionary<int, (int tick, float h, float v)> _latestInputs = new();

    public GameServer(int tcpPort = 7777, int udpPort = 7778)
    {
        _tcpPort = tcpPort;
        _udpPort = udpPort;
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();

        Console.WriteLine($"[서버] 시작 시도... TCP={_tcpPort}, UDP={_udpPort}");

        try
        {
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
            _udpServer = new UdpClient(_udpPort);
            Console.WriteLine($"[UDP] 서버 시작 성공 - 0.0.0.0:{_udpPort}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UDP] 서버 시작 실패: {ex.Message}");
            throw;
        }

        var tcpTask = AcceptTcpClientsAsync(_cts.Token);
        var udpTask = ReceiveUdpAsync(_cts.Token);
        var tickTask = GameTickLoop(_cts.Token);

        Console.WriteLine($"[서버] 가동 중 — {TickRate}Hz 틱 루프 시작");
        Console.WriteLine($"[서버] 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        await Task.WhenAll(tcpTask, udpTask, tickTask);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _tcpListener?.Stop();
        _udpServer?.Close();
        Console.WriteLine("서버 종료됨");
    }

    // ── 틱 기반 게임 루프 ──

    private async Task GameTickLoop(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long nextTickMs = 0;

        while (!ct.IsCancellationRequested)
        {
            long now = sw.ElapsedMilliseconds;
            if (now < nextTickMs)
            {
                await Task.Delay((int)(nextTickMs - now), ct);
            }
            nextTickMs = sw.ElapsedMilliseconds + (1000 / TickRate);

            _currentTick++;

            // 모든 플레이어의 최신 입력을 소비하여 위치 계산
            foreach (var kvp in _latestInputs)
            {
                int playerId = kvp.Key;
                var (tick, h, v) = kvp.Value;

                if (!_positions.ContainsKey(playerId))
                    _positions[playerId] = new float[] { 0, 0, 0 };

                var pos = _positions[playerId];
                pos[0] += h * MoveSpeed * FixedDt; // X
                pos[2] += v * MoveSpeed * FixedDt; // Z
            }

            // 모든 플레이어 위치를 브로드캐스트 (본인 포함)
            foreach (var kvp in _positions)
            {
                int playerId = kvp.Key;
                var pos = kvp.Value;

                // 해당 플레이어의 마지막 입력 틱을 응답에 포함
                int lastInputTick = _latestInputs.TryGetValue(playerId, out var input) ? input.tick : 0;

                var packet = PacketSerializer.WritePositionServer(playerId, lastInputTick, pos[0], pos[1], pos[2]);
                _clients.BroadcastUdp(_udpServer!, packet, excludeId: -1);
            }
        }
    }

    // ── TCP 접속 ──

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

                await _clients.SendTcpAsync(playerId, PacketSerializer.WriteConnected(playerId));

                await _clients.BroadcastTcpAsync(
                    PacketSerializer.WritePlayerJoined(playerId), excludeId: playerId);

                foreach (var existing in _clients.GetAllClients())
                {
                    if (existing.PlayerId != playerId)
                    {
                        await _clients.SendTcpAsync(playerId,
                            PacketSerializer.WritePlayerJoined(existing.PlayerId));
                    }
                }

                _ = HandleTcpClientAsync(playerId, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP] 접속 수락 오류: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private async Task HandleTcpClientAsync(int playerId, CancellationToken ct)
    {
        var client = _clients.GetClient(playerId);
        if (client == null) return;

        var buffer = new byte[1024];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int bytesRead = await ReadExactAsync(client.Stream, buffer, 0, 4, ct);
                if (bytesRead == 0) break;

                int packetLength = BinaryPrimitives.ReadInt32LittleEndian(buffer);
                if (packetLength <= 0 || packetLength > 1024) break;

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

        Console.WriteLine($"[TCP] 플레이어 {playerId} 접속 해제 ({DateTime.Now:HH:mm:ss})");
        _clients.RemoveClient(playerId);
        _positions.TryRemove(playerId, out _);
        _latestInputs.TryRemove(playerId, out _);
        await _clients.BroadcastTcpAsync(PacketSerializer.WritePlayerLeft(playerId));
    }

    private void HandleTcpPacket(int playerId, PacketType type, ReadOnlySpan<byte> data)
    {
        switch (type)
        {
            case PacketType.UdpRegister:
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

    // ── UDP 수신 — 입력을 버퍼에 저장만 함 ──

    private async Task ReceiveUdpAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udpServer!.ReceiveAsync(ct);
                var data = result.Buffer;
                if (data.Length < 1) continue;

                var packetType = (PacketType)data[0];

                if (packetType == PacketType.Input && data.Length >= 17)
                {
                    var (playerId, tick, h, v) = PacketSerializer.ReadInput(data);

                    // UDP 엔드포인트 자동등록
                    var client = _clients.GetClient(playerId);
                    if (client != null && client.UdpEndPoint == null)
                    {
                        client.UdpEndPoint = result.RemoteEndPoint;
                        Console.WriteLine($"[UDP] 플레이어 {playerId} UDP 자동등록: {result.RemoteEndPoint}");
                    }

                    // 입력 버퍼에 저장 (틱 루프에서 소비)
                    _latestInputs[playerId] = (tick, h, v);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[UDP] 수신 오류: {ex.Message}");
            }
        }
    }

    private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead), ct);
            if (read == 0) return 0;
            totalRead += read;
        }
        return totalRead;
    }
}
