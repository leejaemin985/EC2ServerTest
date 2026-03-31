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
    private PlayerMovement _movement = null!;

    internal bool Grounded => Body?.Grounded ?? false;

    protected Player(uint netId, GameLoop loop, Transform? transform = null)
        : base(netId, loop, transform) { }

    public void SetInput(float h, float v, float yaw, float pitch, bool jump)
    {
        Input.Set(h, v, yaw, pitch, jump);
    }

    protected internal override void Awake()
    {
        _movement = new PlayerMovement(this);
    }

    protected internal override void Update(float deltaTime)
    {
        _movement.Update(deltaTime);
    }
}
