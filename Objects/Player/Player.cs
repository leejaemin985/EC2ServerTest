using InGame.FSM;
using InGame.Unit.Player.States;

namespace InGame.Unit.Player;

public class Player : NetworkObject
{
    public override NetworkObjectType ObjectType => NetworkObjectType.Player;

    internal float MoveSpeed { get; } = PlayerData.MoveSpeed;
    internal float CapsuleRadius { get; } = PlayerData.CapsuleRadius;
    internal float CapsuleHeight { get; } = PlayerData.CapsuleHeight;
    internal float JumpForce { get; } = PlayerData.JumpForce;

    public PhysicsBody? Body { get; internal set; }

    internal PlayerInput Input { get; } = new();
    internal PlayerMovement Movement { get; private set; } = null!;
    internal StateMachine<Player> Fsm { get; private set; } = null!;

    // 상태 인스턴스 (미리 할당)
    internal MoveState MoveState { get; } = new();
    internal FallState FallState { get; } = new();

    internal bool Grounded => Body?.Grounded ?? false;

    protected Player(uint netId, GameLoop loop, Transform? transform = null)
        : base(netId, loop, transform) { }

    public void SetInput(float h, float v, float yaw, float pitch, bool jump)
    {
        Input.Set(h, v, yaw, pitch, jump);
    }

    protected internal override void Awake()
    {
        Movement = new PlayerMovement(this);
        Fsm = new StateMachine<Player>(this);
        Fsm.ChangeState(MoveState);
    }

    protected internal override void Update(float deltaTime)
    {
        Fsm.Update(deltaTime);
    }
}
