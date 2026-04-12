/// <summary>
/// 애니메이션 클립 + hitbox 정의를 중앙 관리하는 static 매니저.
/// 서버 시작 시 1회 Init 호출 후, 어디서든 조회 가능.
/// </summary>
public static class AnimationManager
{
    static AnimationClipManager? _clips;
    static HitboxDefinition? _hitboxDefs;

    public static AnimationClipManager Clips
        => _clips ?? throw new InvalidOperationException("AnimationManager not initialized");

    public static HitboxDefinition? HitboxDefs => _hitboxDefs;

    public static bool IsInitialized => _clips != null;

    /// <summary>애니메이션 폴더에서 클립 + hitbox 정의를 로드한다.</summary>
    public static void Init(string animFolder, string hitboxFileName = "hitbox_defs.json")
    {
        _clips = new AnimationClipManager();
        _clips.LoadFolder(animFolder);

        string hitboxPath = Path.Combine(animFolder, hitboxFileName);
        if (File.Exists(hitboxPath))
            _hitboxDefs = HitboxDefinition.FromFile(hitboxPath);
    }
}
