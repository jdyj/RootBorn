namespace Rootborn.Core.Save
{
    /// <summary>세이브 형식 버전의 공통 값. 모양마다 아는 최고 버전은 그 모양의 KnownVersion 상수에 둔다.</summary>
    public static class SaveVersion
    {
        /// <summary>Version 필드가 없던 Unity 판 파일.</summary>
        public const int Unity = 0;

        /// <summary>MonoGame 판이 처음 쓰는 버전.</summary>
        public const int Current = 1;
    }
}
