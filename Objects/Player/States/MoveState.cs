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

        player.Movement.ApplyRotation(input);
        player.Movement.ApplyMovement(input);
        input.ConsumeOneShot();
    }
}
