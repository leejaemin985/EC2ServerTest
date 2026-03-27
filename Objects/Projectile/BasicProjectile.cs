/// <summary>기본 직선 투사체. 직선 이동, 맵/플레이어 충돌 시 파괴.</summary>
public class BasicProjectile : Projectile
{
    public override ProjectileData Data => BasicProjectileData.Instance;

    protected BasicProjectile(uint netId, GameLoop loop, Transform? transform = null)
        : base(netId, loop, transform) { }
}
