using LJMCollision;

/// <summary>
/// 플레이어 단위 애니메이션 상태 관리.
/// 현재 클립, 경과 틱을 추적하고 매 틱 bone transform을 제공한다.
/// </summary>
public class BoneAnimator
{
    readonly AnimationClipManager _clipManager;

    BakedAnimationData? _currentClip;
    int _elapsedTicks;
    bool _loop = true;

    public BakedAnimationData? CurrentClip => _currentClip;

    /// <summary>현재 프레임 인덱스</summary>
    public int CurrentFrame
    {
        get
        {
            if (_currentClip == null) return 0;
            if (_loop)
                return _elapsedTicks % _currentClip.FrameCount;
            return Math.Min(_elapsedTicks, _currentClip.FrameCount - 1);
        }
    }

    /// <summary>재생 완료 여부 (루프가 아닌 경우)</summary>
    public bool IsFinished
    {
        get
        {
            if (_currentClip == null || _loop) return false;
            return _elapsedTicks >= _currentClip.FrameCount;
        }
    }

    public BoneAnimator(AnimationClipManager clipManager)
    {
        _clipManager = clipManager;
    }

    /// <summary>클립 전환. 이름이 같으면 무시.</summary>
    public void Play(string clipName, bool loop = true)
    {
        var clip = _clipManager.Get(clipName);
        if (clip == null || _currentClip == clip) return;

        _currentClip = clip;
        _elapsedTicks = 0;
        _loop = loop;
    }

    /// <summary>강제 클립 전환 (같은 클립이어도 처음부터 재생)</summary>
    public void ForcePlay(string clipName, bool loop = true)
    {
        var clip = _clipManager.Get(clipName);
        if (clip == null) return;

        _currentClip = clip;
        _elapsedTicks = 0;
        _loop = loop;
    }

    /// <summary>매 틱 호출. 1틱 진행.</summary>
    public void Tick()
    {
        if (_currentClip == null) return;
        _elapsedTicks++;
    }

    /// <summary>현재 프레임의 bone position (root 기준 상대 좌표)</summary>
    public Vec3 GetBonePosition(int boneIndex)
    {
        if (_currentClip == null) return Vec3.Zero;
        return _currentClip.GetPosition(CurrentFrame, boneIndex);
    }

    /// <summary>현재 프레임의 bone rotation (root 기준 상대 회전)</summary>
    public Quat GetBoneRotation(int boneIndex)
    {
        if (_currentClip == null) return Quat.Identity;
        return _currentClip.GetRotation(CurrentFrame, boneIndex);
    }
}
