/// <summary>
/// 3D 벡터. 위치, 방향 등에 사용.
/// </summary>
public struct Vec3
{
    public float X;
    public float Y;
    public float Z;

    public Vec3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vec3 Zero => new(0, 0, 0);
    public static Vec3 One => new(1, 1, 1);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 v, float s) => new(v.X * s, v.Y * s, v.Z * s);
    public static Vec3 operator *(float s, Vec3 v) => v * s;

    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}

/// <summary>
/// 쿼터니언. 회전 표현에 사용.
/// </summary>
public struct Quat
{
    public float X;
    public float Y;
    public float Z;
    public float W;

    public Quat(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public static Quat Identity => new(0, 0, 0, 1);

    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2}, {W:F2})";
}

/// <summary>
/// NetworkObject의 공간 정보를 담는 구조체.
/// Position(위치)과 Rotation(회전)을 포함한다.
/// </summary>
public struct NetworkTransform
{
    public Vec3 Position;
    public Quat Rotation;

    public NetworkTransform()
    {
        Position = Vec3.Zero;
        Rotation = Quat.Identity;
    }

    public NetworkTransform(Vec3 position, Quat rotation)
    {
        Position = position;
        Rotation = rotation;
    }
}
