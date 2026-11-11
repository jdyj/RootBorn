namespace Rootborn.Core.Save
{
    /// <summary>
    /// 세이브 파일을 읽고 쓰는 곳 하나. 영역 코드는 어느 슬롯의 어느 파일에 무엇을 쓰는지만 안다.
    /// 파일 형식(JSON 등)과 위치는 구현이 정한다. 파일 이름은 <see cref="SaveFileNames"/>에서 만든다.
    /// </summary>
    public interface ISaveStore
    {
        /// <summary>
        /// 파일이 없으면 false. 파일이 있는데 읽을 수 없으면(비었거나 깨짐, 더 새 버전) 예외를 던진다.
        /// 깨진 파일을 "세이브 없음"으로 보고 새 게임으로 덮어쓰지 않게 하기 위해서다.
        /// </summary>
        bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new();

        void Write<T>(string saveSlot, string fileName, T data) where T : class;

        /// <summary>없는 파일을 지워도 오류가 아니다.</summary>
        void Delete(string saveSlot, string fileName);
    }

    /// <summary>
    /// 파일 맨 위 세이브 모양이 구현한다. Version은 파일의 형식 버전이고, 0은 "Unity 판이 쓴 파일"이다.
    /// 필드 이름이 "Version" 그대로여야 하므로 모양은 공개 필드를 두고 이 인터페이스를 명시적으로 구현한다.
    /// </summary>
    public interface IVersionedSave
    {
        int Version { get; set; }

        /// <summary>이 프로그램이 아는 그 모양의 가장 높은 버전. 이보다 높은 파일은 읽지 않는다.</summary>
        int KnownVersion { get; }
    }
}
