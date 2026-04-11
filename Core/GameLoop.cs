using System.Diagnostics;
using System.Reflection;

/// <summary>
/// 틱 기반 게임 루프 엔진.
/// NetworkObject들의 생명주기를 관리하고, 고정 간격(TickRate)으로 Update를 호출한다.
/// </summary>
public class GameLoop
{
    // ── 설정 ──

    public int TickRate { get; }
    public float DeltaTime { get; }
    public PhysicsWorld PhysicsWorld { get; } = new();

    /// <summary>bake된 애니메이션 클립 관리 (공유 자원)</summary>
    public AnimationClipManager AnimClips { get; } = new();

    /// <summary>hitbox 정의 (캐릭터 공통, 공유 자원)</summary>
    public HitboxDefinition? HitboxDefs { get; private set; }

    /// <summary>매 틱 Update 이후 호출되는 콜백. Transform 브로드캐스트 등에 사용.</summary>
    public Action? OnPostTick;

    // ── 상태 ──

    public int CurrentTick { get; private set; }
    public bool IsRunning { get; private set; }

    // ── 오브젝트 관리 ──

    private uint _nextNetId;
    private readonly object _lock = new();
    private readonly Dictionary<uint, NetworkObject> _objects = new();
    private readonly List<NetworkObject> _pendingAdd = new();
    private readonly List<NetworkObject> _pendingDestroy = new();

    // 순회 중 안전하게 접근하기 위한 스냅샷
    private readonly List<NetworkObject> _updateList = new();

    public GameLoop(int tickRate = 30, string? mapPath = null)
    {
        TickRate = tickRate;
        DeltaTime = 1f / tickRate;

        if (mapPath != null && System.IO.File.Exists(mapPath))
            PhysicsWorld.LoadMap(mapPath);

        // 애니메이션 데이터 로드
        AnimClips.LoadFolder("Data/Animations");
        const string hitboxPath = "Data/Animations/hitbox_defs.json";
        if (File.Exists(hitboxPath))
            HitboxDefs = HitboxDefinition.FromFile(hitboxPath);
    }

    // ── NetworkObject 등록 / 조회 ──

    /// <summary>
    /// NetworkObject를 생성하여 루프에 등록한다.
    /// NetId를 자동 부여하고, 다음 틱에 Awake → Start 순으로 호출된다.
    /// </summary>
    public T Spawn<T>(Transform? transform = null) where T : NetworkObject
    {
        lock (_lock)
        {
            uint netId = ++_nextNetId;
            var obj = (T)Activator.CreateInstance(
                typeof(T),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object?[] { netId, this, transform },
                null)!;

            // 즉시 Awake 호출하여 Spawn 직후 안전하게 접근 가능하도록
            if (!obj.IsAwaked)
            {
                obj.IsAwaked = true;
                obj.Awake();
            }

            _pendingAdd.Add(obj);
            return obj;
        }
    }

    /// <summary>NetId로 오브젝트를 찾는다.</summary>
    public NetworkObject? Find(uint netId)
    {
        lock (_lock)
        {
            if (_objects.TryGetValue(netId, out var obj))
                return obj;

            foreach (var pending in _pendingAdd)
            {
                if (pending.NetId == netId) return pending;
            }
            return null;
        }
    }

    /// <summary>현재 틱의 모든 NetworkObject Transform 스냅샷을 패킷으로 만든다.</summary>
    public TransformPacket BuildTransformSnapshot()
    {
        lock (_lock)
        {
            var packet = new TransformPacket { Tick = CurrentTick };
            foreach (var obj in _objects.Values)
                packet.Add(obj.NetId, obj.Position, obj.Rotation);
            foreach (var obj in _pendingAdd)
                packet.Add(obj.NetId, obj.Position, obj.Rotation);
            return packet;
        }
    }

    /// <summary>특정 OwnerId를 가진 오브젝트를 모두 찾는다.</summary>
    public List<NetworkObject> FindByOwner(int ownerId)
    {
        lock (_lock)
        {
            var result = new List<NetworkObject>();
            foreach (var obj in _objects.Values)
            {
                if (obj.OwnerId == ownerId) result.Add(obj);
            }
            foreach (var obj in _pendingAdd)
            {
                if (obj.OwnerId == ownerId) result.Add(obj);
            }
            return result;
        }
    }

    /// <summary>특정 타입의 오브젝트를 모두 찾는다.</summary>
    public List<T> FindAll<T>() where T : NetworkObject
    {
        lock (_lock)
        {
            var result = new List<T>();
            foreach (var obj in _objects.Values)
            {
                if (obj is T typed) result.Add(typed);
            }
            foreach (var obj in _pendingAdd)
            {
                if (obj is T typed) result.Add(typed);
            }
            return result;
        }
    }

    // ── 메인 루프 ──

    /// <summary>게임 루프를 시작한다. CancellationToken으로 정지할 수 있다.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        IsRunning = true;
        var sw = Stopwatch.StartNew();
        long nextTickMs = 0;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                long now = sw.ElapsedMilliseconds;
                if (now < nextTickMs)
                {
                    await Task.Delay((int)(nextTickMs - now), ct);
                }
                nextTickMs = sw.ElapsedMilliseconds + (1000 / TickRate);

                Tick();
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            // 종료 시 모든 오브젝트 정리
            foreach (var obj in _objects.Values)
            {
                if (!obj.IsDestroyed)
                {
                    obj.IsDestroyed = true;
                    obj.OnDestroy();
                }
            }
            _objects.Clear();
            IsRunning = false;
        }
    }

    /// <summary>단일 틱 실행. 테스트나 수동 제어 시 직접 호출할 수 있다.</summary>
    public void Tick()
    {
        CurrentTick++;

        lock (_lock)
        {
            // 1) 신규 오브젝트 추가 — Awake 호출
            if (_pendingAdd.Count > 0)
            {
                foreach (var obj in _pendingAdd)
                {
                    _objects[obj.NetId] = obj;
                    if (!obj.IsAwaked)
                    {
                        obj.IsAwaked = true;
                        obj.Awake();
                    }
                }
                _pendingAdd.Clear();
            }

            // 2) 스냅샷 구성 — Start & Update
            _updateList.Clear();
            foreach (var obj in _objects.Values)
            {
                if (!obj.IsDestroyed)
                    _updateList.Add(obj);
            }
        }

        foreach (var obj in _updateList)
        {
            if (!obj.Active) continue;

            // Start (최초 1회, Active일 때만)
            if (!obj.IsStarted)
            {
                obj.IsStarted = true;
                obj.Start();
            }

            // Update
            if (!obj.IsDestroyed)
            {
                obj.Update(DeltaTime);
            }
        }

        // 3) 물리 시뮬레이션
        PhysicsWorld.Step(DeltaTime);

        // 4) PostTick 콜백
        OnPostTick?.Invoke();

        // 5) 파괴 예약된 오브젝트 정리
        lock (_lock)
        {
            _pendingDestroy.Clear();
            foreach (var obj in _objects.Values)
            {
                if (obj.IsDestroyed)
                    _pendingDestroy.Add(obj);
            }

            foreach (var obj in _pendingDestroy)
            {
                obj.OnDestroy();
                _objects.Remove(obj.NetId);
            }
        }
    }
}
