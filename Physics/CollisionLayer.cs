/// <summary>
/// 충돌 레이어 (비트마스크).
/// </summary>
[Flags]
public enum CollisionLayer : ushort
{
    None        = 0,
    Map         = 1 << 0,   // 정적 지형 (벽, 바닥)
    Player      = 1 << 1,   // 플레이어
    Projectile  = 1 << 2,   // 투사체
    // Destructible = 1 << 3,
    // Shield       = 1 << 4,
    // Trigger      = 1 << 5,

    All = 0xFFFF,
}

/// <summary>
/// 레이어 간 충돌 가능 여부를 관리하는 매트릭스.
/// 16개 레이어 (ushort 비트) 대응. 대칭 구조 — SetCollision(A,B)는 B↔A도 동시 설정.
/// </summary>
public class LayerMatrix
{
    // _matrix[i] = i번 레이어가 충돌 가능한 레이어들의 비트마스크
    readonly ushort[] _matrix = new ushort[16];

    /// <summary>모든 레이어 쌍이 충돌하도록 초기화</summary>
    public LayerMatrix()
    {
        for (int i = 0; i < 16; i++)
            _matrix[i] = 0xFFFF;
    }

    /// <summary>두 레이어 간 충돌 여부 설정 (대칭)</summary>
    public void SetCollision(CollisionLayer a, CollisionLayer b, bool enabled)
    {
        int idxA = BitIndex(a);
        int idxB = BitIndex(b);
        if (idxA < 0 || idxB < 0) return;

        if (enabled)
        {
            _matrix[idxA] |= (ushort)(1 << idxB);
            _matrix[idxB] |= (ushort)(1 << idxA);
        }
        else
        {
            _matrix[idxA] &= (ushort)~(1 << idxB);
            _matrix[idxB] &= (ushort)~(1 << idxA);
        }
    }

    /// <summary>두 레이어가 충돌 가능한지 확인</summary>
    public bool CanCollide(CollisionLayer a, CollisionLayer b)
    {
        int idxA = BitIndex(a);
        if (idxA < 0) return false;
        return (_matrix[idxA] & (ushort)b) != 0;
    }

    static int BitIndex(CollisionLayer layer)
    {
        ushort val = (ushort)layer;
        if (val == 0) return -1;
        int idx = 0;
        while ((val >> idx & 1) == 0) idx++;
        return idx;
    }
}
