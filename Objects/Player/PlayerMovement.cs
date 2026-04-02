using LJMCollision;

namespace InGame.Unit.Player;

/// <summary>
/// 입력값을 기반으로 실제 물리 이동을 처리하는 컴포넌트.
/// Player의 PhysicsBody와 Transform을 조작한다.
/// </summary>
public class PlayerMovement
{
    private readonly Player _player;

    public PlayerMovement(Player player)
    {
        _player = player;
    }

    public void ApplyRotation(PlayerInput input)
    {
        float rad = input.Yaw * MathF.PI / 180f;
        float sinY = MathF.Sin(rad * 0.5f);
        float cosY = MathF.Cos(rad * 0.5f);
        _player.Rotation = new Quat(0f, sinY, 0f, cosY);
    }

    public void ApplyMovement(PlayerInput input)
    {
        var body = _player.Body;
        if (body == null) return;

        float rad = input.Yaw * MathF.PI / 180f;
        float sin = MathF.Sin(rad);
        float cos = MathF.Cos(rad);
        float moveX = input.H * cos + input.V * sin;
        float moveZ = -input.H * sin + input.V * cos;
        Vec3 inputVelocity = new Vec3(moveX, 0f, moveZ) * _player.MoveSpeed;

        body.Velocity = new Vec3(inputVelocity.X, body.Velocity.Y, inputVelocity.Z);
    }
}
