using System.Diagnostics;

/// <summary>
/// 틱 기반 게임 루프 엔진.
/// NetworkObjectManager가 관리하는 오브젝트들에 대해 고정 간격(TickRate)으로 Update를 호출한다.
/// </summary>
public class GameLoop
{
    // ── 설정 ──

    public int TickRate { get; }
    public float DeltaTime { get; }
    public PhysicsWorld PhysicsWorld { get; } = new();
    public NetworkObjectManager Objects { get; }

    /// <summary>매 틱 Update 이후 호출되는 콜백. Transform 브로드캐스트 등에 사용.</summary>
    public Action? OnPostTick;

    // ── 상태 ──

    public int CurrentTick { get; private set; }
    public bool IsRunning { get; private set; }

    public GameLoop(int tickRate = 30, string? mapPath = null)
    {
        TickRate = tickRate;
        DeltaTime = 1f / tickRate;
        Objects = new NetworkObjectManager(this);

        if (mapPath != null && System.IO.File.Exists(mapPath))
            PhysicsWorld.LoadMap(mapPath);
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
            Objects.DestroyAll();
            IsRunning = false;
        }
    }

    /// <summary>단일 틱 실행. 테스트나 수동 제어 시 직접 호출할 수 있다.</summary>
    public void Tick()
    {
        CurrentTick++;

        // 1) 오브젝트 등록 + Update 대상 수집
        var updateList = Objects.FlushAndGetUpdateList();

        // 2) Start & Update
        foreach (var obj in updateList)
        {
            if (!obj.Active) continue;

            if (!obj.IsStarted)
            {
                obj.IsStarted = true;
                obj.Start();
            }

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
        Objects.CleanupDestroyed();
    }
}
