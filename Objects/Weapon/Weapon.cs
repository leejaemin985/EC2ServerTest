using LJMCollision;

/// <summary>
/// 플레이어가 소유하는 무기 컴포넌트.
/// 발사 가능 여부(쿨다운), 방향 계산, 투사체 스폰까지 담당.
/// </summary>
public class Weapon
{
    public WeaponData Data { get; }

    readonly NetworkObjectManager _objects;
    readonly PhysicsWorld _physics;

    float _cooldownRemaining;

    public bool CanFire => _cooldownRemaining <= 0f;

    public Weapon(WeaponData data, NetworkObjectManager objects, PhysicsWorld physics)
    {
        Data = data;
        _objects = objects;
        _physics = physics;
    }

    /// <summary>발사. 쿨다운 체크 → 방향 계산 → 투사체 스폰.</summary>
    public bool Fire(uint ownerNetId, Vec3 ownerPos, Quat ownerRot, float yaw, float pitch)
    {
        if (!CanFire) return false;
        _cooldownRemaining = Data.FireRate;

        // 발사 방향 계산 (클라 pitch: 위를 보면 음수)
        float yawRad = yaw * MathF.PI / 180f;
        float pitchRad = -pitch * MathF.PI / 180f;
        float cosPitch = MathF.Cos(pitchRad);
        var direction = new Vec3(
            cosPitch * MathF.Sin(yawRad),
            MathF.Sin(pitchRad),
            cosPitch * MathF.Cos(yawRad));

        // 총구 위치
        var muzzlePos = ownerPos + ownerRot.Rotate(Data.MuzzleOffset);

        // 투사체 스폰
        var proj = _objects.Spawn<BasicProjectile>();
        proj.Position = muzzlePos;
        proj.AttachPhysics(_physics, Data.BulletRadius);
        proj.Initialize(ownerNetId, direction, Data.BulletSpeed, Data.BulletLifetime);

        return true;
    }

    /// <summary>매 틱 호출. 쿨다운 감소.</summary>
    public void Update(float deltaTime)
    {
        if (_cooldownRemaining > 0f)
            _cooldownRemaining -= deltaTime;
    }
}
