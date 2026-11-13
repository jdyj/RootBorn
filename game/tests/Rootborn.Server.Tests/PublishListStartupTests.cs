using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Screens;
using Rootborn.World;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 배포 목록이 시작할 때 읽는 필수 파일을 다 고르는가(2026-11-12: game/time.json 이 빠져 배포한 게임이 시작하지 못했고,
    /// 맵 데이터가 빠져 배포한 서버에서 같이 쓰는 집의 가구 놓기가 모두 거절됐다 — 둘 다 개발 폴더에서는 드러나지 않았다).
    /// 목록이 고르는 데이터·설정·맵(그림 빼고, <see cref="PublishListCopy"/>)만 저장소 밖 임시 폴더에 복사하고 그 폴더를 루트로 삼아
    /// 클라이언트가 시작할 때 읽는 것(정의, 게임 한 판, 문구, 맵 목록과 맵, 상호작용·조명·연출, 테마 설정)과 서버가 읽는 것(정의, 서버 한 번 켜고 끄기,
    /// 같이 쓰는 집의 판정 칸)을 읽어 본다. 자동 테스트다 — 배포 폴더 자체의 실행은 Tools/publish/verify.sh 가 본다.
    /// </summary>
    [NonParallelizable]
    public sealed class PublishListStartupTests
    {
        private string _root;
        private static string Repo => ServerProcessTests.RepoRoot();

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-publish-list", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private string CopyOf(string listName)
        {
            string target = Path.Combine(_root, Path.GetFileNameWithoutExtension(listName));
            var files = PublishListCopy.Copy(Repo, listName, target);
            Assert.That(files, Does.Contain("data/registry.json"), listName + " 가 data/registry.json 을 고르지 않는다");
            return target;
        }

        private static void AssertDataLoads(string root, string listName)
        {
            var load = GameDataLoader.Load(Path.Combine(root, "data"), RootbornBuilders.Register);
            Assert.That(load.Ok, Is.True, listName + " 로 모은 폴더에서 정의를 읽지 못했다:\n"
                + string.Join("\n", load.Errors.Select(e => e.ToString()).Concat(load.UnclassifiedTypes.Select(t => "분류 안 된 종류 " + t))));
        }

        [Test]
        public void ClientList_HasEverythingTheGameReadsAtStart()
        {
            string game = CopyOf("content.txt");
            AssertDataLoads(game, "content.txt");

            // 게임 한 판(새 게임). 하루 시작 시각은 game/time.json 에서 온다.
            var session = GameSession.StartFromData(Path.Combine(game, "data"), new FileSaveStore(Path.Combine(_root, "save")), "slot-1", null);
            Assert.That(session.Definitions.TimeRules, Is.Not.Null, "시간 규칙(game/time.json)");
            Assert.That(session.ClockMinutes, Is.EqualTo(session.Definitions.TimeRules.DayStartMinutes), "새 게임은 하루 시작 시각에 시작한다");

            // 문구: HostGame 이 읽는 두 폴더. 저장소에 있는 언어가 다 있다.
            var text = GameText.LoadMerged(GameText.DefaultBaseLanguage, Path.Combine(game, "data", "localization"), Path.Combine(game, "game", "localization"));
            var repoText = GameText.LoadMerged(GameText.DefaultBaseLanguage, Path.Combine(Repo, "data", "localization"), Path.Combine(Repo, "game", "localization"));
            Assert.That(text.Languages, Is.EquivalentTo(repoText.Languages), "언어 파일");

            // 월드 화면이 읽는 것(WorldScreen): 맵 목록과 모든 맵(LDtk), 상호작용 지점, 조명, 연출.
            var maps = WorldMapLoader.FromRepo(game);
            Assert.That(maps.Catalog.Maps, Is.Not.Empty);
            foreach (var entry in maps.Catalog.Maps)
                Assert.DoesNotThrow(() => maps.LoadMap(entry.Id), "맵 " + entry.Id + " (" + entry.Ldtk + ")");
            Assert.DoesNotThrow(() => InteractionData.Load(Path.Combine(game, InteractionData.DefaultRelativePath)), "상호작용 지점");
            Assert.DoesNotThrow(() => WorldLightingData.Load(Path.Combine(game, WorldLightingData.DefaultRelativePath)), "조명");
            Assert.DoesNotThrow(() => WorldPresentation.Load(Path.Combine(game, WorldPresentation.DefaultRelativePath)), "연출");

            // UI 테마(Host ThemeFolder 가 game/themes/<이름>/theme.json 을 읽는다). 저장소의 테마가 다 있다.
            foreach (string theme in Directory.GetDirectories(Path.Combine(Repo, "game", "themes")))
            {
                string name = Path.GetFileName(theme);
                if (!File.Exists(Path.Combine(theme, "theme.json"))) continue;
                Assert.That(File.Exists(Path.Combine(game, "game", "themes", name, "theme.json")), Is.True, "테마 " + name);
            }
        }

        [Test]
        public void ServerList_StartsAServer_AndCanJudgeTheSharedHouse()
        {
            string server = CopyOf("content-server.txt");
            AssertDataLoads(server, "content-server.txt");

            // 같이 쓰는 집의 판정 칸: 저장소에서 읽은 것과 같은 면을 다 안다(맵 데이터가 빠지면 비어 가구 놓기가 모두 거절된다).
            var log = new ConcurrentQueue<string>();
            var grids = HouseGrids.Load(server, log.Enqueue);
            var repoGrids = HouseGrids.Load(Repo, null);
            Assert.That(repoGrids, Is.Not.Empty, "이 시험의 전제: 저장소에 가구를 놓는 맵이 있다");
            Assert.That(grids.Keys, Is.EquivalentTo(repoGrids.Keys), "판정 칸을 만든 면\n" + string.Join("\n", log));
            foreach (var surface in repoGrids.Keys)
                Assert.That(grids[surface].Spawn, Is.EqualTo(repoGrids[surface].Spawn), surface);
            Assert.That(log, Is.Empty);

            // 서버 한 번: 그 폴더의 data 로 켜서(돈·집·마을을 같이 쓰는 방) 듣고, 끄면 저장하고 닫는다.
            var options = ServerOptions.Resolve(new[]
            {
                "--data", Path.Combine(server, "data"), "--save-root", Path.Combine(_root, "server-save"), "--slot", "publish-check",
                // 같은 기계 안에서만 닿는 루프백 — 시험용으로 안전하다. 포트는 운영체제가 준다.
                "--bind", System.Net.IPAddress.Loopback.ToString(), "--port", "0", "--share", "wallet,house,town",
            }, _ => null, server);
            var output = new StringWriter();
            var writer = TextWriter.Synchronized(output);
            using var stop = new CancellationTokenSource();
            int exit = -1;
            var thread = new Thread(() => exit = Program.Run(options, writer, stop.Token)) { IsBackground = true };
            thread.Start();
            var started = System.Diagnostics.Stopwatch.StartNew();
            while (!output.ToString().Contains(Program.ListeningPrefix) && thread.IsAlive && started.Elapsed < TimeSpan.FromSeconds(60)) Thread.Sleep(20);
            stop.Cancel();
            Assert.That(thread.Join(TimeSpan.FromSeconds(30)), Is.True, "끝나지 않았다\n" + output);
            Assert.That(exit, Is.EqualTo(Program.ExitOk), output.ToString());
            StringAssert.Contains(Program.ListeningPrefix, output.ToString());
            StringAssert.Contains("저장하고 닫음", output.ToString());
        }

        [Test]
        public void ClientList_WithoutTheTimeFile_DoesNotStart()
        {
            // 목록이 지키는 것이 실제로 잡히는지: game/time.json 줄을 뺀 목록으로는 정의를 읽지 못한다.
            string target = Path.Combine(_root, "no-time");
            PublishListCopy.Copy(Repo, "content.txt", target, list => list.Replace("\ngame/time.json", "\n# (뺌)"));
            Assert.That(File.Exists(Path.Combine(target, "game", "time.json")), Is.False, "이 시험의 전제: 줄을 뺐다");
            var load = GameDataLoader.Load(Path.Combine(target, "data"), RootbornBuilders.Register);
            Assert.That(load.Ok, Is.False);
        }

        // 실내 맵(Assets/LDtk)과 키트로 만든 집 맵(art/world/rooms/house) 줄을 하나씩 뺀다.
        [TestCase("Assets/LDtk/**/*.ldtk", "Assets/LDtk")]
        [TestCase("art/world/rooms/house/*.ldtk", "art/world/rooms/house")]
        public void ServerList_WithoutTheMaps_CannotJudgeTheHouse(string line, string folder)
        {
            string target = Path.Combine(_root, "no-maps");
            PublishListCopy.Copy(Repo, "content-server.txt", target, list => list.Replace("\n" + line + "\n", "\n# (뺌)\n"));
            string dir = Path.Combine(target, folder);
            Assert.That(Directory.Exists(dir) && Directory.GetFiles(dir, "*.ldtk", SearchOption.AllDirectories).Length > 0,
                Is.False, "이 시험의 전제: 줄을 뺐다");
            var log = new ConcurrentQueue<string>();
            Assert.That(HouseGrids.Load(target, log.Enqueue), Is.Empty);
            Assert.That(log, Is.Not.Empty, "읽지 못했다고 로그를 남긴다");
        }

        [Test]
        public void ListCopy_FollowsThePublishScriptRules()
        {
            var client = PublishListCopy.Select(Repo, File.ReadAllText(Path.Combine(Repo, "Tools", "publish", "content.txt")));
            Assert.That(client.Where(f => f.StartsWith("data/atlases/", StringComparison.Ordinal)), Is.Empty, "좌표표는 그림 몫(뺀다)");
            Assert.That(client.Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)), Is.Empty, "그림은 복사하지 않는다");
            Assert.That(client, Does.Contain("game/time.json"));
            Assert.That(client.Any(f => f.StartsWith("game/themes/", StringComparison.Ordinal) && f.EndsWith("/theme.json", StringComparison.Ordinal)), Is.True);
            Assert.That(client.Any(f => f.EndsWith(".ldtkl", StringComparison.Ordinal)), Is.True, "@ldtk 는 레벨 파일도");
            Assert.That(client, Does.Not.Contain("game/themes/README.md"), "끝이 / 인 글롭은 폴더에만 맞는다");

            var server = PublishListCopy.Select(Repo, File.ReadAllText(Path.Combine(Repo, "Tools", "publish", "content-server.txt")));
            Assert.That(server.Where(f => f.StartsWith("data/localization/", StringComparison.Ordinal)), Is.Empty);
            Assert.That(server, Does.Not.Contain("data/export-report.json"));
        }
    }
}
