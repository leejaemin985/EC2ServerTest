using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;

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
