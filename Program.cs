using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

// ── 설정 ──

const int TcpPort = 7777;
const int UdpPort = 8888;
const int TickRate = 30;
const int HeaderSize = 8;

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// ── 네트워크 ──

var udpServer = new UdpClient(UdpPort);
var tcpListener = new TcpListener(IPAddress.Any, TcpPort);
tcpListener.Start();

Console.WriteLine($"[TCP] Listening on port {TcpPort}");
Console.WriteLine($"[UDP] Listening on port {UdpPort}");

// ── 룸 (우선 하나) ──

var room = new Room(1, TickRate, udpServer, "Maps/map.json");

// ── 태스크 시작 ──

var gameLoopTask = room.GameLoop.RunAsync(cts.Token);
var tcpAcceptTask = AcceptTcpClientsAsync(cts.Token);
var udpReceiveTask = ReceiveUdpAsync(cts.Token);

Console.WriteLine($"[Server] Running at {TickRate} tick/s. Press Ctrl+C to stop.");

try
{
    await Task.WhenAll(gameLoopTask, tcpAcceptTask, udpReceiveTask);
}
catch (OperationCanceledException) { }
finally
{
    tcpListener.Stop();
    udpServer.Close();
    Console.WriteLine("[Server] Shutdown complete.");
}

// ═══════════════════════════════════════════════════════════════
//  TCP Accept
// ═══════════════════════════════════════════════════════════════

async Task AcceptTcpClientsAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        TcpClient tcpClient;
        try { tcpClient = await tcpListener.AcceptTcpClientAsync(ct); }
        catch (OperationCanceledException) { break; }

        var session = room.SessionManager.CreateSession(tcpClient);
        Console.WriteLine($"[TCP] Player {session.PlayerId} connected from {tcpClient.Client.RemoteEndPoint}");

        await room.PlayerJoinAsync(session);

        _ = HandleTcpClientAsync(session, ct);
    }
}

// ═══════════════════════════════════════════════════════════════
//  TCP 클라이언트별 수신
// ═══════════════════════════════════════════════════════════════

async Task HandleTcpClientAsync(Session session, CancellationToken ct)
{
    var stream = session.Stream;
    var headerBuf = new byte[HeaderSize];

    try
    {
        while (!ct.IsCancellationRequested)
        {
            if (!await ReadExactAsync(stream, headerBuf, 0, HeaderSize, ct))
                break;

            ushort payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(headerBuf);
            ushort packetType = BinaryPrimitives.ReadUInt16LittleEndian(headerBuf.AsSpan(2));
            int tick = BinaryPrimitives.ReadInt32LittleEndian(headerBuf.AsSpan(4));

            byte[] payload = Array.Empty<byte>();
            if (payloadLength > 0)
            {
                payload = new byte[payloadLength];
                if (!await ReadExactAsync(stream, payload, 0, payloadLength, ct))
                    break;
            }

            room.HandlePacket(session, (PacketType)packetType, tick, payload);
        }
    }
    catch (Exception ex) when (ex is IOException or ObjectDisposedException)
    {
    }

    room.PlayerLeave(session);
}

// ═══════════════════════════════════════════════════════════════
//  UDP 수신
// ═══════════════════════════════════════════════════════════════

async Task ReceiveUdpAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        UdpReceiveResult result;
        try { result = await udpServer.ReceiveAsync(ct); }
        catch (OperationCanceledException) { break; }

        var data = result.Buffer;
        if (data.Length < HeaderSize) continue;

        ushort packetType = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2));
        int tick = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4));

        byte[] payload = data.Length > HeaderSize
            ? data[HeaderSize..]
            : Array.Empty<byte>();

        var session = room.SessionManager.FindByEndPoint(result.RemoteEndPoint);
        if (session == null)
        {
            if ((PacketType)packetType == PacketType.Connected && payload.Length >= 4)
            {
                int playerId = BinaryPrimitives.ReadInt32LittleEndian(payload);
                session = room.SessionManager.FindByPlayerId(playerId);
                if (session != null)
                {
                    room.SessionManager.RegisterUdpEndPoint(session, result.RemoteEndPoint);
                    Console.WriteLine($"[UDP] Player {playerId} mapped to {result.RemoteEndPoint}");
                }
            }
            continue;
        }

        room.HandlePacket(session, (PacketType)packetType, tick, payload);
    }
}

// ═══════════════════════════════════════════════════════════════
//  유틸리티
// ═══════════════════════════════════════════════════════════════

static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken ct)
{
    int read = 0;
    while (read < count)
    {
        int n = await stream.ReadAsync(buffer.AsMemory(offset + read, count - read), ct);
        if (n == 0) return false;
        read += n;
    }
    return true;
}
