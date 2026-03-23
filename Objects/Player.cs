using LJMCollision;

public class Player : NetworkObject
{
    public override NetworkObjectType ObjectType => NetworkObjectType.Player;

    public float MoveSpeed { get; set; } = 5f;
    public float Gravity { get; set; } = -15f;

    // 캡슐 설정
    public float CapsuleRadius { get; set; } = 0.5f;
    public float CapsuleHeight { get; set; } = 1.8f;

    // 충돌 월드 참조 (Room에서 세팅)
    public CollisionWorld? World { get; set; }

    float _inputH;
    float _inputV;
    float _velocityY;
    bool _grounded;

    protected Player(uint netId, GameLoop loop, NetworkTransform? transform = null)
        : base(netId, loop, transform) { }

    public void SetInput(float h, float v)
    {
        _inputH = h;
        _inputV = v;
    }

    protected internal override void Update(float deltaTime)
    {
        Vec3 pos = Position;
        LJMCollision.Vec3 center = new(pos.X, pos.Y + CapsuleHeight * 0.5f, pos.Z);

        // 수평 이동
        LJMCollision.Vec3 velocity = new(
            _inputH * MoveSpeed * deltaTime,
            0f,
            _inputV * MoveSpeed * deltaTime);

        // 중력
        if (!_grounded)
            _velocityY += Gravity * deltaTime;
        velocity = new LJMCollision.Vec3(velocity.X, _velocityY * deltaTime, velocity.Z);

        // 충돌 처리
        if (World != null)
        {
            var capsule = new Capsule(center, CapsuleRadius, CapsuleHeight);
            LJMCollision.Vec3 newCenter = World.MoveAndSlide(capsule, velocity);

            // 바닥 체크
            float feetY = newCenter.Y - CapsuleHeight * 0.5f;
            if (World.GroundCheck(new LJMCollision.Vec3(newCenter.X, feetY + 0.1f, newCenter.Z), CapsuleRadius, out float groundY))
            {
                if (feetY <= groundY)
                {
                    newCenter = new LJMCollision.Vec3(newCenter.X, groundY + CapsuleHeight * 0.5f, newCenter.Z);
                    _velocityY = 0f;
                    _grounded = true;
                }
                else
                {
                    _grounded = false;
                }
            }
            else
            {
                _grounded = false;
            }

            Position = new Vec3(newCenter.X, newCenter.Y - CapsuleHeight * 0.5f, newCenter.Z);
        }
        else
        {
            // 충돌 월드 없으면 그냥 이동
            pos.X += velocity.X;
            pos.Y += velocity.Y;
            pos.Z += velocity.Z;
            Position = pos;
        }

        _inputH = 0f;
        _inputV = 0f;
    }
}
