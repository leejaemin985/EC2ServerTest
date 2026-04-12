using LJMCollision;

public enum HitboxShapeType { Sphere, Capsule, OBB }

public struct BoneHitbox
{
    public string Bone;
    public HitboxShapeType Type;
    public Vec3 Offset;      // bone 기준 local offset

    // Sphere
    public float Radius;

    // Capsule
    public float Height;
    public int Direction;    // 0=X, 1=Y, 2=Z

    // OBB
    public Vec3 Size;        // full size
}
