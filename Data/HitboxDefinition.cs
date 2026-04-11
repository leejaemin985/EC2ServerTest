using System.Text.Json;
using LJMCollision;

/// <summary>
/// bone별 hitbox shape 정의. Sphere/Capsule/OBB를 지원한다.
/// </summary>
public class HitboxDefinition
{
    public enum HitboxShapeType { Sphere, Capsule, OBB }

    public struct BoneHitbox
    {
        public string Bone;
        public HitboxShapeType Type;
        public Vec3 Offset;      // bone 기준 local offset

        // Sphere
        public float Radius;

        // Capsule
        public float Height;
        public int Direction;    // 0=X, 1=Y, 2=Z

        // OBB
        public Vec3 Size;        // full size
    }

    public BoneHitbox[] Hitboxes { get; }
    readonly Dictionary<string, int> _index;

    HitboxDefinition(BoneHitbox[] hitboxes)
    {
        Hitboxes = hitboxes;
        _index = new Dictionary<string, int>(hitboxes.Length);
        for (int i = 0; i < hitboxes.Length; i++)
            _index[hitboxes[i].Bone] = i;
    }

    /// <summary>bone 이름으로 hitbox 조회. 없으면 null.</summary>
    public BoneHitbox? Get(string boneName)
        => _index.TryGetValue(boneName, out int idx) ? Hitboxes[idx] : null;

    /// <summary>JSON 파일에서 로드</summary>
    public static HitboxDefinition FromFile(string path)
    {
        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var arr = root.GetProperty("hitboxes");
        var hitboxes = new BoneHitbox[arr.GetArrayLength()];

        for (int i = 0; i < hitboxes.Length; i++)
        {
            var el = arr[i];
            var o = el.GetProperty("offset");
            string typeStr = el.GetProperty("type").GetString()!;

            var hb = new BoneHitbox
            {
                Bone = el.GetProperty("bone").GetString()!,
                Offset = new Vec3(o[0].GetSingle(), o[1].GetSingle(), o[2].GetSingle()),
            };

            switch (typeStr)
            {
                case "Sphere":
                    hb.Type = HitboxShapeType.Sphere;
                    hb.Radius = el.GetProperty("radius").GetSingle();
                    break;

                case "Capsule":
                    hb.Type = HitboxShapeType.Capsule;
                    hb.Radius = el.GetProperty("radius").GetSingle();
                    hb.Height = el.GetProperty("height").GetSingle();
                    hb.Direction = el.TryGetProperty("direction", out var dir) ? dir.GetInt32() : 1;
                    break;

                case "OBB":
                default:
                    hb.Type = HitboxShapeType.OBB;
                    var s = el.GetProperty("size");
                    hb.Size = new Vec3(s[0].GetSingle(), s[1].GetSingle(), s[2].GetSingle());
                    break;
            }

            hitboxes[i] = hb;
        }

        return new HitboxDefinition(hitboxes);
    }
}
