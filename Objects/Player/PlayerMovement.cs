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

        // 입력 정규화 (대각선 이동 시 속도 초과 방지)
        float h = input.H;
        float v = input.V;
        float inputLen = MathF.Sqrt(h * h + v * v);
        if (inputLen > 1f) { h /= inputLen; v /= inputLen; }

        float rad = input.Yaw * MathF.PI / 180f;
        float sin = MathF.Sin(rad);
        float cos = MathF.Cos(rad);
        float moveX = h * cos + v * sin;
        float moveZ = -h * sin + v * cos;
        Vec3 moveDir = new Vec3(moveX, 0f, moveZ) * _player.MoveSpeed;

        // grounded + 경사면일 때: 이동 방향을 바닥 평면에 투영
        if (body.Grounded && body.GroundNormal.Y < 0.999f)
        {
            Vec3 n = body.GroundNormal;
            // velocity를 ground plane에 투영: v - dot(v, n) * n
            float dot = moveDir.X * n.X + moveDir.Y * n.Y + moveDir.Z * n.Z;
            Vec3 projected = new Vec3(
                moveDir.X - dot * n.X,
                moveDir.Y - dot * n.Y,
                moveDir.Z - dot * n.Z);

            // 투영 후 속력 보존 (경사면에서도 동일한 이동 속도)
            float origLen = MathF.Sqrt(moveDir.X * moveDir.X + moveDir.Z * moveDir.Z);
            float projLen = MathF.Sqrt(projected.X * projected.X + projected.Y * projected.Y + projected.Z * projected.Z);
            if (projLen > 0.001f)
                projected = projected * (origLen / projLen);

            body.Velocity = projected;
        }
        else
        {
            body.Velocity = new Vec3(moveDir.X, body.Velocity.Y, moveDir.Z);
        }
    }
}
