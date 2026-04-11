using System.Text.Json;
using LJMCollision;

/// <summary>
/// bake된 애니메이션 JSON을 로드하고, 프레임/bone 기준으로 position/rotation을 조회한다.
/// </summary>
public class BakedAnimationData
{
    public string ClipName { get; }
    public int TickRate { get; }
    public int FrameCount { get; }
    public string[] Bones { get; }

    // [frame][bone] = Vec3/Quat
    readonly Vec3[][] _positions;
    readonly Quat[][] _rotations;
    readonly Dictionary<string, int> _boneIndex;

    BakedAnimationData(string clipName, int tickRate, int frameCount, string[] bones,
        Vec3[][] positions, Quat[][] rotations)
    {
        ClipName = clipName;
        TickRate = tickRate;
        FrameCount = frameCount;
        Bones = bones;
        _positions = positions;
        _rotations = rotations;

        _boneIndex = new Dictionary<string, int>(bones.Length);
        for (int i = 0; i < bones.Length; i++)
            _boneIndex[bones[i]] = i;
    }

    /// <summary>특정 프레임의 bone position (root 기준 상대 좌표)</summary>
    public Vec3 GetPosition(int frame, int boneIndex)
        => _positions[frame % FrameCount][boneIndex];

    /// <summary>특정 프레임의 bone rotation (root 기준 상대 회전)</summary>
    public Quat GetRotation(int frame, int boneIndex)
        => _rotations[frame % FrameCount][boneIndex];

    /// <summary>bone 이름으로 인덱스 조회. 없으면 -1.</summary>
    public int GetBoneIndex(string boneName)
        => _boneIndex.TryGetValue(boneName, out int idx) ? idx : -1;

    /// <summary>JSON 파일에서 로드</summary>
    public static BakedAnimationData FromFile(string path)
    {
        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string clipName = root.GetProperty("clipName").GetString()!;
        int tickRate = root.GetProperty("tickRate").GetInt32();
        int frameCount = root.GetProperty("frameCount").GetInt32();

        // bones
        var bonesArr = root.GetProperty("bones");
        var bones = new string[bonesArr.GetArrayLength()];
        for (int i = 0; i < bones.Length; i++)
            bones[i] = bonesArr[i].GetString()!;

        int boneCount = bones.Length;

        // frames
        var framesArr = root.GetProperty("frames");
        var positions = new Vec3[frameCount][];
        var rotations = new Quat[frameCount][];

        for (int f = 0; f < frameCount; f++)
        {
            var frame = framesArr[f];
            var posArr = frame.GetProperty("pos");
            var rotArr = frame.GetProperty("rot");

            positions[f] = new Vec3[boneCount];
            rotations[f] = new Quat[boneCount];

            for (int b = 0; b < boneCount; b++)
            {
                var p = posArr[b];
                positions[f][b] = new Vec3(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle());

                var r = rotArr[b];
                rotations[f][b] = new Quat(r[0].GetSingle(), r[1].GetSingle(), r[2].GetSingle(), r[3].GetSingle());
            }
        }

        return new BakedAnimationData(clipName, tickRate, frameCount, bones, positions, rotations);
    }
}
