using LJMCollision;

namespace InGame.Unit.Player.States;

public class MoveState : PlayerState
{
    public override PlayerStateType StateType => PlayerStateType.Move;

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

        // 이동 입력이 없으면 IdleState로 전환
        if (input.H == 0f && input.V == 0f)
        {
            player.Fsm.ChangeState(player.IdleState);
            return;
        }

        // 입력 방향에 따라 애니메이션 전환
        if (player.Animator != null)
        {
            string clip = (MathF.Abs(input.V) >= MathF.Abs(input.H))
                ? (input.V > 0 ? "Root_Aim_Run_F" : "Root_Aim_Run_B")
                : (input.H > 0 ? "Root_Aim_Run_R" : "Root_Aim_Run_L");
            player.Animator.Play(clip, player.Loop.CurrentTick);
        }

        player.Movement.ApplyRotation(input);
        player.Movement.ApplyMovement(input);
        input.ConsumeOneShot();
    }
}
