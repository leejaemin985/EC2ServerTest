using LJMCollision;

/// <summary>
/// 물리 바디가 사용하는 충돌 형태 인터페이스.
/// 각 Shape이 위치 변환, 맵 충돌, 레이캐스트를 직접 처리한다.
/// </summary>
public interface ICollisionShape
{
    ShapeType Type { get; }

    /// <summary>발 위치에서 center까지의 Y 오프셋</summary>
    float BottomOffset { get; }

    /// <summary>Substep 분할 기준이 되는 수평 반경</summary>
    float HorizontalRadius { get; }

    /// <summary>feet 위치를 center 위치로 변환</summary>
    Vec3 ToWorldCenter(Vec3 feetPosition);

    /// <summary>맵 OBB와 충돌하며 이동. 최종 center 위치와 바닥 정보를 반환.</summary>
    MoveResult MoveAndSlide(CollisionWorld world, Vec3 feetPosition, Quat rotation, Vec3 displacement, float maxSlopeAngle = 45f);

    /// <summary>맵 OBB와 겹침 검사만. 밀어냄 없이 충돌 정보만 반환.</summary>
    OverlapResult OverlapTest(CollisionWorld world, Vec3 feetPosition, Quat rotation, Vec3 displacement);

    /// <summary>이 Shape에 대한 레이캐스트</summary>
    RaycastResult Raycast(Ray ray, Vec3 feetPosition, Quat rotation);
}

public enum ShapeType : byte
{
    Sphere,
    Capsule,
    Box,
}

/// <summary>Shape 공통 유틸리티</summary>
static class ShapeUtils
{
    /// <summary>substep MoveAndSlide. 이동량이 stepSize보다 크면 자동 분할.</summary>
    /// <param name="moveFunc">center와 stepVelocity를 받아 단일 스텝 MoveAndSlide 실행</param>
    public static MoveResult SubstepMoveAndSlide(
        Vec3 center, Vec3 displacement, float stepSize,
        Func<Vec3, Vec3, MoveResult> moveFunc)
    {
        float dist = displacement.Magnitude;
        int steps = Math.Max(1, (int)MathF.Ceiling(dist / stepSize));
        Vec3 stepVel = displacement * (1f / steps);
        bool grounded = false;
        bool hitCeiling = false;
        bool hitWall = false;
        Vec3 groundNormal = Vec3.Up;
        Vec3 wallNormal = Vec3.Zero;

        for (int s = 0; s < steps; s++)
        {
            var result = moveFunc(center, stepVel);
            center = result.Position;
            if (result.Grounded) { grounded = true; groundNormal = result.GroundNormal; }
            if (result.HitCeiling) hitCeiling = true;
            if (result.HitWall) { hitWall = true; wallNormal = result.WallNormal; }
        }

        return new MoveResult
        {
            Position = center, Grounded = grounded, GroundNormal = groundNormal,
            HitCeiling = hitCeiling, HitWall = hitWall, WallNormal = wallNormal
        };
    }
}

/// <summary>구체 형태 (투사체 등)</summary>
public class SphereShape : ICollisionShape
{
    public ShapeType Type => ShapeType.Sphere;
    public float Radius { get; set; }
    public float BottomOffset => Radius;
    public float HorizontalRadius => Radius;

    public SphereShape(float radius) => Radius = radius;

    public Sphere ToSphere(Vec3 position) => new(position, Radius);

    public Vec3 ToWorldCenter(Vec3 feetPosition)
        => new(feetPosition.X, feetPosition.Y + Radius, feetPosition.Z);

    public MoveResult MoveAndSlide(CollisionWorld world, Vec3 feetPosition, Quat rotation, Vec3 displacement, float maxSlopeAngle = 45f)
        => ShapeUtils.SubstepMoveAndSlide(ToWorldCenter(feetPosition), displacement, Radius,
            (center, stepVel) => world.MoveAndSlide(new Sphere(center, Radius), stepVel, maxSlopeAngle));

    public OverlapResult OverlapTest(CollisionWorld world, Vec3 feetPosition, Quat rotation, Vec3 displacement)
        => world.OverlapTest(new Sphere(ToWorldCenter(feetPosition), Radius), displacement);

    public RaycastResult Raycast(Ray ray, Vec3 feetPosition, Quat rotation)
        => CollisionDetection.RayVsSphere(ray, new Sphere(ToWorldCenter(feetPosition), Radius));
}

/// <summary>캡슐 형태 (플레이어 등)</summary>
public class CapsuleShape : ICollisionShape
{
    public ShapeType Type => ShapeType.Capsule;
    public float Radius { get; set; }
    public float Height { get; set; }
    public float BottomOffset => Height * 0.5f;
    public float HorizontalRadius => Radius;

    public CapsuleShape(float radius, float height)
    {
        Radius = radius;
        Height = height;
    }

    public Capsule ToCapsule(Vec3 feetPosition)
        => new(ToWorldCenter(feetPosition), Radius, Height);

    public Capsule ToCapsule(Vec3 feetPosition, Quat rotation)
        => new(ToWorldCenter(feetPosition), Radius, Height, rotation.Rotate(Vec3.Up));

    public Vec3 ToWorldCenter(Vec3 feetPosition)
        => new(feetPosition.X, feetPosition.Y + Height * 0.5f, feetPosition.Z);

    public MoveResult MoveAndSlide(CollisionWorld world, Vec3 feetPosition, Quat rotation, Vec3 displacement, float maxSlopeAngle = 45f)
    {
        Vec3 dir = rotation.Rotate(Vec3.Up);
        return ShapeUtils.SubstepMoveAndSlide(ToWorldCenter(feetPosition), displacement, Radius,
            (center, stepVel) => world.MoveAndSlide(new Capsule(center, Radius, Height, dir), stepVel, maxSlopeAngle));
    }

    public OverlapResult OverlapTest(CollisionWorld world, Vec3 feetPosition, Quat rotation, Vec3 displacement)
        => world.OverlapTest(new Capsule(ToWorldCenter(feetPosition), Radius, Height, rotation.Rotate(Vec3.Up)), displacement);

    public RaycastResult Raycast(Ray ray, Vec3 feetPosition, Quat rotation)
        => CollisionDetection.RayVsCapsule(ray, ToCapsule(feetPosition));
}

/// <summary>박스 형태 (구조물 등)</summary>
public class BoxShape : ICollisionShape
{
    public ShapeType Type => ShapeType.Box;
    public Vec3 HalfSize { get; set; }
    public float BottomOffset => HalfSize.Y;
    public float HorizontalRadius => MathF.Max(HalfSize.X, HalfSize.Z);

    public BoxShape(Vec3 halfSize) => HalfSize = halfSize;

    public OBB ToOBB(Vec3 position, Quat rotation)
        => new OBB(position, HalfSize, rotation);

    public Vec3 ToWorldCenter(Vec3 feetPosition)
        => new(feetPosition.X, feetPosition.Y + HalfSize.Y, feetPosition.Z);

    public MoveResult MoveAndSlide(CollisionWorld world, Vec3 feetPosition, Quat rotation, Vec3 displacement, float maxSlopeAngle = 45f)
        => ShapeUtils.SubstepMoveAndSlide(ToWorldCenter(feetPosition), displacement,
            MathF.Min(HalfSize.X, MathF.Min(HalfSize.Y, HalfSize.Z)),
            (center, stepVel) => world.MoveAndSlide(new OBB(center, HalfSize, rotation), stepVel, maxSlopeAngle));

    public OverlapResult OverlapTest(CollisionWorld world, Vec3 feetPosition, Quat rotation, Vec3 displacement)
        => world.OverlapTest(new OBB(ToWorldCenter(feetPosition), HalfSize, rotation), displacement);

    public RaycastResult Raycast(Ray ray, Vec3 feetPosition, Quat rotation)
        => CollisionDetection.RayVsOBB(ray, new OBB(ToWorldCenter(feetPosition), HalfSize, rotation));
}
