using System.Text.Json;
using LJMCollision;

/// <summary>
/// 총기 데이터. JSON에서 로드하여 서버에서 사용한다.
/// 클라이언트 Unity 에디터에서 bake → JSON export → 서버 로드.
/// </summary>
public class WeaponData
{
    /// <summary>무기 식별자 (예: "Rifle_01")</summary>
    public string Id { get; init; } = "";

    /// <summary>발사 간격 (초). 0이면 제한 없음.</summary>
    public float FireRate { get; init; }

    /// <summary>기본 데미지</summary>
    public int Damage { get; init; }

    /// <summary>단발 여부. false면 연사 가능.</summary>
    public bool SingleShot { get; init; }

    /// <summary>투사체 속도 (m/s)</summary>
    public float BulletSpeed { get; init; }

    /// <summary>투사체 반지름</summary>
    public float BulletRadius { get; init; }

    /// <summary>투사체 최대 수명 (초)</summary>
    public float BulletLifetime { get; init; }

    /// <summary>총구 위치 — 무기 root 기준 상대 좌표</summary>
    public Vec3 MuzzleOffset { get; init; }

    /// <summary>오른손 그립 위치 — 무기 root 기준 상대 좌표</summary>
    public Vec3 RightHandOffset { get; init; }
    /// <summary>오른손 그립 회전</summary>
    public Quat RightHandRotation { get; init; }

    /// <summary>왼손 그립 위치 — 무기 root 기준 상대 좌표</summary>
    public Vec3 LeftHandOffset { get; init; }
    /// <summary>왼손 그립 회전</summary>
    public Quat LeftHandRotation { get; init; }

    /// <summary>JSON 파일에서 로드</summary>
    public static WeaponData FromFile(string path)
    {
        string json = File.ReadAllText(path);
        return FromJson(json);
    }

    /// <summary>JSON 문자열에서 로드</summary>
    public static WeaponData FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new WeaponData
        {
            Id = root.GetProperty("id").GetString() ?? "",
            FireRate = root.GetProperty("fireRate").GetSingle(),
            Damage = root.GetProperty("damage").GetInt32(),
            SingleShot = root.TryGetProperty("singleShot", out var ss) && ss.GetBoolean(),
            BulletSpeed = root.GetProperty("bulletSpeed").GetSingle(),
            BulletRadius = root.TryGetProperty("bulletRadius", out var br) ? br.GetSingle() : 0.1f,
            BulletLifetime = root.TryGetProperty("bulletLifetime", out var bl) ? bl.GetSingle() : 5f,
            MuzzleOffset = ReadVec3(root, "muzzleOffset"),
            RightHandOffset = ReadVec3(root, "rightHandOffset"),
            RightHandRotation = ReadQuat(root, "rightHandRotation"),
            LeftHandOffset = ReadVec3(root, "leftHandOffset"),
            LeftHandRotation = ReadQuat(root, "leftHandRotation"),
        };
    }

    static Vec3 ReadVec3(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)) return Vec3.Zero;
        return new Vec3(el[0].GetSingle(), el[1].GetSingle(), el[2].GetSingle());
    }

    static Quat ReadQuat(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)) return Quat.Identity;
        return new Quat(el[0].GetSingle(), el[1].GetSingle(), el[2].GetSingle(), el[3].GetSingle());
    }

    /// <summary>폴더 내 모든 무기 JSON 로드</summary>
    public static Dictionary<string, WeaponData> LoadFolder(string folderPath)
    {
        var weapons = new Dictionary<string, WeaponData>();
        if (!Directory.Exists(folderPath)) return weapons;

        foreach (var file in Directory.GetFiles(folderPath, "*.json"))
        {
            try
            {
                var data = FromFile(file);
                weapons[data.Id] = data;
                Console.WriteLine($"[Weapon] Loaded: {data.Id}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Weapon] Failed to load {file}: {e.Message}");
            }
        }
        return weapons;
    }
}
