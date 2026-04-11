using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
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
    int _lastSentTick;

    /// <param name="tickRate">GameLoop 틱레이트(Hz)</param>
    /// <param name="port">WebSocket 포트</param>
    /// <param name="sendRate">브로드캐스트 주기(Hz)</param>
    public DebugBroadcaster(GameLoop gameLoop, int tickRate, int port = 9090, int sendRate = 10)
    {
        _gameLoop = gameLoop;
        _physics = gameLoop.PhysicsWorld;
        _intervalTicks = Math.Max(1, tickRate / sendRate);
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
        names.Add("Hitbox"); // 테스트용 bone hitbox 레이어
        return names;
    }

    // ── State snapshot (매 주기) ──

    string BuildStatePayload(int tick)
    {
        var shapes = new List<object>();

        // 1) MapData OBB → layer "Map", feet position으로 변환
        if (_physics.MapData != null)
        {
            foreach (var b in _physics.MapData.Boxes)
            {
                var halfSize = new[] { b.SX * 0.5f, b.SY * 0.5f, b.SZ * 0.5f };
                // center → feet: feetY = centerY - halfSizeY
                var feetPos = new[] { b.X, b.Y - halfSize[1], b.Z };
                var rot = ToArray(Quat.FromEuler(b.RX, b.RY, b.RZ));

                shapes.Add(new
                {
                    shape = "OBB",
                    layer = "Map",
                    pos = feetPos,
                    rot,
                    halfSize
                });
            }
        }

        // 2) 동적 바디
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

        // 3) Player bone hitbox
        foreach (var player in _gameLoop.FindAll<InGame.Unit.Player.Player>())
        {
            var hitboxes = player.EvaluateHitboxes();
            if (hitboxes == null) continue;
            foreach (var wh in hitboxes)
            {
                switch (wh.Type)
                {
                    case HitboxDefinition.HitboxShapeType.Sphere:
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

                    case HitboxDefinition.HitboxShapeType.Capsule:
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

                    case HitboxDefinition.HitboxShapeType.OBB:
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

        return JsonSerializer.Serialize(new { type = "state", tick, shapes }, JsonOpts);
    }

    static float[] ToArray(Vec3 v) => new[] { v.X, v.Y, v.Z };
    static float[] ToArray(Quat q) => new[] { q.X, q.Y, q.Z, q.W };

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

// ──────────────────────────────────────────────────────────────
// WebSocket 미니 서버
// ──────────────────────────────────────────────────────────────

/// <summary>
/// 디버그 뷰어 전용 WebSocket 서버.
/// /ws 경로로 접속한 브라우저 소켓을 관리하고 broadcast 기능을 제공한다.
/// </summary>
internal sealed class DebugWebSocketServer : IDisposable
{
    readonly HttpListener _listener = new();
    readonly ConcurrentDictionary<WebSocket, byte> _clients = new();
    readonly CancellationTokenSource _cts = new();
    readonly Task _acceptLoop;

    public Func<WebSocket, Task>? OnClientConnected { get; set; }

    public DebugWebSocketServer(int port)
    {
        string prefix = $"http://localhost:{port}/";
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        Console.WriteLine($"[DebugWS] Listening on ws://localhost:{port}/ws");
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    public async Task BroadcastAsync(string payload)
    {
        var buffer = Encoding.UTF8.GetBytes(payload);
        foreach (var socket in _clients.Keys)
            await SendAsync(socket, buffer);
    }

    public async Task SendAsync(WebSocket socket, string payload)
    {
        var buffer = Encoding.UTF8.GetBytes(payload);
        await SendAsync(socket, buffer);
    }

    async Task SendAsync(WebSocket socket, byte[] buffer)
    {
        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch { RemoveClient(socket); }
    }

    async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch when (_cts.IsCancellationRequested) { break; }
            catch { continue; }

            if (!ctx.Request.IsWebSocketRequest || ctx.Request.Url?.AbsolutePath != "/ws")
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
                continue;
            }

            WebSocketContext? wsCtx;
            try { wsCtx = await ctx.AcceptWebSocketAsync(null); }
            catch
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.Close();
                continue;
            }

            var socket = wsCtx.WebSocket;
            _clients[socket] = 0;
            Console.WriteLine("[DebugWS] Client connected");

            if (OnClientConnected != null)
                _ = OnClientConnected(socket);

            _ = Task.Run(() => ReceiveLoopAsync(socket));
        }
    }

    async Task ReceiveLoopAsync(WebSocket socket)
    {
        var buffer = new byte[256];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch { }
        finally { RemoveClient(socket); }
    }

    void RemoveClient(WebSocket socket)
    {
        if (_clients.TryRemove(socket, out _))
        {
            try { socket.Dispose(); } catch { }
            Console.WriteLine("[DebugWS] Client removed");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        foreach (var socket in _clients.Keys)
        {
            try { socket.Abort(); } catch { }
        }
    }
}
