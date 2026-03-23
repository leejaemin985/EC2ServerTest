/// <summary>
/// 서버/클라이언트 양쪽에서 공유하는 오브젝트 종류 식별자.
/// 클라이언트는 이 값을 보고 어떤 프리팹을 생성할지 결정한다.
/// </summary>
public enum NetworkObjectType : ushort
{
    None = 0,
    Player = 1,
    Movable = 2,
}
