using LJMCollision;

/// <summary>
/// 플레이어가 소유하는 무기 컴포넌트.
/// 발사 가능 여부(쿨다운)만 관리하며, 벽 체크/투사체 생성/데미지는 외부 책임.
/// </summary>
public class Weapon
{
    public WeaponData Data { get; }

    float _cooldownRemaining;

    public bool CanFire => _cooldownRemaining <= 0f;

    public Weapon(WeaponData data)
    {
        Data = data;
    }

    /// <summary>발사 시도. 쿨다운이 끝났으면 true + 쿨다운 시작.</summary>
    public bool TryFire()
    {
        if (!CanFire) return false;
        _cooldownRemaining = Data.FireRate;
        return true;
    }

    /// <summary>매 틱 호출. 쿨다운 감소.</summary>
    public void Update(float deltaTime)
    {
        if (_cooldownRemaining > 0f)
            _cooldownRemaining -= deltaTime;
    }

    /// <summary>총구 월드 위치 계산.</summary>
    public Vec3 GetMuzzleWorldPosition(Vec3 playerPos, Quat playerRot)
    {
        return playerPos + playerRot.Rotate(Data.MuzzleOffset);
    }
}
