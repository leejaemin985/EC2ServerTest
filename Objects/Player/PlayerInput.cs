namespace InGame.Unit.Player;

/// <summary>
/// 클라이언트에서 수신한 입력값을 저장하는 컴포넌트.
/// 로직 없이 데이터만 보관한다.
/// </summary>
public class PlayerInput
{
    public float H { get; private set; }
    public float V { get; private set; }
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public bool Jump { get; private set; }

    public void Set(float h, float v, float yaw, float pitch, bool jump)
    {
        H = h;
        V = v;
        Yaw = yaw;
        Pitch = pitch;
        Jump = jump;
    }

    /// <summary>매 틱 끝에 일회성 입력(점프 등)을 초기화한다.</summary>
    public void ConsumeOneShot()
    {
        Jump = false;
    }
}
