using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using LJMCollision;
using InGame.FSM;
using InGame.Unit.Player;
using InGame.Unit.Player.States;

public class Room
{
    public int Id { get; }
    public GameLoop GameLoop { get; }
    public SessionManager SessionManager { get; }
    public CollisionWorld CollisionWorld { get; }
    public PhysicsWorld PhysicsWorld { get; }

    const int HeaderSize = 8;

    UdpClient _udpServer;

    public Room(int id, int tickRate, UdpClient udpServer, string? mapPath = null)
    {
        Id = id;
        GameLoop = new GameLoop(tickRate);
        SessionManager = new SessionManager();
        CollisionWorld = new CollisionWorld();
        PhysicsWorld = new PhysicsWorld { StaticWorld = CollisionWorld };
        _udpServer = udpServer;

        if (mapPath != null && System.IO.File.Exists(mapPath))
        {
            var mapData = MapData.FromFile(mapPath);
            CollisionWorld.Load(mapData);
            Console.WriteLine($"[Room {Id}] Map loaded: {CollisionWorld.BoxCount} boxes");
        }

        GameLoop.OnPostTick = () =>
        {
            PhysicsWorld.Step(GameLoop.DeltaTime);
            BroadcastTransforms();
        };
    }

    // ── 플레이어 입장 / 퇴장 ──

    public async Task PlayerJoinAsync(Session session)
    {
        Console.WriteLine($"[Room {Id}] Player {session.PlayerId} joined");

        var player = GameLoop.Spawn<Player>();
        player.OwnerId = session.PlayerId;
        session.PlayerNetId = player.NetId;

        // 물리 바디 부착
        var body = new PhysicsBody(
            player.Transform, BodyType.Dynamic,
            new CapsuleShape(player.CapsuleRadius, player.CapsuleHeight),
            CollisionLayer.Player)
        {
            Mass = PlayerData.Mass,
            UseGravity = true,
            Drag = PlayerData.Drag,
            MaxSlopeAngle = PlayerData.MaxSlopeAngle,
            UserData = player,
        };
        player.Body = body;
        PhysicsWorld.Add(body);

        // 상태 변경 콜백 등록
        player.Fsm.OnStateChanged = (p, state) =>
        {
            if (state is PlayerState ps)
            {
                var packet = new PlayerStatePacket
                {
                    Tick = GameLoop.CurrentTick,
                    NetId = p.NetId,
                    State = (byte)ps.StateType,
                };
                var data = PacketToBytes(packet);
                _ = SessionManager.BroadcastTcpAsync(data);
            }
        };

        // 자기 스폰을 먼저 전송 (클라이언트가 PlayerId를 인식하도록)
        var spawnData = PacketToBytes(MakeSpawnPacket(player));
        await SessionManager.SendTcpAsync(session, spawnData);

        // 기존 오브젝트들을 새 클라이언트에게 전송
        foreach (var existing in GameLoop.FindAll<NetworkObject>())
        {
            if (existing.NetId == player.NetId) continue;
            var data = PacketToBytes(MakeSpawnPacket(existing));
            await SessionManager.SendTcpAsync(session, data);
        }

        // 새 플레이어 스폰을 다른 클라이언트들에게 브로드캐스트
        await SessionManager.BroadcastTcpAsync(spawnData, session.PlayerId);
    }

    public void PlayerLeave(Session session)
    {
        Console.WriteLine($"[Room {Id}] Player {session.PlayerId} left");

        if (session.PlayerNetId is { } netId)
        {
            var obj = GameLoop.Find(netId);
            if (obj is Player player && player.Body != null)
                PhysicsWorld.Remove(player.Body);
            obj?.Destroy();

            var despawn = new DespawnPacket { NetId = netId };
            var data = PacketToBytes(despawn);
            _ = SessionManager.BroadcastTcpAsync(data, session.PlayerId);
        }

        SessionManager.RemoveSession(session.PlayerId);
    }

    // ── 패킷 처리 ──

    public void HandlePacket(Session session, PacketType type, int tick, byte[] payload)
    {
        switch (type)
        {
            case PacketType.Input:
                HandleInput(session, payload);
                break;
            default:
                Console.WriteLine($"[Room {Id}] Unknown packet {type} from Player {session.PlayerId}");
                break;
        }
    }

    void HandleInput(Session session, byte[] payload)
    {
        if (payload.Length < 16) return;

        var reader = new PacketReader(payload);
        float h = reader.ReadFloat();
        float v = reader.ReadFloat();
        float yaw = reader.ReadFloat();
        float pitch = reader.ReadFloat();
        bool jump = payload.Length >= 17 && payload[16] != 0;
        if (session.PlayerNetId is not { } netId) return;

        var obj = GameLoop.Find(netId);
        if (obj is Player player)
            player.SetInput(h, v, yaw, pitch, jump);
    }

    // ── Transform 브로드캐스트 ──

    void BroadcastTransforms()
    {
        var objects = GameLoop.FindAll<NetworkObject>();
        var writer = new PacketWriter();

        int count = 0;
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
        BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)count);

        var final = new byte[HeaderSize + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(final, (ushort)payload.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(final.AsSpan(2), (ushort)PacketType.Transform);
        BinaryPrimitives.WriteInt32LittleEndian(final.AsSpan(4), GameLoop.CurrentTick);
        payload.CopyTo(final.AsSpan(HeaderSize));

        SessionManager.BroadcastUdp(_udpServer, final);
    }

    // ── 유틸리티 ──

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
}
