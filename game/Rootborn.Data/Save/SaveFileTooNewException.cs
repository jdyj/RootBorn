using System.IO;

namespace Rootborn.Data.Save
{
    /// <summary>파일이 이 프로그램이 아는 것보다 높은 형식 버전부터 읽을 수 있다고 적었다(ReadableFrom). 읽지 않고 덮어쓰지 않는다.</summary>
    public sealed class SaveFileTooNewException : IOException, Rootborn.Core.Save.ISaveFileTooNew
    {
        public SaveFileTooNewException(string path, int fileVersion, int knownVersion)
            : base($"세이브 파일(형식 버전 {fileVersion})이 이 프로그램이 아는 {knownVersion}보다 새 프로그램을 요구한다: {path}")
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
