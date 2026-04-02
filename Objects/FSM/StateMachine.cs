namespace InGame.FSM;

public class StateMachine<TOwner>
{
    private readonly TOwner _owner;
    private IState<TOwner>? _current;

    public IState<TOwner>? Current => _current;

    /// <summary>상태 전환 시 호출되는 콜백. (owner, newState)</summary>
    public Action<TOwner, IState<TOwner>>? OnStateChanged;

    public StateMachine(TOwner owner)
    {
        _owner = owner;
    }

    public void ChangeState(IState<TOwner> next)
    {
        _current?.Exit(_owner);
        _current = next;
        _current.Enter(_owner);
        OnStateChanged?.Invoke(_owner, next);
    }

    public void Update(float deltaTime)
    {
        _current?.Update(_owner, deltaTime);
    }
}
