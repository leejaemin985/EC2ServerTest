using LJMCollision;

/// <summary>
/// 서버 측 게임 오브젝트의 베이스 클래스.
/// 유니티의 MonoBehaviour처럼 Awake/Start/Update/OnDestroy 라이프사이클을 제공한다.
/// GameLoop에 의해 관리되며, 틱 단위로 Update가 호출된다.
/// </summary>
public abstract class NetworkObject
{
    /// <summary>GameLoop이 부여하는 고유 ID</summary>
    public uint NetId { get; }

    /// <summary>소속된 GameLoop 참조</summary>
    public GameLoop Loop { get; }

    /// <summary>이 오브젝트의 종류. 클라이언트가 어떤 프리팹을 생성할지 결정하는 데 사용.</summary>
    public abstract NetworkObjectType ObjectType { get; }

    /// <summary>이 오브젝트를 소유한 플레이어 ID. 0이면 서버 소유(소유자 없음).</summary>
    public int OwnerId { get; set; }

    // ── Transform ──

    /// <summary>공간 정보 (위치 + 회전)</summary>
    public Transform Transform;

    // ── 생성자 ──

    protected NetworkObject(uint netId, GameLoop loop, Transform? transform = null)
    {
        NetId = netId;
        Loop = loop;
        Transform = transform ?? new Transform();
    }

    /// <summary>위치 바로가기</summary>
    public Vec3 Position
    {
        get => Transform.Position;
        set => Transform.Position = value;
    }

    /// <summary>회전 바로가기</summary>
    public Quat Rotation
    {
        get => Transform.Rotation;
        set => Transform.Rotation = value;
    }

    // ── Active / Enabled ──

    private bool _active = true;

    /// <summary>
    /// 오브젝트 활성 상태. false로 설정하면 Update가 호출되지 않는다.
    /// 유니티의 SetActive(bool)과 동일한 개념.
    /// </summary>
    public bool Active
    {
        get => _active;
        set
        {
            if (_active == value) return;
            _active = value;
            if (value) OnEnable();
            else OnDisable();
        }
    }

    /// <summary>Destroy 요청이 들어왔는지 여부. GameLoop이 틱 끝에 정리한다.</summary>
    public bool IsDestroyed { get; internal set; }

    // ── 브로드캐스트 패킷 큐 ──

    private readonly Queue<Packet> _pendingPackets = new();

    /// <summary>브로드캐스트할 패킷을 큐에 추가한다.</summary>
    protected void EnqueuePacket(Packet packet) => _pendingPackets.Enqueue(packet);

    /// <summary>큐에 쌓인 패킷을 모두 꺼낸다. GameLoop/Room이 틱마다 호출.</summary>
    internal IEnumerable<Packet> DrainPackets()
    {
        while (_pendingPackets.Count > 0)
            yield return _pendingPackets.Dequeue();
    }

    // ── 내부 상태 플래그 (GameLoop이 사용) ──

    internal bool IsAwaked { get; set; }
    internal bool IsStarted { get; set; }

    // ── 라이프사이클 메서드 (서브클래스에서 override) ──

    /// <summary>오브젝트가 생성될 때 최초 1회 호출. 초기화 용도.</summary>
    protected internal virtual void Awake() { }

    /// <summary>Awake 이후 첫 틱에서 1회 호출. Awake에서 세팅한 값을 바탕으로 로직 시작.</summary>
    protected internal virtual void Start() { }

    /// <summary>매 틱마다 호출. Active가 true일 때만 호출된다.</summary>
    protected internal virtual void Update(float deltaTime) { }

    /// <summary>오브젝트가 파괴될 때 1회 호출. 정리 용도.</summary>
    protected internal virtual void OnDestroy() { }

    /// <summary>Active가 true로 전환될 때 호출.</summary>
    protected internal virtual void OnEnable() { }

    /// <summary>Active가 false로 전환될 때 호출.</summary>
    protected internal virtual void OnDisable() { }

    // ── 유틸리티 ──

    /// <summary>이 오브젝트를 파괴 예약한다. 실제 제거는 틱 끝에 이루어진다.</summary>
    public void Destroy()
    {
        IsDestroyed = true;
    }
}
