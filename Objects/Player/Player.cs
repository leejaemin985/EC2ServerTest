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

    internal BoneAnimator? Animator { get; private set; }
    internal HitboxSkeleton? Skeleton { get; private set; }
    internal Weapon? CurrentWeapon { get; private set; }

    internal PlayerInput Input { get; } = new();
    internal PlayerMovement Movement { get; private set; } = null!;
    internal StateMachine<Player> Fsm { get; private set; } = null!;

    // 상태 인스턴스 (미리 할당)
    internal IdleState IdleState { get; } = new();
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
        InitMovement();
        InitFsm();
        InitAnimation();
        InitPhysicsBody();
        InitWeapon();
    }

    void InitMovement()
    {
        Movement = new PlayerMovement(this);
    }

    void InitFsm()
    {
        Fsm = new StateMachine<Player>(this, IdleState);

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
    }

    void InitAnimation()
    {
        if (!AnimationManager.IsInitialized) return;

        if (AnimationManager.ClipManager.Clips.Count > 0)
            Animator = new BoneAnimator(AnimationManager.ClipManager);

        if (Animator != null && AnimationManager.HitboxDefs is { } hitboxDefs)
            Skeleton = new HitboxSkeleton(Animator, hitboxDefs);
    }

    void InitWeapon()
    {
        var weaponData = WeaponManager.Get("TestRifle");
        if (weaponData == null) return;

        CurrentWeapon = new Weapon(weaponData, Loop.Objects, Loop.PhysicsWorld);
        EnqueuePacket(new WeaponEquipPacket
        {
            Tick = Loop.CurrentTick,
            NetId = NetId,
            WeaponId = weaponData.Id,
        });
    }

    void InitPhysicsBody()
    {
        if (Loop.PhysicsWorld is not { } world) return;

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

    protected internal override void Update(float deltaTime)
    {
        CurrentWeapon?.Update(deltaTime);
        Fsm.Update(deltaTime);
    }

    /// <summary>특정 틱 시점의 월드 공간 hitbox 목록</summary>
    public List<WorldHitbox>? EvaluateHitboxes(int tick)
        => Skeleton?.Evaluate(tick, Position, Rotation);

    protected internal override void HandlePacket(PacketType type, PacketReader reader)
    {
        switch (type)
        {
            case PacketType.Input:
                if (reader.Remaining < 16) return;
                float h = reader.ReadFloat();
                float v = reader.ReadFloat();
                float yaw = reader.ReadFloat();
                float pitch = reader.ReadFloat();
                bool jump = reader.Remaining >= 1 && reader.ReadByte() != 0;
                SetInput(h, v, yaw, pitch, jump);
                break;

            case PacketType.Shoot:
                HandleShoot(reader);
                break;
        }
    }

    void HandleShoot(PacketReader reader)
    {
        if (CurrentWeapon == null || reader.Remaining < 8) return;

        float yaw = reader.ReadFloat();
        float pitch = reader.ReadFloat();

        CurrentWeapon.Fire(NetId, Position, Rotation, yaw, pitch);
    }

    protected internal override void OnDestroy()
    {
        if (Body != null)
            Loop.PhysicsWorld?.Remove(Body);
    }
}
