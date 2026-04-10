namespace InGame.Unit.Player.States;

public class FallState : PlayerState
{
    public override PlayerStateType StateType => PlayerStateType.Fall;

    public override void Update(Player player, float deltaTime)
    {
        var input = player.Input;

        if (player.Grounded)
        {
            if (input.H != 0f || input.V != 0f)
                player.Fsm.ChangeState(player.MoveState);
            else
                player.Fsm.ChangeState(player.IdleState);
            return;
        }

        // 공중 이동
        player.Movement.ApplyRotation(input);
        player.Movement.ApplyMovement(input);
        input.ConsumeOneShot();
    }
}
