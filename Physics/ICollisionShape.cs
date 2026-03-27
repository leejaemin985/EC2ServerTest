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

    /// <summary>GroundCheck용 수평 반경</summary>
    float HorizontalRadius { get; }

    /// <summary>feet 위치를 center 위치로 변환</summary>
    Vec3 ToWorldCenter(Vec3 feetPosition);

    /// <summary>맵 OBB와 충돌하며 이동. 최종 center 위치와 바닥 정보를 반환.</summary>
    MoveResult MoveAndSlide(CollisionWorld world, Vec3 feetPosition, Quat rotation, Vec3 displacement, float maxSlopeAngle = 45f);

    /// <summary>이 Shape에 대한 레이캐스트</summary>
    RaycastResult Raycast(Ray ray, Vec3 feetPosition, Quat rotation);
}

public enum ShapeType : byte
{
    Sphere,
    Capsule,
    Box,
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
    {
        float dist = displacement.Magnitude;
        int steps = Math.Max(1, (int)MathF.Ceiling(dist / Radius));
        Vec3 stepVel = displacement * (1f / steps);
        Vec3 center = ToWorldCenter(feetPosition);
        bool grounded = false;
        bool hitCeiling = false;
        Vec3 groundNormal = Vec3.Up;

        for (int s = 0; s < steps; s++)
        {
            var result = world.MoveAndSlide(new Sphere(center, Radius), stepVel, maxSlopeAngle);
            center = result.Position;
            if (result.Grounded) { grounded = true; groundNormal = result.GroundNormal; }
            if (result.HitCeiling) hitCeiling = true;
        }

        return new MoveResult { Position = center, Grounded = grounded, GroundNormal = groundNormal, HitCeiling = hitCeiling };
    }

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
    {
        Vec3 center = new(feetPosition.X, feetPosition.Y + Height * 0.5f, feetPosition.Z);
        return new Capsule(center, Radius, Height);
    }

    public Vec3 ToWorldCenter(Vec3 feetPosition)
        => new(feetPosition.X, feetPosition.Y + Height * 0.5f, feetPosition.Z);

    public MoveResult MoveAndSlide(CollisionWorld world, Vec3 feetPosition, Quat rotation, Vec3 displacement, float maxSlopeAngle = 45f)
    {
        float dist = displacement.Magnitude;
        int steps = Math.Max(1, (int)MathF.Ceiling(dist / Radius));
        Vec3 stepVel = displacement * (1f / steps);
        Vec3 center = ToWorldCenter(feetPosition);
        bool grounded = false;
        bool hitCeiling = false;
        Vec3 groundNormal = Vec3.Up;

        for (int s = 0; s < steps; s++)
        {
            var result = world.MoveAndSlide(new Capsule(center, Radius, Height), stepVel, maxSlopeAngle);
            center = result.Position;
            if (result.Grounded) { grounded = true; groundNormal = result.GroundNormal; }
            if (result.HitCeiling) hitCeiling = true;
        }

        return new MoveResult { Position = center, Grounded = grounded, GroundNormal = groundNormal, HitCeiling = hitCeiling };
    }

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
    {
        float dist = displacement.Magnitude;
        float minSize = MathF.Min(HalfSize.X, MathF.Min(HalfSize.Y, HalfSize.Z));
        int steps = Math.Max(1, (int)MathF.Ceiling(dist / minSize));
        Vec3 stepVel = displacement * (1f / steps);
        Vec3 center = ToWorldCenter(feetPosition);
        bool grounded = false;
        bool hitCeiling = false;
        Vec3 groundNormal = Vec3.Up;

        for (int s = 0; s < steps; s++)
        {
            var result = world.MoveAndSlide(new OBB(center, HalfSize, rotation), stepVel, maxSlopeAngle);
            center = result.Position;
            if (result.Grounded) { grounded = true; groundNormal = result.GroundNormal; }
            if (result.HitCeiling) hitCeiling = true;
        }

        return new MoveResult { Position = center, Grounded = grounded, GroundNormal = groundNormal, HitCeiling = hitCeiling };
    }

    public RaycastResult Raycast(Ray ray, Vec3 feetPosition, Quat rotation)
        => CollisionDetection.RayVsOBB(ray, new OBB(ToWorldCenter(feetPosition), HalfSize, rotation));
}
