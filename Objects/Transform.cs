using LJMCollision;

/// <summary>
/// 공간 정보를 담는 클래스. Position(위치)과 Rotation(회전)을 포함한다.
/// PhysicsBody와 NetworkObject가 같은 인스턴스를 공유하여 동기화 없이 동작한다.
/// </summary>
public class Transform
{
    public Vec3 Position;
    public Quat Rotation;

    public Transform()
    {
        Position = new Vec3(0, 0, 0);
        Rotation = Quat.Identity;
    }

    public Transform(Vec3 position, Quat rotation)
    {
        Position = position;
        Rotation = rotation;
    }
}
