using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Objectives;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 묶음 6(목표 수첩)을 실제 창과 게임 루프로: 새 슬롯 → 엄마에게 걸어가 E로 대화(메인 체인 시작) → 닫기 → Tab으로 수첩 →
    /// 분류 단추와 줄을 포인터로 → 추적 → 수첩이 떠 있는 채 걷기 → Tab으로 닫으면 추적 목표 HUD가 그 항목을 보임.
    /// 조작은 방향 키·E·Tab·포인터·휠뿐(장치 이벤트 주입, OS 입력 계층 미확인). 순간이동·내부 호출·상태 강제 설정 없음.
    /// 스크린샷: output/host-screens-evidence/journal-*.png. 200줄 목록을 넘기는 동안 프레임 시간을 잰다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class ObjectiveJournalHostTests
    {
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-journal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start(Action<ScreenRegistry> extra = null)
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "host-screens-evidence"),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options, extra);
            player.Frames(3);
            return player;
        }

        private static ObjectiveJournalScreen Journal(PlayerDriver player) => (ObjectiveJournalScreen)player.Screens.Find(ScreenCatalog.ObjectiveJournal);

        /// <summary>목록의 줄을 포인터로 누른다(휠로 보이게 넘긴 뒤 그 줄 가운데).</summary>
        private static void ClickRow(PlayerDriver player, ObjectiveJournalScreen journal, int row)
        {
            UiRect view = journal.List.View;
            for (int guard = 0; guard < 200; guard++)
            {
                UiRect r = journal.List.RowRect(row);
                if (r.Y >= view.Y && r.Bottom <= view.Bottom) break;
                player.MovePointer(view.CenterX, view.CenterY);
                player.Wheel(r.Y < view.Y ? 1 : -1);
            }
            UiRect rect = journal.List.RowRect(row);
            Assert.That(player.Game.Ui.HitTest(rect.CenterX, rect.CenterY), Is.SameAs(journal.List));
            player.ClickAt(rect.CenterX, rect.CenterY);
        }

        [Test]
        public void OBJECTIVE_JOURNAL_001_TalkToMom_Tab_PickCategoryAndRow_Track_WalkWhileOpen_HudShowsIt()
        {
            using PlayerDriver player = Start();
            player.WalkToNpc("npc.first-guide"); // 엄마는 집 안을 걸어 다닌다
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Dialogue);
            player.PressButton(Rootborn.Screens.Dialogue.DialogueScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.Dialogue);
            var hud = (TrackedObjectiveHudScreen)player.Screens.Find(ScreenCatalog.HudTracked);
            Assert.That(hud.IsShown, Is.False, "수첩에서 추적하기 전");

            player.OpenScreenWithKey(TestKey.Tab, ScreenCatalog.ObjectiveJournal);
            ObjectiveJournalScreen journal = Journal(player);
            Assert.That(player.Screens.MovementBlocked, Is.False);
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.ObjectiveJournal, "journal-01-open-goals");

            player.PressButton(ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Quests));
            Assert.That(journal.SelectedCategory, Is.EqualTo(JournalCategories.Quests));
            int row = journal.Items.ToList().FindIndex(i => i.StableKey.StartsWith("questchain.") && i.IsTracked);
            Assert.That(row, Is.GreaterThanOrEqualTo(0), "메인 체인 줄");
            ClickRow(player, journal, row);
            Assert.That(journal.SelectedIndex, Is.EqualTo(row));
            string key = journal.SelectedStableKey;
            player.PressButton(ObjectiveJournalScreen.TrackId);
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.ObjectiveJournal, "journal-02-quests-tracked");

            // 수첩이 떠 있는 동안 걷는다(이동을 막지 않는 화면).
            float x0 = player.World.Walker.X, y0 = player.World.Walker.Y;
            player.WalkAway(2);
            Assert.That(Math.Abs(player.World.Walker.X - x0) + Math.Abs(player.World.Walker.Y - y0), Is.GreaterThan(player.World.Map.Grid.CellSize));
            Assert.That(player.Screens.IsOpen(ScreenCatalog.ObjectiveJournal), Is.True);

            player.CloseScreenWithKey(TestKey.Tab, ScreenCatalog.ObjectiveJournal);
            Assert.That(hud.IsShown, Is.True, "닫으면 추적 목표 HUD가 그 항목을 보인다");
            Assert.That(hud.Model.StableKey, Is.EqualTo(key));
            Assert.That(((UiLabel)player.Node(TrackedObjectiveHudScreen.ProgressId)).Text, Does.Contain(" / "));
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.HudTracked, "journal-03-tracked-hud");

            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, journal.TextKeys.Concat(hud.TextKeys));
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }

        /// <summary>
        /// 퀘스트 체인 기록의 그림 확인용. Unity 판에도 여는 키·상호작용이 없어 화면 쌓기의 Open(id)로 연다(내부 호출 — 플레이어 경로가 아니다).
        /// 카드는 포인터로 누른다.
        /// </summary>
        [Test]
        public void QUEST_CHAIN_LOG_Screenshot_OpenedById_CardByPointer()
        {
            using PlayerDriver player = Start();
            player.Screens.Open(ScreenCatalog.QuestChainLog);
            player.Frames(2);
            // 개발용 체인(DeveloperOnly)은 보이지 않는다. 실제 data/에서 보이는 체인은 메인 이야기 하나다.
            player.PressButton(Rootborn.Screens.Quests.QuestChainLogScreen.CardId(0));
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.QuestChainLog, "quest-chain-log-01");
            var screen = (Rootborn.Screens.Quests.QuestChainLogScreen)player.Screens.Find(ScreenCatalog.QuestChainLog);
            Assert.That(screen.Model.Selected, Is.EqualTo(0));
            Assert.That(screen.Model.Rows.Select(r => r.ChainId), Has.None.Matches<string>(id => player.Game.Session.Chain(id).DeveloperOnly));
            var card = (Rootborn.Screens.Quests.QuestChainCard)player.Node(Rootborn.Screens.Quests.QuestChainLogScreen.CardId(0));
            Assert.That(card.Title, Is.Not.EqualTo(screen.Model.Rows[0].NameKey), "이름은 키가 아니라 문구");
            Assert.That(player.Node(Rootborn.Screens.Quests.QuestChainLogScreen.CardId(1)).Visible, Is.False);
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }

        /// <summary>시험용 모으는 쪽: 발견 분류에 줄 200개.</summary>
        private sealed class ManyItemsSource : IObjectiveJournalSource
        {
            public string SourceId => "test-many";
            public string CategoryId => JournalCategories.Hints;

            public IReadOnlyList<JournalItem> BuildItems(GameSession session)
            {
                var items = new JournalItem[200];
                for (int i = 0; i < items.Length; i++)
                    items[i] = new JournalItem("test-many." + i.ToString("000"), CategoryId, JournalText.Raw("row " + i.ToString("000")), JournalText.Empty,
                        JournalText.Empty, JournalText.Key("ui.objectives.day_progress", i, 0, 0), JournalText.Empty, false, 1000 + i);
                return items;
            }
        }

        [Test]
        public void OBJECTIVE_JOURNAL_PERF_200Rows_OpenAndScroll_FrameTime()
        {
            using PlayerDriver player = Start(r => r.AddListener(s => ObjectiveJournalSources.For(s).Add(new ManyItemsSource())));
            var clock = Stopwatch.StartNew();
            player.OpenScreenWithKey(TestKey.Tab, ScreenCatalog.ObjectiveJournal);
            double openMs = clock.Elapsed.TotalMilliseconds;
            ObjectiveJournalScreen journal = Journal(player);
            clock.Restart();
            player.PressButton(ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Hints));
            double categoryMs = clock.Elapsed.TotalMilliseconds;
            Assert.That(journal.Items.Count, Is.GreaterThanOrEqualTo(200));

            var frames = new List<double>(400);
            UiRect view = journal.List.View;
            player.MovePointer(view.CenterX, view.CenterY);
            player.Wheel(-1); // 첫 휠은 글자 그림(글리프)을 채운다
            player.Wheel(1);
            int maxCommands = 0;
            long allocated = 0;
            for (int i = 0; i < 120; i++)
            {
                clock.Restart();
                long before = GC.GetAllocatedBytesForCurrentThread();
                player.Wheel(i < 60 ? -1 : 1);
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                frames.Add(clock.Elapsed.TotalMilliseconds / 2.0); // Wheel은 두 프레임을 돈다
                Assert.That(journal.List.LastEmittedRows, Is.LessThanOrEqualTo(8), "보이는 줄만 그린다");
                maxCommands = Math.Max(maxCommands, player.Game.DrawCommandCount);
            }
            long bytesPerFrame = allocated / (120 * 2);
            ClickRow(player, journal, 150);
            Assert.That(journal.SelectedIndex, Is.EqualTo(150));
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.ObjectiveJournal, "journal-04-200-rows");
            frames.Sort();
            TestContext.Progress.WriteLine($"[OBJECTIVE_JOURNAL_PERF] 열기 {openMs:0.0}ms(Tab 누름·뗌 4프레임 포함), 분류 바꾸기 {categoryMs:0.0}ms(포인터 4프레임 포함), 200줄 넘기기 프레임 {frames.Count}개: 중앙 {frames[frames.Count / 2]:0.00}ms, 95% {frames[(int)(frames.Count * 0.95)]:0.00}ms, 최대 {frames[frames.Count - 1]:0.00}ms (참고 — 판정 안 함), 프레임당 할당 {bytesPerFrame}바이트, 그리기 명령 최대 {maxCommands}");
            // 벽시계는 CI 러너(소프트웨어 그리기)에서 흔들려 판정하지 않는다. 대신 흔들리지 않는 값: 보이는 줄만(위), 그리기 명령 수, 프레임당 할당.
            // 200줄을 다 그리면 명령이 1,000개를 넘는다. 할당은 처음 보이는 줄의 글자 그림을 만드는 것뿐이다(2026-11-12: 평균 170, 같은 줄을 다시 지나면 0).
            Assert.That(maxCommands, Is.LessThan(300), "보이는 것만 그린다(그리기 명령 수)");
            Assert.That(bytesPerFrame, Is.LessThan(4096), "넘기는 동안 프레임마다 새로 잡는 바이트");
        }
    }
}
