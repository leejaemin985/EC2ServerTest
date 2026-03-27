using LJMCollision;

public class Player : NetworkObject
{
    public override NetworkObjectType ObjectType => NetworkObjectType.Player;

    public float MoveSpeed { get; set; } = PlayerData.MoveSpeed;
    public float CapsuleRadius { get; set; } = PlayerData.CapsuleRadius;
    public float CapsuleHeight { get; set; } = PlayerData.CapsuleHeight;
    public float JumpForce { get; set; } = PlayerData.JumpForce;

    public PhysicsBody? Body { get; set; }

    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public bool Grounded => Body?.Grounded ?? false;
    public Vec3 AirMoveVelocity { get; set; }

    public Vec3 CapsuleCenter =>
        new(Position.X, Position.Y + CapsuleHeight * 0.5f, Position.Z);

    protected Player(uint netId, GameLoop loop, Transform? transform = null)
        : base(netId, loop, transform) { }

    public void SetInput(float h, float v, float yaw, float pitch, bool jump = false)
    {
        Yaw = yaw;
        Pitch = pitch;

        if (Body == null) return;

        // 점프: Player가 직접 Velocity.Y 설정
        if (jump && Grounded)
        {
            Body.Velocity = new Vec3(Body.Velocity.X, JumpForce, Body.Velocity.Z);
        }

        float rad = yaw * MathF.PI / 180f;
        float sinY = MathF.Sin(rad * 0.5f);
        float cosY = MathF.Cos(rad * 0.5f);
        Rotation = new Quat(0f, sinY, 0f, cosY);

        float sin = MathF.Sin(rad);
        float cos = MathF.Cos(rad);
        float moveX = h * cos + v * sin;
        float moveZ = -h * sin + v * cos;
        Vec3 inputVelocity = new Vec3(moveX, 0f, moveZ) * MoveSpeed;

        Body.Velocity = new Vec3(inputVelocity.X, Body.Velocity.Y, inputVelocity.Z);
    }

    protected internal override void Update(float deltaTime)
    {
    }
}
