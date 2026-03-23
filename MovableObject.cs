/// <summary>
/// 입력을 받아 이동하는 테스트용 NetworkObject.
/// 외부에서 SetInput()으로 입력을 넣으면 Update에서 Position을 갱신한다.
/// </summary>
public class MovableObject : NetworkObject
{
    public override NetworkObjectType ObjectType => NetworkObjectType.Movable;

    public float MoveSpeed { get; set; } = 5f;

    private float _inputH;
    private float _inputV;

    protected MovableObject(uint netId, GameLoop loop, NetworkTransform? transform = null)
        : base(netId, loop, transform) { }

    /// <summary>외부(네트워크 수신 등)에서 입력값을 세팅한다.</summary>
    public void SetInput(float h, float v)
    {
        _inputH = h;
        _inputV = v;
    }

    protected internal override void Update(float deltaTime)
    {
        if (_inputH == 0f && _inputV == 0f) return;

        var pos = Position;
        pos.X += _inputH * MoveSpeed * deltaTime;
        pos.Z += _inputV * MoveSpeed * deltaTime;
        Position = pos;

        // 입력 소비
        _inputH = 0f;
        _inputV = 0f;
    }
}
