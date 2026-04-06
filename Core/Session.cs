using System.Net;
using System.Net.Sockets;

/// <summary>
/// 하나의 클라이언트 연결을 나타낸다.
/// TCP 소켓과 UDP EndPoint를 보관하고, 소유한 NetworkObject와 연결된다.
/// </summary>
public class Session
{
    /// <summary>서버가 부여한 고유 플레이어 ID</summary>
    public int PlayerId { get; }

    /// <summary>TCP 연결</summary>
    public TcpClient TcpClient { get; }

    /// <summary>TCP 스트림</summary>
    public NetworkStream Stream { get; }

    /// <summary>UDP EndPoint. 첫 UDP 패킷 수신 시 매핑된다.</summary>
    public IPEndPoint? UdpEndPoint { get; set; }

    public Session(int playerId, TcpClient tcpClient)
    {
        PlayerId = playerId;
        TcpClient = tcpClient;
        Stream = tcpClient.GetStream();
    }

    public void Close()
    {
        try { Stream.Close(); } catch { }
        try { TcpClient.Close(); } catch { }
    }
}
