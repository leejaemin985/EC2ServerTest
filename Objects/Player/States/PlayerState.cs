using InGame.FSM;

namespace InGame.Unit.Player.States;

public abstract class PlayerState : IState<Player>
{
    public abstract PlayerStateType StateType { get; }

    public virtual void Enter(Player player) { }
    public virtual void Update(Player player, float deltaTime) { }
    public virtual void Exit(Player player) { }
}
