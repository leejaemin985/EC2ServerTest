using LJMCollision;

/// <summary>
/// 투사체 데이터 베이스 클래스.
/// 종류별 투사체 데이터가 상속하여 수치를 정의한다.
/// 데미지는 투사체가 관리하지 않음 — 히트 시 무기 쪽 콜백으로 처리.
/// </summary>
public abstract class ProjectileData
{
    /// <summary>투사체 속도 (m/s)</summary>
    public abstract float Speed { get; }
    /// <summary>충돌 반지름</summary>
    public abstract float Radius { get; }
    /// <summary>최대 수명 (초)</summary>
    public abstract float MaxLifetime { get; }
    /// <summary>발사 시 플레이어로부터 떨어지는 거리</summary>
    public abstract float SpawnOffset { get; }
    /// <summary>중력 적용 여부</summary>
    public virtual bool UseGravity => false;
    /// <summary>공기 저항 (1 = 없음)</summary>
    public virtual float Drag => 1f;

    /// <summary>충돌 Shape 생성. 기본: Sphere(Radius)</summary>
    public virtual ICollisionShape CreateShape() => new SphereShape(Radius);
}
