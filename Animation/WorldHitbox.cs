using LJMCollision;

/// <summary>월드 공간에 배치된 개별 hitbox</summary>
public struct WorldHitbox
{
    public string Bone;
    public HitboxShapeType Type;
    public Vec3 Center;
    public Quat Rotation;

    // shape별 파라미터
    public float Radius;     // Sphere, Capsule
    public float Height;     // Capsule
    public int Direction;    // Capsule (0=X, 1=Y, 2=Z)
    public Vec3 HalfSize;    // OBB
}
