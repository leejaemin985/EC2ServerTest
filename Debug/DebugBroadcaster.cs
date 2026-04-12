using System.Net.WebSockets;
using System.Text.Json;
using LJMCollision;

/// <summary>
/// PhysicsWorld 상태를 WebSocket으로 브라우저 디버그 뷰어에 스트리밍하는 브로드캐스터.
/// - GameLoop.PostTick에서 Update(tick) 호출.
/// - 클라이언트 접속 시 layer 목록을 1회 전송.
/// - 이후 매 주기(기본 10Hz)로 full snapshot 전송.
/// </summary>
public sealed class DebugBroadcaster : IDisposable
{
    readonly GameLoop _gameLoop;
    readonly PhysicsWorld _physics;
    readonly DebugWebSocketServer _ws;
    readonly int _intervalTicks;
    readonly bool _hitbox;
    int _lastSentTick;

    // 맵은 변경되지 않으므로 1회만 직렬화하여 캐싱
    List<object>? _cachedMapShapes;

    public DebugBroadcaster(GameLoop gameLoop, int tickRate, int port = 9090, int sendRate = 10, bool hitbox = false)
    {
        _gameLoop = gameLoop;
        _physics = gameLoop.PhysicsWorld;
        _intervalTicks = Math.Max(1, tickRate / sendRate);
        _hitbox = hitbox;
        _ws = new DebugWebSocketServer(port);
        _ws.OnClientConnected = SendLayersAsync;
    }

    public void Dispose() => _ws.Dispose();

    /// <summary>틱마다 호출. 주기에 맞춰 snapshot을 브로드캐스트한다.</summary>
    public void Update(int currentTick)
    {
        if ((currentTick - _lastSentTick) < _intervalTicks) return;
        _lastSentTick = currentTick;

        string payload = BuildStatePayload(currentTick);
        _ = _ws.BroadcastAsync(payload);
    }

    // ── Layer 목록 (접속 시 1회) ──

    async Task SendLayersAsync(WebSocket socket)
    {
        var layerNames = GetDefinedLayers();
        string payload = JsonSerializer.Serialize(new { type = "layers", layers = layerNames }, JsonOpts);
        await _ws.SendAsync(socket, payload);
    }

    static List<string> GetDefinedLayers()
    {
        var names = new List<string>();
        foreach (CollisionLayer val in Enum.GetValues<CollisionLayer>())
        {
            if (val == CollisionLayer.None || val == CollisionLayer.All) continue;
            names.Add(val.ToString());
        }
        names.Add("Hitbox");
        return names;
    }

    // ── State snapshot ──

    string BuildStatePayload(int tick)
    {
        var shapes = new List<object>();

        AppendMapShapes(shapes);
        AppendBodyShapes(shapes);
        if (_hitbox) AppendHitboxShapes(shapes);

        return JsonSerializer.Serialize(new { type = "state", tick, shapes }, JsonOpts);
    }

    /// <summary>맵 OBB (정적, 캐싱)</summary>
    void AppendMapShapes(List<object> shapes)
    {
        if (_physics.MapData == null) return;

        // 최초 1회만 생성
        if (_cachedMapShapes == null)
        {
            _cachedMapShapes = new List<object>(_physics.MapData.Boxes.Count);
            foreach (var b in _physics.MapData.Boxes)
            {
                var halfSize = new[] { b.SX * 0.5f, b.SY * 0.5f, b.SZ * 0.5f };
                _cachedMapShapes.Add(new
                {
                    shape = "OBB",
                    layer = "Map",
                    pos = new[] { b.X, b.Y - halfSize[1], b.Z },
                    rot = ToArray(Quat.FromEuler(b.RX, b.RY, b.RZ)),
                    halfSize
                });
            }
        }

        shapes.AddRange(_cachedMapShapes);
    }

    /// <summary>동적 PhysicsBody</summary>
    void AppendBodyShapes(List<object> shapes)
    {
        foreach (var body in _physics.Bodies)
        {
            if (!body.Active) continue;

            string layerName = body.Layer.ToString();

            switch (body.Shape)
            {
                case SphereShape s:
                    shapes.Add(new
                    {
                        shape = "Sphere",
                        layer = layerName,
                        pos = ToArray(body.Position),
                        rot = ToArray(body.Rotation),
                        radius = s.Radius
                    });
                    break;

                case CapsuleShape c:
                    shapes.Add(new
                    {
                        shape = "Capsule",
                        layer = layerName,
                        pos = ToArray(body.Position),
                        rot = ToArray(body.Rotation),
                        radius = c.Radius,
                        height = c.Height
                    });
                    break;

                case BoxShape bx:
                    shapes.Add(new
                    {
                        shape = "OBB",
                        layer = layerName,
                        pos = ToArray(body.Position),
                        rot = ToArray(body.Rotation),
                        halfSize = new[] { bx.HalfSize.X, bx.HalfSize.Y, bx.HalfSize.Z }
                    });
                    break;
            }
        }
    }

    /// <summary>Player bone hitbox</summary>
    void AppendHitboxShapes(List<object> shapes)
    {
        foreach (var player in _gameLoop.FindAll<InGame.Unit.Player.Player>())
        {
            var hitboxes = player.EvaluateHitboxes(_gameLoop.CurrentTick);
            if (hitboxes == null) continue;

            foreach (var wh in hitboxes)
            {
                switch (wh.Type)
                {
                    case HitboxShapeType.Sphere:
                        shapes.Add(new
                        {
                            shape = "Sphere",
                            layer = "Hitbox",
                            pos = ToArray(wh.Center),
                            rot = ToArray(wh.Rotation),
                            radius = wh.Radius,
                            isCenter = true
                        });
                        break;

                    case HitboxShapeType.Capsule:
                        shapes.Add(new
                        {
                            shape = "Capsule",
                            layer = "Hitbox",
                            pos = ToArray(wh.Center),
                            rot = ToArray(wh.Rotation),
                            radius = wh.Radius,
                            height = wh.Height,
                            direction = wh.Direction,
                            isCenter = true
                        });
                        break;

                    case HitboxShapeType.OBB:
                        shapes.Add(new
                        {
                            shape = "OBB",
                            layer = "Hitbox",
                            pos = ToArray(wh.Center),
                            rot = ToArray(wh.Rotation),
                            halfSize = new[] { wh.HalfSize.X, wh.HalfSize.Y, wh.HalfSize.Z },
                            isCenter = true
                        });
                        break;
                }
            }
        }
    }

    // ── 유틸 ──

    static float[] ToArray(Vec3 v) => new[] { v.X, v.Y, v.Z };
    static float[] ToArray(Quat q) => new[] { q.X, q.Y, q.Z, q.W };

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
