using System.Reflection;

/// <summary>
/// NetworkObject의 생명주기를 관리한다.
/// 스폰, 디스폰, 조회, ID 부여를 담당하며 GameLoop은 틱 엔진 역할만 한다.
/// </summary>
public class NetworkObjectManager
{
    private uint _nextNetId;
    private readonly object _lock = new();
    private readonly Dictionary<uint, NetworkObject> _objects = new();
    private readonly List<NetworkObject> _pendingAdd = new();
    private readonly List<NetworkObject> _pendingDestroy = new();

    // 순회용 스냅샷
    private readonly List<NetworkObject> _updateList = new();

    private readonly GameLoop _loop;

    // ── 이벤트 ──

    /// <summary>오브젝트가 스폰되었을 때 발행. Room이 구독하여 브로드캐스트.</summary>
    public Action<NetworkObject>? OnObjectSpawned;

    /// <summary>오브젝트가 파괴되었을 때 발행. Room이 구독하여 브로드캐스트.</summary>
    public Action<NetworkObject>? OnObjectDestroyed;

    public NetworkObjectManager(GameLoop loop)
    {
        _loop = loop;
    }

    // ── 스폰 ──

    public T Spawn<T>(Transform? transform = null) where T : NetworkObject
    {
        lock (_lock)
        {
            uint netId = ++_nextNetId;
            var obj = (T)Activator.CreateInstance(
                typeof(T),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object?[] { netId, _loop, transform },
                null)!;

            if (!obj.IsAwaked)
            {
                obj.IsAwaked = true;
                obj.Awake();
            }

            _pendingAdd.Add(obj);
            OnObjectSpawned?.Invoke(obj);
            return obj;
        }
    }

    // ── 조회 ──

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

    // ── Transform 스냅샷 ──

    public TransformPacket BuildTransformSnapshot(int currentTick)
    {
        lock (_lock)
        {
            var packet = new TransformPacket { Tick = currentTick };
            foreach (var obj in _objects.Values)
                packet.Add(obj.NetId, obj.Position, obj.Rotation);
            foreach (var obj in _pendingAdd)
                packet.Add(obj.NetId, obj.Position, obj.Rotation);
            return packet;
        }
    }

    // ── 틱 처리 (GameLoop이 호출) ──

    /// <summary>pending 오브젝트를 등록하고 Update 대상 목록을 반환.</summary>
    internal List<NetworkObject> FlushAndGetUpdateList()
    {
        lock (_lock)
        {
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

            _updateList.Clear();
            foreach (var obj in _objects.Values)
            {
                if (!obj.IsDestroyed)
                    _updateList.Add(obj);
            }
        }
        return _updateList;
    }

    /// <summary>파괴 예약된 오브젝트를 정리.</summary>
    internal void CleanupDestroyed()
    {
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
                OnObjectDestroyed?.Invoke(obj);
            }
        }
    }

    /// <summary>모든 오브젝트 정리 (종료 시).</summary>
    internal void DestroyAll()
    {
        foreach (var obj in _objects.Values)
        {
            if (!obj.IsDestroyed)
            {
                obj.IsDestroyed = true;
                obj.OnDestroy();
            }
        }
        _objects.Clear();
    }
}
