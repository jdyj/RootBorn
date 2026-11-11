using System;
using System.IO;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    internal static class TestData
    {
        private static readonly Lazy<string> _root = new Lazy<string>(FindRoot);

        public static string RepoRoot => _root.Value;
        public static string DataDirectory => Path.Combine(RepoRoot, "data");
        public static string LocalizationDirectory => Path.Combine(DataDirectory, "localization");

        /// <summary>실제 ko.json·en.json. 테스트마다 새로 만든다(언어와 빠진 키 목록이 테스트끼리 섞이지 않게).</summary>
        public static GameText Text(string language = "ko") => GameText.Load(LocalizationDirectory, language);

        private static string FindRoot()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "data", "registry.json"))) return dir.FullName;
            throw new DirectoryNotFoundException("data/registry.json을 찾지 못했다");
        }
    }

    /// <summary>글자 하나의 폭을 글자 크기의 절반으로 치는 측정기.</summary>
    internal sealed class TestMeasurer : ITextMeasurer
    {
        public float Width(string text, float fontSize) => (text?.Length ?? 0) * fontSize * 0.5f;
        public float LineHeight(float fontSize) => fontSize;
    }
}
