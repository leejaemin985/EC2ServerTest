using System.Net;
using System.Net.Sockets;
using InGame.Unit.Player;

public class Room : IDisposable
{
    public int Id { get; }
    public GameLoop GameLoop { get; }
    public SessionManager SessionManager { get; }

    UdpClient _udpServer;
    DebugBroadcaster? _debugBroadcaster;

    public Room(int id, int tickRate, UdpClient udpServer, string? mapPath = null,
        bool debugViewer = false, bool debugHitbox = false)
    {
        Id = id;
        GameLoop = new GameLoop(tickRate, mapPath);
        SessionManager = new SessionManager();
        _udpServer = udpServer;

        if (debugViewer)
            _debugBroadcaster = new DebugBroadcaster(GameLoop, tickRate, hitbox: debugHitbox);

        // 스폰/디스폰 이벤트 구독 → 브로드캐스트
        GameLoop.Objects.OnObjectSpawned += OnObjectSpawned;
        GameLoop.Objects.OnObjectDestroyed += OnObjectDestroyed;

        GameLoop.OnPostTick = () =>
        {
            FlushObjectPackets();
            BroadcastTransforms();
            _debugBroadcaster?.Update(GameLoop.CurrentTick);
        };
    }

    // ── 이벤트 핸들러 ──

    void OnObjectSpawned(NetworkObject obj)
    {
        var spawnPacket = MakeSpawnPacket(obj);
        _ = SessionManager.BroadcastTcpAsync(spawnPacket.ToBytes());
    }

    void OnObjectDestroyed(NetworkObject obj)
    {
        var despawn = new DespawnPacket { NetId = obj.NetId };
        _ = SessionManager.BroadcastTcpAsync(despawn.ToBytes());
    }

    // ── 플레이어 입장/퇴장 ──

    public async Task PlayerJoinAsync(Session session)
    {
        Console.WriteLine($"[Room {Id}] Player {session.PlayerId} joined");

        var idPacket = new PlayerIdAssignPacket
        {
            Tick = GameLoop.CurrentTick,
            PlayerId = session.PlayerId,
        };
        await SessionManager.SendTcpAsync(session, idPacket.ToBytes());

        // 스폰 이벤트 임시 해제 (개별 전송할 것이므로)
        GameLoop.Objects.OnObjectSpawned -= OnObjectSpawned;

        var player = GameLoop.Objects.Spawn<Player>();
        player.OwnerId = session.PlayerId;

        // 스폰 이벤트 복원
        GameLoop.Objects.OnObjectSpawned += OnObjectSpawned;

        // 자신에게 자기 스폰 패킷 전송
        var spawnData = MakeSpawnPacket(player).ToBytes();
        await SessionManager.SendTcpAsync(session, spawnData);

        // 기존 오브젝트 스냅샷 전송
        foreach (var existing in GameLoop.Objects.FindAll<NetworkObject>())
        {
            if (existing.NetId == player.NetId) continue;
            await SessionManager.SendTcpAsync(session, MakeSpawnPacket(existing).ToBytes());
        }

        // 새 플레이어 스폰을 다른 클라에 브로드캐스트
        await SessionManager.BroadcastTcpAsync(spawnData, session.PlayerId);
    }

    public void PlayerLeave(Session session)
    {
        Console.WriteLine($"[Room {Id}] Player {session.PlayerId} left");

        foreach (var obj in GameLoop.Objects.FindByOwner(session.PlayerId))
        {
            obj.Destroy();
            var despawn = new DespawnPacket { NetId = obj.NetId };
            _ = SessionManager.BroadcastTcpAsync(despawn.ToBytes(), session.PlayerId);
        }

        SessionManager.RemoveSession(session.PlayerId);
    }

    // ── 패킷 처리 ──

    public void HandlePacket(Session session, PacketType type, int tick, byte[] payload)
    {
        var reader = new PacketReader(payload);
        if (reader.Remaining < 4) return;

        uint netId = reader.ReadUInt();
        var obj = GameLoop.Objects.Find(netId);
        if (obj == null || obj.OwnerId != session.PlayerId) return;

        obj.HandlePacket(type, reader);
    }

    // ── 오브젝트 패킷 큐 flush ──

    void FlushObjectPackets()
    {
        foreach (var obj in GameLoop.Objects.FindAll<NetworkObject>())
        {
            foreach (var packet in obj.DrainPackets())
                _ = SessionManager.BroadcastTcpAsync(packet.ToBytes());
        }
    }

    // ── Transform 브로드캐스트 ──

    void BroadcastTransforms()
    {
        var packet = GameLoop.Objects.BuildTransformSnapshot(GameLoop.CurrentTick);
        if (packet.Count == 0) return;

        SessionManager.BroadcastUdp(_udpServer, packet.ToBytes());
    }

    public void Dispose()
    {
        _debugBroadcaster?.Dispose();
    }

    // ── 헬퍼 ──

    SpawnPacket MakeSpawnPacket(NetworkObject obj)
    {
        return new SpawnPacket
        {
            Tick = GameLoop.CurrentTick,
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
}
