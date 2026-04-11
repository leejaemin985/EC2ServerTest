using LJMCollision;

namespace InGame.Unit.Player.States;

public class IdleState : PlayerState
{
    public override PlayerStateType StateType => PlayerStateType.Idle;

    public override void Enter(Player player)
    {
        // 수평 속도 즉시 정지
        if (player.Body != null)
            player.Body.Velocity = new Vec3(0f, player.Body.Velocity.Y, 0f);

        player.Animator?.Play("Root_Aim_Idle");
    }

    public override void Update(Player player, float deltaTime)
    {
        var input = player.Input;

        if (!player.Grounded)
        {
            player.Fsm.ChangeState(player.FallState);
            return;
        }

        if (input.Jump)
        {
            if (player.Body != null)
                player.Body.Velocity = new Vec3(player.Body.Velocity.X, player.JumpForce, player.Body.Velocity.Z);
            player.Fsm.ChangeState(player.FallState);
            return;
        }

        // 이동 입력이 들어오면 MoveState로 전환
        if (input.H != 0f || input.V != 0f)
        {
            player.Fsm.ChangeState(player.MoveState);
            return;
        }

        player.Movement.ApplyRotation(input);
        input.ConsumeOneShot();
    }
}
