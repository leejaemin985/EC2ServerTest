using LJMCollision;

/// <summary>
/// PhysicsWorld 쿼리 API — Raycast, RaycastAll
/// </summary>
public partial class PhysicsWorld
{
    /// <summary>레이캐스트 — 동적 바디 대상 (레이어 필터링)</summary>
    public RaycastResult Raycast(Ray ray, float maxDistance, CollisionLayer mask,
        PhysicsBody? ignore = null)
    {
        return RaycastWithBody(ray, maxDistance, mask, ignore).Result;
    }

    /// <summary>레이캐스트 — 맞은 바디 정보도 함께 반환</summary>
    public (RaycastResult Result, PhysicsBody? Body) RaycastWithBody(Ray ray, float maxDistance,
        CollisionLayer mask, PhysicsBody? ignore = null)
    {
        RaycastResult closest = RaycastResult.None;
        PhysicsBody? hitBody = null;

        foreach (var body in _bodies)
        {
            if (!body.Active) continue;
            if (body == ignore) continue;
            if ((body.Layer & mask) == 0) continue;

            RaycastResult hit = RaycastBody(ray, body);
            if (hit.Hit && hit.Distance < maxDistance)
            {
                if (!closest.Hit || hit.Distance < closest.Distance)
                {
                    closest = hit;
                    hitBody = body;
                }
                maxDistance = hit.Distance;
            }
        }

        return (closest, hitBody);
    }

    /// <summary>레이캐스트 — 정적 맵 + 동적 바디 통합</summary>
    public RaycastResult RaycastAll(Ray ray, float maxDistance, CollisionLayer mask,
        PhysicsBody? ignore = null)
    {
        RaycastResult closest = RaycastResult.None;

        // 정적 맵
        if ((mask & CollisionLayer.Map) != 0 && StaticWorld != null)
        {
            var mapHit = StaticWorld.Raycast(ray, maxDistance);
            if (mapHit.Hit)
            {
                closest = mapHit;
                maxDistance = mapHit.Distance;
            }
        }

        // 동적 바디
        var bodyHit = Raycast(ray, maxDistance, mask & ~CollisionLayer.Map, ignore);
        if (bodyHit.Hit)
            closest = bodyHit;

        return closest;
    }

    /// <summary>개별 바디에 대한 레이캐스트</summary>
    static RaycastResult RaycastBody(Ray ray, PhysicsBody body)
        => body.Shape.Raycast(ray, body.Position, body.Rotation);
}
