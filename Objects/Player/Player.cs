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

    public PhysicsBody? Body { get; private set; }

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
        Fsm = new StateMachine<Player>(this, MoveState);

        // FSM 상태변경 → 패킷 큐에 추가
        Fsm.OnStateChanged = (p, state) =>
        {
            if (state is PlayerState ps)
            {
                EnqueuePacket(new PlayerStatePacket
                {
                    Tick = Loop.CurrentTick,
                    NetId = NetId,
                    State = (byte)ps.StateType,
                });
            }
        };

        // 초기 상태 전파
        if (Fsm.Current is PlayerState initState)
        {
            EnqueuePacket(new PlayerStatePacket
            {
                Tick = Loop.CurrentTick,
                NetId = NetId,
                State = (byte)initState.StateType,
            });
        }

        if (Loop.PhysicsWorld is { } world)
        {
            Body = new PhysicsBody(
                Transform, BodyType.Dynamic,
                new CapsuleShape(CapsuleRadius, CapsuleHeight),
                CollisionLayer.Player)
            {
                Mass = PlayerData.Mass,
                UseGravity = true,
                Drag = PlayerData.Drag,
                MaxSlopeAngle = PlayerData.MaxSlopeAngle,
                UserData = this,
            };
            world.Add(Body);
        }
    }

    protected internal override void Update(float deltaTime)
    {
        Fsm.Update(deltaTime);
    }

    protected internal override void OnDestroy()
    {
        if (Body != null)
            Loop.PhysicsWorld?.Remove(Body);
    }
}
