using LJMCollision;

/// <summary>
/// 물리 바디의 종류.
/// Static: 움직이지 않음 (맵, 구조물). 충돌 판정 대상이지만 물리 영향 안 받음.
/// Dynamic: 움직임 (플레이어, 투사체). 속도, 질량, 넉백 등 물리 영향 받음.
/// Trigger: 충돌 감지만 하고 물리 반응 없음 (영역 진입 감지 등).
/// </summary>
public enum BodyType : byte
{
    Static,
    Dynamic,
    Trigger,
}

/// <summary>
/// 물리 시스템의 핵심 컴포넌트.
/// Transform을 외부와 공유하여 동기화 없이 위치/회전이 반영된다.
/// </summary>
public class PhysicsBody
{
    /// <summary>바디 종류</summary>
    public BodyType BodyType { get; set; }

    /// <summary>충돌 형태</summary>
    public ICollisionShape Shape { get; set; }

    /// <summary>자기 레이어</summary>
    public CollisionLayer Layer { get; set; }

    // ── Transform (공유 참조) ──

    /// <summary>공유 Transform. NetworkObject와 같은 인스턴스를 참조한다.</summary>
    public Transform Transform { get; }

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

    /// <summary>활성 여부. false면 물리 처리에서 제외된다.</summary>
    public bool Active { get; set; } = true;

    /// <summary>외부에서 자유롭게 사용할 수 있는 참조. 물리 시스템은 이 값을 사용하지 않는다.</summary>
    public object? UserData { get; set; }

    // ── 물리 속성 (Dynamic 전용) ──

    /// <summary>질량 (kg). 넉백 계산에 사용. 음수면 무한 질량(밀리지 않음). 0이면 충돌 판정만, 밀어냄 없음.</summary>
    public float Mass { get; set; } = 70f;

    /// <summary>무한 질량 여부</summary>
    public bool IsInfiniteMass => Mass < 0f;

    /// <summary>현재 속도 (m/s). 매 틱 Position에 적용된다.</summary>
    public Vec3 Velocity { get; set; }

    /// <summary>속도 감쇠 (0~1). 매 틱 Velocity에 곱해진다. 1이면 감쇠 없음.</summary>
    public float Drag { get; set; } = 0.9f;

    /// <summary>중력 영향 여부</summary>
    public bool UseGravity { get; set; }

    /// <summary>등반 가능한 최대 경사 각도 (도). 이 각도를 초과하면 미끄러진다.</summary>
    public float MaxSlopeAngle { get; set; } = 45f;

    /// <summary>바닥에 닿아 있는지 여부. 맵 충돌 시 PhysicsWorld가 갱신한다.</summary>
    public bool Grounded { get; set; }

    /// <summary>바닥 법선 벡터. Grounded일 때만 유효.</summary>
    public Vec3 GroundNormal { get; set; }

    /// <summary>직전 틱의 맵 충돌 결과.</summary>
    public OverlapResult LastStaticOverlap { get; set; }

    /// <summary>직전 틱의 동적 바디 충돌 결과 목록.</summary>
    public List<(PhysicsBody Other, CollisionResult Result)> LastDynamicCollisions { get; } = new();

    /// <summary>맵 충돌 후 콜백. PhysicsWorld.MoveAndCollide에서 호출.</summary>
    public Action<PhysicsBody, OverlapResult>? OnStaticCollision { get; set; }

    /// <summary>동적 바디 충돌 후 콜백. PhysicsWorld.ResolveDynamicCollisions에서 호출.</summary>
    public Action<PhysicsBody, PhysicsBody, CollisionResult>? OnDynamicCollision { get; set; }

    public PhysicsBody(Transform transform, BodyType bodyType, ICollisionShape shape, CollisionLayer layer)
    {
        Transform = transform;
        BodyType = bodyType;
        Shape = shape;
        Layer = layer;
    }

    // ── 물리 API ──

    /// <summary>충격량(impulse)을 가한다. Velocity에 즉시 반영.</summary>
    public void AddImpulse(Vec3 impulse)
    {
        if (BodyType != BodyType.Dynamic) return;
        if (Mass <= 0f) return; // 0: 밀어냄 없음, 음수: 무한질량
        Velocity += impulse * (1f / Mass);
    }

    /// <summary>힘(force)을 가한다. 질량과 deltaTime을 고려하여 Velocity에 반영.</summary>
    public void AddForce(Vec3 force, float deltaTime)
    {
        if (BodyType != BodyType.Dynamic) return;
        if (Mass <= 0f) return;
        Velocity += force * (deltaTime / Mass);
    }
}
