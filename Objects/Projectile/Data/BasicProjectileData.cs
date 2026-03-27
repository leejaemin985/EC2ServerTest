/// <summary>기본 직선 투사체 데이터</summary>
public class BasicProjectileData : ProjectileData
{
    public static readonly BasicProjectileData Instance = new();

    public override float Speed => 50f;
    public override float Radius => 0.15f;
    public override float MaxLifetime => 3f;
    public override float SpawnOffset => 1.0f;
}
