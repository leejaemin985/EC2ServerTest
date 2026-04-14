/// <summary>
/// 무기 데이터를 중앙 관리하는 static 매니저.
/// 서버 시작 시 1회 Init 호출 후 어디서든 조회 가능.
/// </summary>
public static class WeaponManager
{
    static Dictionary<string, WeaponData>? _weapons;

    public static bool IsInitialized => _weapons != null;

    public static void Init(string folder)
    {
        _weapons = WeaponData.LoadFolder(folder);
    }

    public static WeaponData? Get(string id)
        => _weapons != null && _weapons.TryGetValue(id, out var data) ? data : null;
}
