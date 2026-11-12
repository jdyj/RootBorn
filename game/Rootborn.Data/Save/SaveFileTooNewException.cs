using System.IO;

namespace Rootborn.Data.Save
{
    /// <summary>이 프로그램이 아는 것보다 높은 형식 버전의 세이브. 옛 프로그램이 새 세이브를 망가뜨리지 않게 읽기를 거절한다.</summary>
    public sealed class SaveFileTooNewException : IOException, Rootborn.Core.Save.ISaveFileTooNew
    {
        public SaveFileTooNewException(string path, int fileVersion, int knownVersion)
            : base($"세이브 파일의 형식 버전 {fileVersion}이 이 프로그램이 아는 {knownVersion}보다 높다: {path}")
        {
            Path = path;
            FileVersion = fileVersion;
            KnownVersion = knownVersion;
        }

        public string Path { get; }
        public int FileVersion { get; }
        public int KnownVersion { get; }
    }
}
