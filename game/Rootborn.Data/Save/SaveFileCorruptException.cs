using System;
using System.IO;

namespace Rootborn.Data.Save
{
    /// <summary>세이브 파일이 있는데 비었거나 JSON이 깨져 읽을 수 없다. "세이브 없음"과 다르다. 파일은 건드리지 않는다.</summary>
    public sealed class SaveFileCorruptException : IOException
    {
        public SaveFileCorruptException(string path, string reason, Exception inner = null)
            : base($"세이브 파일을 읽을 수 없다: {path} ({reason})", inner)
        {
            Path = path;
        }

        public string Path { get; }
    }
}
