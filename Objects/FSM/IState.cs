namespace InGame.FSM;

public interface IState<TOwner>
{
    void Enter(TOwner owner);
    void Update(TOwner owner, float deltaTime);
    void Exit(TOwner owner);
}
