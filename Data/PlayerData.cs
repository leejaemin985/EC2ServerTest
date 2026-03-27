/// <summary>플레이어 수치 데이터</summary>
public static class PlayerData
{
    // ── 크기 ──
    public const float CapsuleRadius = 0.5f;
    public const float CapsuleHeight = 1.8f;

    // ── 시점 (클라이언트 카메라 오프셋과 동일하게 유지) ──
    public const float EyeOffsetX = 0f;
    public const float EyeOffsetY = 0.5f;   // 클라 카메라 로컬 오프셋
    public const float EyeOffsetZ = 0f;

    // ── 이동 ──
    public const float MoveSpeed = 10f;
    public const float JumpForce = 20f;
    public const float MaxSlopeAngle = 45f;

    // ── 물리 ──
    public const float Mass = 70f;
    public const float Drag = 0.85f;
}
