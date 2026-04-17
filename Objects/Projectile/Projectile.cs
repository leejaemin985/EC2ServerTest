using LJMCollision;
using InGame.Unit.Player;

/// <summary>
/// 투사체 베이스 클래스.
/// 공통: 스폰, 수명, 맵/플레이어 충돌 콜백 연결, 파괴.
/// 데미지는 투사체가 관리하지 않음 — 히트 시 OnHit 콜백으로 외부(무기)에 전달.
/// </summary>
public abstract class Projectile : NetworkObject
{
    public override NetworkObjectType ObjectType => NetworkObjectType.Projectile;

    public PhysicsBody? Body { get; set; }
    public uint OwnerNetId { get; protected set; }
    public Vec3 Direction { get; protected set; }

    /// <summary>히트 시 외부 콜백.</summary>
    public Action<Projectile, Player>? OnHit { get; set; }

    bool _destroyed;
    float _lifetime;

    protected Projectile(uint netId, GameLoop loop, Transform? transform = null)
        : base(netId, loop, transform) { }

    // ── 초기화 ──

    public void Initialize(uint ownerNetId, Vec3 direction, float speed, float lifetime = 5f)
    {
        OwnerNetId = ownerNetId;
        Direction = direction;
        _lifetime = lifetime;

        if (Body != null)
        {
            Body.Velocity = direction * speed;
            Body.UserData = this;
            Body.OnStaticCollision = OnMapHit;
            Body.OnDynamicCollision = OnBodyHit;
        }
    }

    /// <summary>PhysicsBody를 생성하고 월드에 등록한다.</summary>
    public void AttachPhysics(PhysicsWorld world, float radius, bool useGravity = false, float drag = 1f)
    {
        var body = new PhysicsBody(
            Transform, BodyType.Dynamic,
            new SphereShape(radius),
            CollisionLayer.Projectile)
        {
            Mass = 0f,
            UseGravity = useGravity,
            Drag = drag,
        };
        Body = body;
        world.Add(body);
    }

    // ── 충돌 응답 (서브클래스에서 override 가능) ──

    protected virtual void OnMapHit(PhysicsBody body, OverlapResult overlap)
    {
        DestroyProjectile();
    }

    protected virtual void OnBodyHit(PhysicsBody self, PhysicsBody other, CollisionResult result)
    {
        if (_destroyed) return;
        if (other.UserData is not Player victim) return;
        if (victim.NetId == OwnerNetId) return;

        OnHit?.Invoke(this, victim);
        DestroyProjectile();
    }

    // ── Update ──

    protected internal override void Update(float deltaTime)
    {
        if (_destroyed) return;

        _lifetime -= deltaTime;
        if (_lifetime <= 0f)
            DestroyProjectile();
    }

    // ── 파괴 ──

    protected void DestroyProjectile()
    {
        if (_destroyed) return;
        _destroyed = true;
        Destroy();
    }
}
