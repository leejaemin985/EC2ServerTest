/// <summary>
/// bake된 애니메이션 클립들을 로드하고 이름으로 관리한다.
/// 서버 시작 시 한 번 로드하면 이후 읽기 전용으로 사용.
/// </summary>
public class AnimationClipManager
{
    readonly Dictionary<string, BakedAnimationData> _clips = new();

    public IReadOnlyDictionary<string, BakedAnimationData> Clips => _clips;

    /// <summary>단일 클립 파일 로드</summary>
    public void Load(string filePath)
    {
        var clip = BakedAnimationData.FromFile(filePath);
        _clips[clip.ClipName] = clip;
        Console.WriteLine($"[AnimClip] Loaded: {clip.ClipName} ({clip.FrameCount} frames, {clip.TickRate}Hz)");
    }

    /// <summary>폴더 내 모든 애니메이션 JSON 일괄 로드 (hitbox_defs 제외)</summary>
    public void LoadFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Console.WriteLine($"[AnimClip] Folder not found: {folderPath}");
            return;
        }

        foreach (var file in Directory.GetFiles(folderPath, "*.json"))
        {
            // hitbox 정의 파일은 건너뜀
            if (Path.GetFileName(file).StartsWith("hitbox_")) continue;

            try { Load(file); }
            catch (Exception e) { Console.WriteLine($"[AnimClip] Failed to load {file}: {e.Message}"); }
        }
    }

    /// <summary>클립 이름으로 조회. 없으면 null.</summary>
    public BakedAnimationData? Get(string clipName)
        => _clips.TryGetValue(clipName, out var clip) ? clip : null;
}
