using System;
using System.IO;

namespace Rootborn.Data.Tests
{
    /// <summary>테스트 실행 폴더에서 위로 올라가며 data/registry.json이 있는 저장소 뿌리를 찾는다.</summary>
    internal static class RepoPaths
    {
        private static readonly Lazy<string> _root = new Lazy<string>(FindRoot);

        public static string Root => _root.Value;
        public static string DataDirectory => Path.Combine(Root, "data");

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "data", "registry.json"))) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException($"data/registry.json을 {AppContext.BaseDirectory} 위에서 찾지 못했다");
        }
    }
}
