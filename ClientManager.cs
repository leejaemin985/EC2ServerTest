using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// 접속한 클라이언트 정보
/// </summary>
public class ConnectedClient
{
    public int PlayerId { get; }
    public TcpClient TcpClient { get; }
    public NetworkStream Stream { get; }
    public IPEndPoint? UdpEndPoint { get; set; }

    public ConnectedClient(int playerId, TcpClient tcpClient)
    {
        PlayerId = playerId;
        TcpClient = tcpClient;
        Stream = tcpClient.GetStream();
    }
}

/// <summary>
/// 연결된 클라이언트들을 관리
/// </summary>
public class ClientManager
{
    private readonly ConcurrentDictionary<int, ConnectedClient> _clients = new();
    private int _nextId = 0;

    public int AddClient(TcpClient tcpClient)
    {
        int id = Interlocked.Increment(ref _nextId);
        var client = new ConnectedClient(id, tcpClient);
        _clients[id] = client;
        return id;
    }

    public void RemoveClient(int playerId)
    {
        if (_clients.TryRemove(playerId, out var client))
        {
            try { client.TcpClient.Close(); } catch { }
        }
    }

    public ConnectedClient? GetClient(int playerId)
    {
        _clients.TryGetValue(playerId, out var client);
        return client;
    }

    /// <summary>UDP 엔드포인트로 플레이어 찾기</summary>
    public ConnectedClient? FindByUdpEndPoint(IPEndPoint ep)
    {
        foreach (var kvp in _clients)
        {
            if (kvp.Value.UdpEndPoint != null &&
                kvp.Value.UdpEndPoint.Address.Equals(ep.Address) &&
                kvp.Value.UdpEndPoint.Port == ep.Port)
            {
                return kvp.Value;
            }
        }
        return null;
    }

    /// <summary>특정 플레이어에게 TCP 전송</summary>
    public async Task SendTcpAsync(int playerId, byte[] data)
    {
        if (_clients.TryGetValue(playerId, out var client))
        {
            try
            {
                await client.Stream.WriteAsync(data);
            }
            catch { }
        }
    }

    /// <summary>특정 플레이어를 제외하고 모든 클라이언트에게 TCP 전송</summary>
    public async Task BroadcastTcpAsync(byte[] data, int excludeId = -1)
    {
        foreach (var kvp in _clients)
        {
            if (kvp.Key == excludeId) continue;
            try
            {
                await kvp.Value.Stream.WriteAsync(data);
            }
            catch { }
        }
    }

    /// <summary>특정 플레이어를 제외하고 UDP 위치 브로드캐스트</summary>
    public void BroadcastUdp(UdpClient udpServer, byte[] data, int excludeId = -1)
    {
        foreach (var kvp in _clients)
        {
            if (kvp.Key == excludeId) continue;
            if (kvp.Value.UdpEndPoint == null) continue;
            try
            {
                udpServer.Send(data, data.Length, kvp.Value.UdpEndPoint);
            }
            catch { }
        }
    }

    public IEnumerable<ConnectedClient> GetAllClients() => _clients.Values;

    public int Count => _clients.Count;
}
