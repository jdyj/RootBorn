using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 지문이 셈하는 범위: 규칙에 쓰이는 데이터만(등록부, 엔티티 파일, data 옆의 game/economy.json).
    /// 그림 좌표표(data/atlases), 변환 보고서, 문구(data/localization)는 셈하지 않는다 — 배포마다 들어가는 몫이 다르고,
    /// 문구가 달라도(언어가 다른 두 사람) 같은 방에 들어갈 수 있어야 한다.
    /// 클라이언트 배포(Tools/publish/content.txt)와 서버 배포(content-server.txt)의 data/ 줄로 모은 폴더가 같은 지문을 내야 한다.
    /// </summary>
    public sealed class DataFingerprintScopeTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-fingerprint-scope", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void ClientAndServerPublishLists_GiveTheSameFingerprint()
        {
            string client = CopyPublished("content.txt", "client");
            string server = CopyPublished("content-server.txt", "server");
            Assert.That(RelativeFiles(server), Is.Not.EquivalentTo(RelativeFiles(client)),
                "이 시험의 전제: 두 배포의 data/ 파일 묶음이 다르다(클라이언트는 문구, 저장소는 좌표표·보고서까지)");

            string clientPrint = DataFingerprint.Compute(Path.Combine(client, "data"));
            Assert.That(DataFingerprint.Compute(Path.Combine(server, "data")), Is.EqualTo(clientPrint));
            Assert.That(DataFingerprint.Compute(RepoPaths.DataDirectory), Is.EqualTo(clientPrint), "저장소에서 돈 서버·게임과도 같다");
        }

        [Test]
        public void OneCharacterOfRuleData_ChangesIt()
        {
            string data = SmallData();
            string before = DataFingerprint.Compute(data);
            File.WriteAllText(Path.Combine(data, "items", "ItemDefinition.json"), "[{\"type\":\"ItemDefinition\",\"id\":\"b\"}]\n");
            Assert.That(DataFingerprint.Compute(data), Is.Not.EqualTo(before), "엔티티 파일");

            string afterItem = DataFingerprint.Compute(data);
            File.WriteAllText(Path.Combine(data, "registry.json"), "{\"items\":[\"b\"]}\n");
            Assert.That(DataFingerprint.Compute(data), Is.Not.EqualTo(afterItem), "등록부");

            string afterRegistry = DataFingerprint.Compute(data);
            File.WriteAllText(Path.Combine(_root, "game", "economy.json"), "{\"currencyItem\":\"b\"}\n");
            Assert.That(DataFingerprint.Compute(data), Is.Not.EqualTo(afterRegistry), "game/economy.json");
        }

        [Test]
        public void AtlasesReportAndText_DoNotChangeIt()
        {
            string data = SmallData();
            string before = DataFingerprint.Compute(data);

            File.WriteAllText(Path.Combine(data, "atlases", "Assets", "x.png.atlas.json"), "{\"image\":\"Assets/y.png\"}\n");
            File.WriteAllText(Path.Combine(data, "atlases", "Assets", "new.png.atlas.json"), "{}\n");
            File.WriteAllText(Path.Combine(data, "localization", "ko.json"), "{\"ui.a\":\"다른 문구\"}\n");
            File.WriteAllText(Path.Combine(data, "localization", "fr.json"), "{\"ui.a\":\"b\"}\n");
            File.Delete(Path.Combine(data, "export-report.json"));
            Assert.That(DataFingerprint.Compute(data), Is.EqualTo(before));
        }

        [Test]
        public void LineEndingsAndBom_DoNotChangeIt_OnRealData()
        {
            string data = CopyPublished("content-server.txt", "lf");
            string lf = DataFingerprint.Compute(Path.Combine(data, "data"));
            foreach (var file in Directory.GetFiles(_root + "/lf", "*.json", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file).Replace("\r\n", "\n").Replace("\n", "\r\n");
                File.WriteAllText(file, text, new UTF8Encoding(true));
            }

            Assert.That(DataFingerprint.Compute(Path.Combine(data, "data")), Is.EqualTo(lf), "Windows에서 만든 배포(CRLF·BOM)와 Linux에서 만든 배포(LF)");
        }

        [Test]
        public void MissingEconomyFile_IsADifferentFingerprint()
        {
            string data = SmallData();
            string with = DataFingerprint.Compute(data);
            File.Delete(Path.Combine(_root, "game", "economy.json"));
            Assert.That(DataFingerprint.Compute(data), Is.Not.EqualTo(with), "통화가 있는 판과 없는 판은 규칙이 다르다");
        }

        private string SmallData()
        {
            string data = Path.Combine(_root, "data");
            Directory.CreateDirectory(Path.Combine(data, "items"));
            Directory.CreateDirectory(Path.Combine(data, "atlases", "Assets"));
            Directory.CreateDirectory(Path.Combine(data, "localization"));
            Directory.CreateDirectory(Path.Combine(_root, "game"));
            File.WriteAllText(Path.Combine(data, "registry.json"), "{\"items\":[\"a\"]}\n");
            File.WriteAllText(Path.Combine(data, "items", "ItemDefinition.json"), "[{\"type\":\"ItemDefinition\",\"id\":\"a\"}]\n");
            File.WriteAllText(Path.Combine(data, "atlases", "Assets", "x.png.atlas.json"), "{\"image\":\"Assets/x.png\"}\n");
            File.WriteAllText(Path.Combine(data, "localization", "ko.json"), "{\"ui.a\":\"가\"}\n");
            File.WriteAllText(Path.Combine(data, "export-report.json"), "{\"types\":1}\n");
            File.WriteAllText(Path.Combine(_root, "game", "economy.json"), "{\"currencyItem\":\"a\"}\n");
            return data;
        }

        /// <summary>
        /// 배포 목록의 평범한 줄과 `!` 줄 가운데 data/·game/*.json 에 해당하는 것만 적용해 그 파일들을 복사한다.
        /// `@refs` 가 따라오게 하는 좌표표는 그림 몫이라 더하지 않는다 — 클라이언트 쪽이 좌표표를 가장 적게 갖는 경우다.
        /// </summary>
        private string CopyPublished(string listName, string name)
        {
            string target = Path.Combine(_root, name);
            string repo = RepoPaths.Root;
            var lines = File.ReadAllLines(Path.Combine(repo, "Tools", "publish", listName))
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal) && !l.StartsWith("@", StringComparison.Ordinal))
                .ToList();

            var candidates = Directory.GetFiles(Path.Combine(repo, "data"), "*.json", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(Path.Combine(repo, "game"), "*.json", SearchOption.TopDirectoryOnly))
                .Select(p => Path.GetRelativePath(repo, p).Replace('\\', '/'))
                .ToList();

            var chosen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in lines)
            {
                bool remove = line.StartsWith("!", StringComparison.Ordinal);
                var pattern = GlobToRegex(remove ? line.Substring(1) : line);
                foreach (var file in candidates.Where(f => pattern.IsMatch(f)))
                {
                    if (remove) chosen.Remove(file);
                    else chosen.Add(file);
                }
            }

            Assert.That(chosen.Any(f => f == "data/registry.json"), Is.True, listName + "가 data/registry.json 을 넣지 않는다");
            foreach (var file in chosen)
            {
                string to = Path.Combine(target, file);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(Path.Combine(repo, file), to);
            }

            return target;
        }

        private static string[] RelativeFiles(string folder)
        {
            string data = Path.Combine(folder, "data");
            return Directory.GetFiles(data, "*.json", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(data, p).Replace('\\', '/'))
                .ToArray();
        }

        private static Regex GlobToRegex(string glob)
        {
            var sb = new StringBuilder("^");
            for (int i = 0; i < glob.Length; i++)
            {
                char c = glob[i];
                if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
                {
                    sb.Append(".*");
                    i++;
                    if (i + 1 < glob.Length && glob[i + 1] == '/') i++;
                }
                else if (c == '*') sb.Append("[^/]*");
                else if (c == '?') sb.Append("[^/]");
                else sb.Append(Regex.Escape(c.ToString()));
            }

            if (glob.EndsWith("/", StringComparison.Ordinal)) sb.Append(".*");
            return new Regex(sb.Append("(/.*)?$").ToString());
        }
    }
}
