using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.Host.Rendering;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 창 없이 도는 테스트: --root·환경 변수가 없을 때 저장소 루트를 찾는 표지. 자동 테스트.
    /// 표지는 게임 데이터(data/registry.json)다 — 서버(ServerOptions)와 테스트 도우미와 같다.
    /// 그림 폴더(Assets/…)는 새 그림이 오면 사라지므로 표지로 쓰지 않는다.
    /// </summary>
    public sealed class HostOptionsRootTests
    {
        private string _temp;

        [SetUp]
        public void SetUp()
        {
            _temp = Path.Combine(Path.GetTempPath(), "rootborn-host-root-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temp);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temp)) Directory.Delete(_temp, true);
        }

        [Test]
        public void Resolve_FindsRootByGameData_WithoutUiPictureFolder()
        {
            string repo = RealRepoRoot();
            string root = Path.Combine(_temp, "copy");
            // 게임 데이터 표지와 기본 테마(그 그림 포함)만 있는 폴더. Assets/modernuserinterface-win 은 없다.
            Directory.CreateDirectory(Path.Combine(root, "data"));
            File.Copy(Path.Combine(repo, "data", "registry.json"), Path.Combine(root, "data", "registry.json"));
            string themeRel = Path.Combine("game", "themes", HostOptions.DefaultUiTheme, ThemeFolder.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, themeRel)));
            string themeText = File.ReadAllText(Path.Combine(repo, themeRel));
            File.WriteAllText(Path.Combine(root, themeRel), themeText);
            foreach (Match m in Regex.Matches(themeText, "\"" + ThemeFolder.RepoPrefix + "([^\"]+)\""))
            {
                string rel = m.Groups[1].Value;
                string to = Path.Combine(root, rel);
                if (File.Exists(to)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(Path.Combine(repo, rel), to);
            }
            string font = Path.Combine(_temp, "font.ttf");
            File.WriteAllText(font, "");
            string start = Path.Combine(root, "bin", "deep");
            Directory.CreateDirectory(start);
            Assume.That(Directory.Exists(Path.Combine(root, "Assets", "modernuserinterface-win")), Is.False);

            HostOptions o = HostOptions.Resolve(new[] { "--font", font }, _ => null, start);

            Assert.That(Path.GetFullPath(o.RepoRoot), Is.EqualTo(Path.GetFullPath(root)));
        }

        [Test]
        public void Resolve_FolderWithoutGameData_IsNotRoot()
        {
            string start = Path.Combine(_temp, "empty", "bin");
            Directory.CreateDirectory(start);
            // 그림 폴더만 있고 게임 데이터가 없는 곳은 루트가 아니다.
            Directory.CreateDirectory(Path.Combine(_temp, "empty", "Assets", "modernuserinterface-win"));
            string font = Path.Combine(_temp, "font.ttf");
            File.WriteAllText(font, "");

            var e = Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--font", font }, _ => null, start));
            Assert.That(e.Message, Does.Contain(HostOptions.RootVariable));
        }

        private static string RealRepoRoot()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "data", "registry.json"))) return d.FullName;
            throw new InvalidOperationException("저장소를 찾지 못했다(data/registry.json)");
        }
    }
}
