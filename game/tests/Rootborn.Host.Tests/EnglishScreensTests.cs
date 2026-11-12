using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Host.Text;
using Rootborn.Screens;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 영어 화면: --lang en으로 실제 창과 게임 루프를 띄우고(도시형 몰딩 테마), 등록 표의 모든 화면을 하나씩 열어 그리고 찍는다
    /// (output/host-screens-evidence/en-&lt;화면 id&gt;.png). 본다: 패널 밖으로 나간 글자, 영어에 없는 키, 그려진 글자에 남은 한글.
    /// 데이터 이름이 한국어뿐인 것(가구 이름 등 — 데이터에 이름 키가 없다)은 KnownKorean에 이유와 함께 둔다.
    /// 화면은 ScreenStack.Open과 상호작용으로 연다(여는 길은 각 화면 테스트가 본다). 자동 테스트와 스크린샷이고, 입력 근거가 아니다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class EnglishScreensTests
    {
        /// <summary>한글이 남아도 되는 곳: 화면 id → 이유(그 화면의 한글 글자 전부를 허용한다).</summary>
        private static readonly Dictionary<string, string> KnownKorean = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ScreenCatalog.FurniturePlacement] = "가구 1,503개에 이름 키가 없어 데이터의 한국어 이름을 보인다(data/interiors, 범위 밖 — 규칙 키 data.<id>.name으로 더할 수 있다)",
        };

        /// <summary>어느 언어에도 문구가 없는 키(데이터 문제, Unity 판도 같았다 — Rootborn.Screens.Tests의 KnownMissingKeys와 같은 것): 키 → 이유.</summary>
        private static readonly Dictionary<string, string> KnownMissing = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en:dialogue.firstNpc.greeting"] = "첫 NPC 인사말 문구가 data/localization 어디에도 없다(대사는 이야기 기획이라 지어 넣지 않는다)",
        };

        private static readonly InteractionEvent[] ContentInteractions =
        {
            // 알바 예약이 먼저다(장소 방문이 시간을 쓰면 예약이 거절될 수 있다).
            new InteractionEvent("Town", "part-time-work", "work.shift.corner-store"),
            new InteractionEvent("House", "npc", "npc.first-guide"),
            new InteractionEvent("Town", "location-activity", "location.library"),
        };

        private string _work;

        [SetUp]
        public void SetUp()
        {
            _work = Path.Combine(Path.GetTempPath(), "rootborn-en-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_work, "saves"));
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_work, true); } catch (IOException) { }
        }

        private static bool IsHangul(char c) => (c >= '가' && c <= '힣') || (c >= 'ᄀ' && c <= 'ᇿ') || (c >= '㄰' && c <= '㆏');

        [Test]
        public void EveryRegisteredScreen_InEnglish_IsShot_FitsItsPanel_AndHasNoKoreanLeft()
        {
            string root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            string evidence = Path.Combine(root, "output", "host-screens-evidence");
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", Path.Combine(_work, "saves"), "--slot", "slot-1", "--size", "1920x1080", "--evidence", evidence, "--lang", "en",
            });
            options.TestMode = true;

            var errors = new List<string>();
            var overflows = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            var borderTouches = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            var korean = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            var shot = new List<string>();
            List<ScreenEntry> entries = ScreenCatalog.Create().Screens.OrderBy(e => e.Id, StringComparer.Ordinal).ToList();

            using (var player = new PlayerDriver(options))
            {
                player.Frames(3);
                Assert.That(player.Game.Text.Language, Is.EqualTo("en"));
                using var textRenderer = new TextRenderer(player.Game.GraphicsDevice, options.FontPath);
                var measurer = new HostTextMeasurer(textRenderer) { Scale = player.Game.Fit.Scale };

                bool Inspect(string id)
                {
                    GameScreen screen = player.Screens.Find(id);
                    var list = new DrawList();
                    if (screen != null && screen.IsOpen) TestThemeScreensTests.EmitVisible(screen.Root, list, measurer);
                    if (list.Commands.Count == 0) return false;
                    player.ScreenshotScreen(id, "en-" + id);
                    if (!shot.Contains(id)) shot.Add(id);
                    TestThemeScreensTests.Check(id, list, measurer, player.Game.Renderer.Skins, overflows, borderTouches);
                    foreach (DrawCommand c in list.Commands)
                        if (c.Kind == DrawKind.Text && c.Text.Any(IsHangul))
                        {
                            if (!korean.TryGetValue(id, out List<string> found)) korean[id] = found = new List<string>();
                            if (!found.Contains(c.Text)) found.Add(c.Text);
                        }
                    return true;
                }

                // 월드 화면 전체(HUD가 영어로 보이는지)
                player.Screenshot("en-world");
                // 내용이 있어야 보이는 화면(미니게임, 대화, 장소)을 먼저 상호작용으로 연다(다른 화면이 시간을 쓰기 전에).
                foreach (InteractionEvent interaction in ContentInteractions.Append(StorageChestHostTests.PrepareChest(player)))
                {
                    try
                    {
                        player.Screens.Interaction(interaction);
                        player.Frames(3);
                    }
                    catch (Exception e) { errors.Add(interaction.Kind + ": " + e.GetType().Name + ": " + e.Message); continue; }
                    // 닫으면 다음 화면이 열릴 수 있다(장소를 닫으면 하루 이벤트). 맨 위부터 찍고 닫기를 남은 것이 없을 때까지.
                    for (int guard = 0; guard < 8; guard++)
                    {
                        GameScreen top = player.Screens.OpenScreens.LastOrDefault(o => o.Layer != ScreenLayer.Hud);
                        if (top == null) break;
                        Inspect(top.Id);
                        try { player.Screens.Close(top.Id); player.Frames(1); } catch (Exception) { }
                    }
                }

                foreach (ScreenEntry entry in entries)
                {
                    bool openedHere = false;
                    try
                    {
                        if (!player.Screens.IsOpen(entry.Id)) { player.Screens.Open(entry.Id); openedHere = true; }
                        player.Frames(2);
                        Inspect(entry.Id);
                    }
                    catch (Exception e) { errors.Add(entry.Id + ": " + e.GetType().Name + ": " + e.Message); }
                    if (openedHere) { try { player.Screens.Close(entry.Id); player.Frames(1); } catch (Exception) { } }
                }
                List<string> missing = player.Game.Text.MissingKeys.Where(k => k.StartsWith("en:", StringComparison.Ordinal)).ToList();
                var report = new StringBuilder();
                report.AppendLine("# 영어(--lang en)로 모든 화면 그리기");
                report.AppendLine("찍은 화면 " + shot.Count + "개(en-<id>.png): " + string.Join(", ", shot));
                report.AppendLine("열거나 그리다 난 예외: " + (errors.Count == 0 ? "없음" : string.Join(" / ", errors)));
                report.AppendLine("영어에 없어 기준 언어로 보인 키: " + (missing.Count == 0 ? "없음" : string.Join(", ", missing)));
                report.AppendLine("## 패널 밖으로 나간 글자");
                foreach (var pair in overflows) report.AppendLine("- " + pair.Key + ": " + string.Join(" | ", pair.Value.Distinct()));
                report.AppendLine("## 한글이 남은 글자");
                foreach (var pair in korean) report.AppendLine("- " + pair.Key + (KnownKorean.ContainsKey(pair.Key) ? " (알려짐)" : "") + ": " + string.Join(" | ", pair.Value.Take(8)));
                report.AppendLine("## 패널 테두리(안쪽 여백)에 닿은 글자 — 참고");
                foreach (var pair in borderTouches) report.AppendLine("- " + pair.Key + ": " + string.Join(" | ", pair.Value.Distinct().Take(6)));
                Directory.CreateDirectory(evidence);
                File.WriteAllText(Path.Combine(evidence, "en-report.md"), report.ToString());
                TestContext.Out.WriteLine(report.ToString());

                Assert.That(errors, Is.Empty);
                // 내용이 있어야만 그릴 것이 있는 화면: 추적 목표 HUD(추적한 것이 없으면 숨는다), 상호작용 안내(앞에 대상이 있을 때),
                // 성인 시설(성인 진행이 있어야 열린다), 상호작용 실패 안내(거절 이유가 있을 때 — InteractionRefusalNoticeTests가 따로 본다).
                // 방에 들어간 직후 알림(방 설정이 있을 때 — 모든 언어·2배 문구는 Screens HostRoomScreenTests, 실제 창은 MP_GUEST_002).
                // 이 목록 밖의 화면이 찍히지 않으면 실패한다(새 화면이 영어 검사에서 조용히 빠지지 않게).
                var needsContent = new[] { ScreenCatalog.HudTracked, ScreenCatalog.HudPrompt, ScreenCatalog.Facility, ScreenCatalog.InteractionRefusal,
                    ScreenCatalog.RoomSharesNotice };
                Assert.That(entries.Select(e => e.Id).Where(id => !shot.Contains(id)).Except(needsContent), Is.Empty, "내용 없이도 그릴 수 있는 화면은 모두 찍었다");
                Assert.That(overflows, Is.Empty, "영어 글자가 패널 밖으로 나갔다(보고서 output/host-screens-evidence/en-report.md)");
                Assert.That(korean.Keys.Except(KnownKorean.Keys), Is.Empty, "영어 화면에 한글이 남았다(보고서 참고)");
                Assert.That(missing.Except(KnownMissing.Keys), Is.Empty, "영어에 없는 키");
            }
        }
    }
}
