using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

// ── 설정 ──

const int TcpPort = 7777;
const int UdpPort = 8888;
const int TickRate = 30;
const int HeaderSize = 8; // [2B: Length][2B: PacketType][4B: Tick]

// ── 핵심 인스턴스 ──

var sessionManager = new SessionManager();
var gameLoop = new GameLoop(TickRate);
var cts = new CancellationTokenSource();

// Ctrl+C 종료 처리
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// ── UDP 서버 ──

var udpServer = new UdpClient(UdpPort);
Console.WriteLine($"[UDP] Listening on port {UdpPort}");

// ── TCP 서버 ──

var tcpListener = new TcpListener(IPAddress.Any, TcpPort);
tcpListener.Start();
Console.WriteLine($"[TCP] Listening on port {TcpPort}");

// ── Transform 브로드캐스트 ──

gameLoop.OnPostTick = () =>
{
    // 이동 중인(또는 모든) 오브젝트의 위치를 UDP로 브로드캐스트
    var objects = gameLoop.FindAll<NetworkObject>();
    var writer = new PacketWriter();

    int count = 0;
    // count 자리 확보 (나중에 덮어씀)
    writer.WriteUShort(0);

    foreach (var obj in objects)
    {
        writer.WriteUInt(obj.NetId);
        writer.WriteFloat(obj.Position.X);
        writer.WriteFloat(obj.Position.Y);
        writer.WriteFloat(obj.Position.Z);
        writer.WriteFloat(obj.Rotation.X);
        writer.WriteFloat(obj.Rotation.Y);
        writer.WriteFloat(obj.Rotation.Z);
        writer.WriteFloat(obj.Rotation.W);
        count++;
    }

    if (count == 0) return;

    byte[] payload = writer.ToArray();
    // count를 페이로드 맨 앞에 기록
    BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)count);

    // 헤더 + 페이로드 조립
    var final = new byte[HeaderSize + payload.Length];
    BinaryPrimitives.WriteUInt16LittleEndian(final, (ushort)payload.Length);
    BinaryPrimitives.WriteUInt16LittleEndian(final.AsSpan(2), (ushort)PacketType.Transform);
    BinaryPrimitives.WriteInt32LittleEndian(final.AsSpan(4), gameLoop.CurrentTick);
    payload.CopyTo(final.AsSpan(HeaderSize));

    sessionManager.BroadcastUdp(udpServer, final);
};

// ── 태스크 시작 ──

var gameLoopTask = gameLoop.RunAsync(cts.Token);
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
//  TCP Accept Loop
// ═══════════════════════════════════════════════════════════════

async Task AcceptTcpClientsAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        TcpClient tcpClient;
        try { tcpClient = await tcpListener.AcceptTcpClientAsync(ct); }
        catch (OperationCanceledException) { break; }

        var session = sessionManager.CreateSession(tcpClient);
        Console.WriteLine($"[TCP] Player {session.PlayerId} connected from {tcpClient.Client.RemoteEndPoint}");

        // 플레이어 오브젝트 스폰
        var obj = gameLoop.Spawn<MovableObject>();
        obj.OwnerId = session.PlayerId;
        session.PlayerNetId = obj.NetId;

        // 기존 오브젝트들을 새 클라이언트에게 전송
        await SendExistingObjectsAsync(session);

        // 새 오브젝트 스폰을 모든 클라이언트에게 브로드캐스트
        await BroadcastSpawnAsync(obj);

        // 클라이언트 수신 루프 시작
        _ = HandleTcpClientAsync(session, ct);
    }
}

// ═══════════════════════════════════════════════════════════════
//  TCP 클라이언트별 수신 루프
// ═══════════════════════════════════════════════════════════════

async Task HandleTcpClientAsync(Session session, CancellationToken ct)
{
    var stream = session.Stream;
    var headerBuf = new byte[HeaderSize];

    try
    {
        while (!ct.IsCancellationRequested)
        {
            // 1) 헤더 읽기: [2B Length][2B PacketType][4B Tick]
            if (!await ReadExactAsync(stream, headerBuf, 0, HeaderSize, ct))
                break;

            ushort payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(headerBuf);
            ushort packetType = BinaryPrimitives.ReadUInt16LittleEndian(headerBuf.AsSpan(2));
            int tick = BinaryPrimitives.ReadInt32LittleEndian(headerBuf.AsSpan(4));

            // 2) 페이로드 읽기
            byte[] payload = Array.Empty<byte>();
            if (payloadLength > 0)
            {
                payload = new byte[payloadLength];
                if (!await ReadExactAsync(stream, payload, 0, payloadLength, ct))
                    break;
            }

            // 3) 패킷 디스패치
            HandlePacket(session, (PacketType)packetType, tick, payload);
        }
    }
    catch (Exception ex) when (ex is IOException or ObjectDisposedException)
    {
        // 연결 끊김
    }

    OnClientDisconnected(session);
}

// ═══════════════════════════════════════════════════════════════
//  UDP 수신 루프
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

        ushort payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(data);
        ushort packetType = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2));
        int tick = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4));

        byte[] payload = data.Length > HeaderSize
            ? data[HeaderSize..]
            : Array.Empty<byte>();

        // 첫 UDP 패킷으로 세션 매핑
        var session = sessionManager.FindByEndPoint(result.RemoteEndPoint);
        if (session == null)
        {
            // PlayerId를 첫 UDP 패킷의 페이로드에서 읽어 매핑
            // 규약: 클라이언트가 첫 UDP 전송 시 PacketType.Connected + payload=[4B PlayerId]
            if ((PacketType)packetType == PacketType.Connected && payload.Length >= 4)
            {
                int playerId = BinaryPrimitives.ReadInt32LittleEndian(payload);
                session = sessionManager.FindByPlayerId(playerId);
                if (session != null)
                {
                    sessionManager.RegisterUdpEndPoint(session, result.RemoteEndPoint);
                    Console.WriteLine($"[UDP] Player {playerId} mapped to {result.RemoteEndPoint}");
                }
            }
            continue;
        }

        HandlePacket(session, (PacketType)packetType, tick, payload);
    }
}

// ═══════════════════════════════════════════════════════════════
//  패킷 디스패치
// ═══════════════════════════════════════════════════════════════

void HandlePacket(Session session, PacketType type, int tick, byte[] payload)
{
    switch (type)
    {
        case PacketType.Input:
            HandleInput(session, payload);
            break;

        default:
            Console.WriteLine($"[Packet] Unknown type {type} from Player {session.PlayerId}");
            break;
    }
}

void HandleInput(Session session, byte[] payload)
{
    if (payload.Length < 8) return; // float h + float v = 8 bytes

    var reader = new PacketReader(payload);
    float h = reader.ReadFloat();
    float v = reader.ReadFloat();

    if (session.PlayerNetId is not { } netId) return;

    var obj = gameLoop.Find(netId);
    if (obj is MovableObject movable)
    {
        movable.SetInput(h, v);
    }
}

// ═══════════════════════════════════════════════════════════════
//  연결 해제 처리
// ═══════════════════════════════════════════════════════════════

void OnClientDisconnected(Session session)
{
    Console.WriteLine($"[TCP] Player {session.PlayerId} disconnected");

    // 플레이어 오브젝트 파괴
    if (session.PlayerNetId is { } netId)
    {
        var obj = gameLoop.Find(netId);
        obj?.Destroy();

        // Despawn 브로드캐스트
        var despawn = new DespawnPacket { NetId = netId };
        var data = PacketToBytes(despawn);
        _ = sessionManager.BroadcastTcpAsync(data, session.PlayerId);
    }

    sessionManager.RemoveSession(session.PlayerId);
}

// ═══════════════════════════════════════════════════════════════
//  패킷 유틸리티
// ═══════════════════════════════════════════════════════════════

async Task SendExistingObjectsAsync(Session newSession)
{
    foreach (var obj in gameLoop.FindAll<NetworkObject>())
    {
        if (obj.NetId == newSession.PlayerNetId) continue;

        var packet = MakeSpawnPacket(obj);
        var data = PacketToBytes(packet);
        await sessionManager.SendTcpAsync(newSession, data);
    }
}

async Task BroadcastSpawnAsync(NetworkObject obj)
{
    var packet = MakeSpawnPacket(obj);
    var data = PacketToBytes(packet);
    await sessionManager.BroadcastTcpAsync(data);
}

SpawnPacket MakeSpawnPacket(NetworkObject obj)
{
    return new SpawnPacket
    {
        Tick = gameLoop.CurrentTick,
        NetId = obj.NetId,
        ObjectType = (ushort)obj.ObjectType,
        OwnerId = obj.OwnerId,
        PosX = obj.Position.X,
        PosY = obj.Position.Y,
        PosZ = obj.Position.Z,
        RotX = obj.Rotation.X,
        RotY = obj.Rotation.Y,
        RotZ = obj.Rotation.Z,
        RotW = obj.Rotation.W,
    };
}

/// <summary>
/// Packet → [2B Length][2B PacketType][4B Tick][payload...]
/// </summary>
byte[] PacketToBytes(Packet packet)
{
    var writer = new PacketWriter();
    packet.Serialize(writer);
    byte[] payload = writer.ToArray();

    var final = new byte[HeaderSize + payload.Length];
    BinaryPrimitives.WriteUInt16LittleEndian(final, (ushort)payload.Length);
    BinaryPrimitives.WriteUInt16LittleEndian(final.AsSpan(2), (ushort)packet.Type);
    BinaryPrimitives.WriteInt32LittleEndian(final.AsSpan(4), packet.Tick);
    payload.CopyTo(final.AsSpan(HeaderSize));
    return final;
}

/// <summary>TCP 스트림에서 정확히 count 바이트를 읽는다.</summary>
static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken ct)
{
    int read = 0;
    while (read < count)
    {
        int n = await stream.ReadAsync(buffer.AsMemory(offset + read, count - read), ct);
        if (n == 0) return false; // 연결 끊김
        read += n;
    }
    return true;
}
