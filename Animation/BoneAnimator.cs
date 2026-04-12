using LJMCollision;

/// <summary>
/// 플레이어 단위 애니메이션 상태 관리.
/// 클립 전환 시점의 틱을 기록하고, 조회 시 경과 틱으로 프레임을 계산한다.
/// </summary>
public class BoneAnimator
{
    readonly AnimationClipManager _clipManager;

    BakedAnimationData? _currentClip;
    int _startTick;
    bool _loop = true;

    public BakedAnimationData? CurrentClip => _currentClip;

    /// <summary>특정 틱 시점의 프레임 인덱스를 계산한다.</summary>
    public int GetFrame(int currentTick)
    {
        if (_currentClip == null) return 0;
        int elapsed = currentTick - _startTick;
        if (_loop)
            return elapsed % _currentClip.FrameCount;
        return Math.Min(elapsed, _currentClip.FrameCount - 1);
    }

    /// <summary>재생 완료 여부 (루프가 아닌 경우)</summary>
    public bool IsFinished(int currentTick)
    {
        if (_currentClip == null || _loop) return false;
        return (currentTick - _startTick) >= _currentClip.FrameCount;
    }

    public BoneAnimator(AnimationClipManager clipManager)
    {
        _clipManager = clipManager;
    }

    /// <summary>클립 전환. 이름이 같으면 무시.</summary>
    public void Play(string clipName, int currentTick, bool loop = true)
    {
        var clip = _clipManager.Get(clipName);
        if (clip == null || _currentClip == clip) return;

        _currentClip = clip;
        _startTick = currentTick;
        _loop = loop;
    }

    /// <summary>강제 클립 전환 (같은 클립이어도 처음부터 재생)</summary>
    public void ForcePlay(string clipName, int currentTick, bool loop = true)
    {
        var clip = _clipManager.Get(clipName);
        if (clip == null) return;

        _currentClip = clip;
        _startTick = currentTick;
        _loop = loop;
    }

    /// <summary>특정 틱 시점의 bone position (root 기준 상대 좌표)</summary>
    public Vec3 GetBonePosition(int currentTick, int boneIndex)
    {
        if (_currentClip == null) return Vec3.Zero;
        return _currentClip.GetPosition(GetFrame(currentTick), boneIndex);
    }

    /// <summary>특정 틱 시점의 bone rotation (root 기준 상대 회전)</summary>
    public Quat GetBoneRotation(int currentTick, int boneIndex)
    {
        if (_currentClip == null) return Quat.Identity;
        return _currentClip.GetRotation(GetFrame(currentTick), boneIndex);
    }
}
