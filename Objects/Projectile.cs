using LJMCollision;

public class Projectile : NetworkObject
{
    public override NetworkObjectType ObjectType => NetworkObjectType.Projectile;

    public PhysicsBody? Body { get; set; }

    protected Projectile(uint netId, GameLoop loop, Transform? transform = null)
        : base(netId, loop, transform) { }

    protected internal override void Update(float deltaTime)
    {
        // TODO: 투사체 로직 추후 구현
    }
}
