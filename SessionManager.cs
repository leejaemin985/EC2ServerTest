using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// 모든 Session을 관리한다.
/// PlayerId, UDP EndPoint로 Session을 조회할 수 있다.
/// </summary>
public class SessionManager
{
    private int _nextPlayerId;
    private readonly ConcurrentDictionary<int, Session> _sessions = new();
    private readonly ConcurrentDictionary<string, Session> _udpMap = new();

    public int Count => _sessions.Count;

    /// <summary>TCP 접속 시 새 Session을 생성한다.</summary>
    public Session CreateSession(TcpClient tcpClient)
    {
        int playerId = Interlocked.Increment(ref _nextPlayerId);
        var session = new Session(playerId, tcpClient);
        _sessions[playerId] = session;
        return session;
    }

    /// <summary>UDP EndPoint를 Session에 매핑한다.</summary>
    public void RegisterUdpEndPoint(Session session, IPEndPoint endPoint)
    {
        session.UdpEndPoint = endPoint;
        _udpMap[EndPointKey(endPoint)] = session;
    }

    /// <summary>UDP EndPoint로 Session을 찾는다.</summary>
    public Session? FindByEndPoint(IPEndPoint endPoint)
    {
        _udpMap.TryGetValue(EndPointKey(endPoint), out var session);
        return session;
    }

    /// <summary>PlayerId로 Session을 찾는다.</summary>
    public Session? FindByPlayerId(int playerId)
    {
        _sessions.TryGetValue(playerId, out var session);
        return session;
    }

    /// <summary>Session을 제거한다.</summary>
    public void RemoveSession(int playerId)
    {
        if (_sessions.TryRemove(playerId, out var session))
        {
            if (session.UdpEndPoint != null)
                _udpMap.TryRemove(EndPointKey(session.UdpEndPoint), out _);

            session.Close();
        }
    }

    /// <summary>모든 Session을 순회한다.</summary>
    public IEnumerable<Session> GetAll() => _sessions.Values;

    /// <summary>TCP 데이터를 특정 세션에 전송한다.</summary>
    public async Task SendTcpAsync(Session session, byte[] data)
    {
        try { await session.Stream.WriteAsync(data); }
        catch { }
    }

    /// <summary>모든 세션에 TCP 전송한다.</summary>
    public async Task BroadcastTcpAsync(byte[] data, int excludePlayerId = -1)
    {
        foreach (var session in _sessions.Values)
        {
            if (session.PlayerId == excludePlayerId) continue;
            try { await session.Stream.WriteAsync(data); }
            catch { }
        }
    }

    /// <summary>모든 세션에 UDP 전송한다.</summary>
    public void BroadcastUdp(UdpClient udpServer, byte[] data, int excludePlayerId = -1)
    {
        foreach (var session in _sessions.Values)
        {
            if (session.PlayerId == excludePlayerId) continue;
            if (session.UdpEndPoint == null) continue;
            try { udpServer.Send(data, data.Length, session.UdpEndPoint); }
            catch { }
        }
    }

    private static string EndPointKey(IPEndPoint ep) => $"{ep.Address}:{ep.Port}";
}
