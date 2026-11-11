using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Quests;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Dialogue;
using Rootborn.Screens.Hud;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 묶음 2·3(HUD와 대화)을 실제 창과 게임 루프로: 새 슬롯 → HUD → 집에서 엄마에게 걸어가 안내 글자 → E로 대화 → 대화가 떠 있는 채
    /// 걷기 → 닫기 → 메인 체인 시작(추적 목표 HUD는 수첩에서 추적한 뒤에 보임) → 다시 말 걸어 E로 퀘스트 받기. 하루 이벤트는 장소 지점에 걸어가 E.
    /// 조작은 방향 키·E·포인터뿐(장치 이벤트 주입, OS 입력 계층 미확인). 순간이동·내부 호출·상태 강제 설정 없음.
    /// 스크린샷: output/host-screens-evidence/hud-*.png, dialogue-*.png, daily-event-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class HudDialogueHostTests
    {
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-hud-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start(params string[] extra)
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "host-screens-evidence"),
            }.Concat(extra).ToArray());
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private static T Screen<T>(PlayerDriver player, string id) where T : GameScreen => (T)player.Screens.Find(id);

        [Test]
        public void HUD_DIALOGUE_001_NewSlot_HudShows_WalkToMom_E_Dialogue_WalkWhileOpen_Close_TrackedHud()
        {
            using PlayerDriver player = Start();
            // HUD가 처음부터 떠 있다.
            Assert.That(player.Screens.IsOpen(ScreenCatalog.HudClock) && player.Screens.IsOpen(ScreenCatalog.HudVitals)
                && player.Screens.IsOpen(ScreenCatalog.HudButtons) && player.Screens.IsOpen(ScreenCatalog.HudTracked), Is.True);
            Assert.That(((UiLabel)player.Node(ClockHudScreen.LabelId)).Text, Is.EqualTo("1일  07:00 · 용돈 0"));
            Assert.That(player.Node("hud.button.placement").Visible, Is.True, "집에서는 가구 배치 켬/끔이 보인다");
            Assert.That(Screen<TrackedObjectiveHudScreen>(player, ScreenCatalog.HudTracked).IsShown, Is.False, "추적 중인 것이 없다");
            player.Screenshot("hud-01-new-slot-house");

            // 엄마 앞으로 걸어가면 안내 글자.
            PlacedNpc mom = player.Game.World.Npcs.Single(n => n.NpcId == "npc.first-guide");
            player.WalkNear(mom.X, mom.Y + 32f, 20f);
            Assert.That(Screen<InteractionPromptScreen>(player, ScreenCatalog.HudPrompt).Text, Is.EqualTo("[E] 말 걸기"));
            UiRect prompt = player.Node(InteractionPromptScreen.LabelId).Rect;
            player.Screenshot("hud-02-prompt-at-mom");

            // E → 대화.
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Dialogue);
            var dialogue = Screen<DialogueScreen>(player, ScreenCatalog.Dialogue);
            Assert.That(dialogue.Model.DialogueId, Is.EqualTo("Dialogue_FirstNpc"));
            Assert.That(player.Game.Session.CurrentDialogueNpcId, Is.EqualTo("npc.first-guide"));
            Assert.That(player.Screens.MovementBlocked, Is.False);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Dialogue, "dialogue-01-mom-open");

            // 메인 체인이 시작된다. 추적 목표 HUD는 수첩에서 추적한 뒤에 보인다(Unity TrackedObjectiveHud, OBJECTIVE_JOURNAL_001).
            var tracked = Screen<TrackedObjectiveHudScreen>(player, ScreenCatalog.HudTracked);
            Assert.That(player.Game.Session.TrackedObjective().StepId, Is.EqualTo("mainquest.BackToSchool"));
            Assert.That(tracked.IsShown, Is.False);

            // 대화가 떠 있는 동안 걷는다(Unity 판과 같음).
            float x0 = player.World.Walker.X, y0 = player.World.Walker.Y;
            player.WalkAway(2);
            Assert.That(Math.Abs(player.World.Walker.X - x0) + Math.Abs(player.World.Walker.Y - y0), Is.GreaterThan(player.World.Map.Grid.CellSize));
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Dialogue), Is.True);
            player.Screenshot("dialogue-02-walk-while-open");

            // 닫기 버튼을 포인터로.
            player.PressButton(DialogueScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.Dialogue);
            Assert.That(player.Game.Session.CurrentDialogue, Is.Null);
            player.MovePointer(-50, -50);
            player.Screenshot("hud-03-after-mom");

            // 다시 말 걸어 E로 퀘스트를 받는다(첫 선택지).
            player.WalkNear(mom.X, mom.Y + 32f, 20f);
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Dialogue);
            Assert.That(Screen<DialogueScreen>(player, ScreenCatalog.Dialogue).Model.Choices.Length, Is.EqualTo(1));
            player.PressKey(TestKey.E);
            player.WaitForScreenClosed(ScreenCatalog.Dialogue);
            QuestDefinition quest = player.Game.Session.Definitions.Get<QuestDefinition>("quest.gatherWood");
            Assert.That(player.Game.Session.QuestLog.GetState(quest), Is.Not.EqualTo(QuestStage.NotStarted), "E로 고른 선택지가 퀘스트를 받았다");

            Assert.That(player.CollisionViolations, Is.EqualTo(0));
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, Array.Empty<string>());
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
            Assert.That(prompt.W, Is.GreaterThan(0));
        }

        /// <summary>
        /// 시험용 상호작용 파일: 실제 파일에 집 안 도서관 장소 지점(location-activity) 하나를 더한다. 마을의 장소 지점은 아직
        /// 월드 데이터에 없어서다(interactions.json 머리말). 플레이어는 그 지점까지 걸어가 E를 누른다.
        /// </summary>
        private string InteractionsWithLibraryPoint(float x, float y)
        {
            string text = File.ReadAllText(Path.Combine(_root, InteractionData.DefaultRelativePath));
            const string houseThings = "\"things\": [";
            int house = text.IndexOf("\"House\"", StringComparison.Ordinal);
            int things = text.IndexOf(houseThings, house, StringComparison.Ordinal);
            string point = "\n        { \"kind\": \"location-activity\", \"id\": \"location.library\", \"at\": [ " + x.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ", " + y.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ], \"priority\": 45 },";
            text = text.Insert(things + houseThings.Length, point);
            string path = Path.Combine(_saves, "interactions-with-library.json");
            File.WriteAllText(path, text);
            return path;
        }

        [Test]
        public void DAILY_EVENT_001_WalkToALocationPoint_E_EventOpens_Choose_Close()
        {
            const float px = 624f, py = 560f;
            using PlayerDriver player = Start("--interactions", InteractionsWithLibraryPoint(px, py));
            player.WalkNear(px, py, 40f);
            Assert.That(player.Game.World.Interactions.Target?.Kind, Is.EqualTo("location-activity"));
            Assert.That(Screen<InteractionPromptScreen>(player, ScreenCatalog.HudPrompt).Text, Does.StartWith("[E] "));
            player.Screenshot("daily-event-00-prompt");

            player.PressKey(TestKey.E);
            // 장소 지점은 장소 화면을 먼저 연다. 닫기를 누르면 그 장소의 하루 이벤트가 열린다(Unity 순서).
            player.WaitForScreen(ScreenCatalog.Location);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.DailyEvent), Is.False, "장소 화면이 떠 있는 동안은 기다린다");
            player.PressButton(Rootborn.Screens.Day.LocationScreen.CloseId);
            player.WaitForScreen(ScreenCatalog.DailyEvent);
            var screen = Screen<DailyEventScreen>(player, ScreenCatalog.DailyEvent);
            Assert.That(screen.Model.EventId, Is.EqualTo("daily-event.library-hard-book"));
            Assert.That(player.Screens.MovementBlocked, Is.True);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.DailyEvent, "daily-event-01-open");

            // 막는 동안은 걷지 않는다.
            float x0 = player.World.Walker.X, y0 = player.World.Walker.Y;
            player.HoldKey(TestKey.Up, 30);
            Assert.That((player.World.Walker.X, player.World.Walker.Y), Is.EqualTo((x0, y0)));

            int minutes = player.Game.Session.Student.TimeMinutes;
            player.PressButton(DailyEventScreen.ChoiceId(0));
            Assert.That(player.Game.Session.Student.TimeMinutes, Is.GreaterThan(minutes), "선택지가 적용됐다");
            Assert.That(((UiLabel)player.Node(ClockHudScreen.LabelId)).Text, Is.Not.EqualTo("1일  07:00 · 용돈 0"), "시계 HUD가 바뀐 시각을 보인다");
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.DailyEvent, "daily-event-02-chosen");

            player.PressButton(DailyEventScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.DailyEvent);
            Assert.That(player.Screens.MovementBlocked, Is.False);
            player.WalkAway(1);
            player.Screenshot("daily-event-03-closed");
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, Array.Empty<string>());
        }
    }
}
