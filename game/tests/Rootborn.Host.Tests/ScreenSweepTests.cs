using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Screens;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 사람이 보는 훑기(손으로만 돈다 — [Explicit]): 실제 창과 도시형 몰딩 테마로 등록 표의 모든 화면을 언어(ko, en)·창 크기(1920x1080, 1280x720)마다
    /// 하나씩 열어 찍는다(output/host-screens-sweep/&lt;언어&gt;-&lt;크기&gt;/&lt;화면 id&gt;.png). 판정하지 않는다: 겹친 글자, 잘린 패널, 빈 그림, 남은 디버그 표시,
    /// 서로 가리는 HUD는 사람이 그림을 열어 본다. 자동 판정은 EnglishScreensTests(영어 넘침), TestThemeScreensTests(시험용 테마),
    /// Screens의 ScreenOverlapTests·LongTextScreenTests가 한다. 화면은 ScreenStack.Open과 상호작용으로 연다(입력 근거가 아니다).
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable, Explicit("사람이 그림을 보는 훑기 — 판정 없음")]
    public sealed class ScreenSweepTests
    {
        private static readonly InteractionEvent[] ContentInteractions =
        {
            new InteractionEvent("Town", "part-time-work", "work.shift.corner-store"),
            new InteractionEvent("House", "npc", "npc.first-guide"),
            new InteractionEvent("Town", "location-activity", "location.library"),
        };

        private string _work;

        [SetUp]
        public void SetUp()
        {
            _work = Path.Combine(Path.GetTempPath(), "rootborn-sweep-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_work, "saves"));
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_work, true); } catch (IOException) { }
        }

        [TestCase("ko", "1920x1080")]
        [TestCase("ko", "1280x720")]
        [TestCase("en", "1920x1080")]
        [TestCase("en", "1280x720")]
        public void EveryRegisteredScreen_IsShot(string language, string size)
        {
            string root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            string evidence = Path.Combine(root, "output", "host-screens-sweep", language + "-" + size);
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", Path.Combine(_work, "saves"), "--slot", "slot-1", "--size", size, "--evidence", evidence, "--lang", language,
            });
            options.TestMode = true;
            var shot = new List<string>();
            var errors = new List<string>();

            using (var player = new PlayerDriver(options))
            {
                player.Frames(3);
                player.Screenshot("world");

                void Shoot(string id)
                {
                    player.Frames(2);
                    player.Screenshot(id);
                    if (!shot.Contains(id)) shot.Add(id);
                }

                foreach (InteractionEvent interaction in ContentInteractions.Append(StorageChestHostTests.PrepareChest(player)))
                {
                    try { player.Screens.Interaction(interaction); player.Frames(3); }
                    catch (Exception e) { errors.Add(interaction.Kind + ": " + e.Message); continue; }
                    for (int guard = 0; guard < 8; guard++)
                    {
                        GameScreen top = player.Screens.OpenScreens.LastOrDefault(o => o.Layer != ScreenLayer.Hud);
                        if (top == null) break;
                        Shoot(top.Id);
                        try { player.Screens.Close(top.Id); player.Frames(1); } catch (Exception) { }
                    }
                }

                foreach (ScreenEntry entry in ScreenCatalog.Create().Screens.OrderBy(e => e.Id, StringComparer.Ordinal))
                {
                    if (entry.Layer == ScreenLayer.Hud || shot.Contains(entry.Id)) continue;
                    try
                    {
                        if (entry.Id == ScreenCatalog.RoomSharesNotice)
                            player.Screens.Open(entry.Id, argument: new Rootborn.Screens.Start.RoomSharesNoticeOpen(Rootborn.Core.Room.RoomPieceCatalog.CreateDefault().Names));
                        else player.Screens.Open(entry.Id);
                        if (!player.Screens.IsOpen(entry.Id)) continue; // 내용이 없어 바로 닫히는 화면
                        Shoot(entry.Id);
                        player.Screens.Close(entry.Id);
                        player.Frames(1);
                    }
                    catch (Exception e) { errors.Add(entry.Id + ": " + e.Message); }
                }
            }

            TestContext.Out.WriteLine(evidence + " — " + shot.Count + "개: " + string.Join(", ", shot));
            Assert.That(errors, Is.Empty);
        }
    }
}
