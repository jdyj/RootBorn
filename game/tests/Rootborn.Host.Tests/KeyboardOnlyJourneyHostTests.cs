using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Host.Tests.TwoGames;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Minigames;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 키보드만으로 화면 안의 포커스를 옮기고 누른다(PlayerDriver의 키 동사만). 길은 미리 알지 않는다: 지금 포커스에서 아직 안 눌러 본 방향 키를
    /// 실제로 눌러 보며 닿는 곳을 기록하고(눌러 봐도 그 자리면 같은 키를 더 눌러 목록 끝까지), 아는 길로 가장 가까운 안 가 본 갈래까지 되돌아간다.
    /// 화면 쌓기의 포커스를 읽기만 한다. 이동을 막지 않는 화면이면 Shift를 누른 채 방향 키를 누른다.
    /// </summary>
    internal static class KeyboardVerbs
    {
        private static readonly TestKey[] Arrows = { TestKey.Down, TestKey.Right, TestKey.Up, TestKey.Left };

        private static void Arrow(PlayerDriver p, TestKey key, bool shift)
        {
            if (shift) p.KeyDown(TestKey.LeftShift);
            p.PressKey(key);
            if (shift) p.KeyUp(TestKey.LeftShift);
        }

        private static bool ShiftFor(PlayerDriver p) => p.Screens.FocusScreen != null && !p.Screens.FocusScreen.BlocksMovement;

        /// <summary>그 요소에 키보드 포커스가 갈 때까지 방향 키를 누른다. 닿지 못하면 실패(그것이 "막히는 곳"이다).</summary>
        public static void FocusByKeys(this PlayerDriver p, string id, int maxPresses = 1500)
        {
            UiNode target = p.Node(id);
            bool shift = ShiftFor(p);
            var edges = new Dictionary<UiNode, Dictionary<TestKey, UiNode>>();
            int presses = 0;
            if (p.Screens.Tree.Focused == null || !p.Screens.Tree.Focused.FocusRing) { Arrow(p, TestKey.Down, shift); presses++; } // 첫 키는 테두리를 보인다
            while (p.Screens.Tree.Focused != target)
            {
                if (presses > maxPresses) throw new InvalidOperationException("방향 키 " + maxPresses + "번 안에 " + id + "에 닿지 못했다(지금 " + p.Screens.Tree.Focused?.Id + ")");
                UiNode at = p.Screens.Tree.Focused ?? throw new InvalidOperationException("포커스가 없다");
                if (!edges.ContainsKey(at)) edges[at] = new Dictionary<TestKey, UiNode>();
                TestKey? untried = Arrows.Cast<TestKey?>().FirstOrDefault(k => !edges[at].ContainsKey(k.Value));
                if (untried.HasValue)
                {
                    TestKey key = untried.Value;
                    UiNode to = at;
                    // 안에서 키를 쓰는 요소(목록·카드·탭·스크롤 글)는 끝까지 눌러야 나간다. 다른 요소는 한 번.
                    int repeat = at is UiScrollList || at is UiCardScroller || at is UiTabBar || at is UiSectionScrollView ? 200 : 1;
                    for (int i = 0; i < repeat && to == at; i++) { Arrow(p, key, shift); presses++; to = p.Screens.Tree.Focused; }
                    edges[at][key] = to;
                    if (to != null && !edges.ContainsKey(to)) edges[to] = new Dictionary<TestKey, UiNode>();
                    continue;
                }
                // 여기서는 다 눌러 봤다: 아는 길로 안 눌러 본 방향이 남은 가장 가까운 요소까지 간다.
                List<TestKey> route = RouteToUnexplored(at, edges);
                if (route == null) throw new InvalidOperationException(id + "에 닿는 길이 없다(닿은 요소 " + edges.Count + "개: " + string.Join(", ", edges.Keys.Select(n => n.Id)) + ")");
                foreach (TestKey key in route) { Arrow(p, key, shift); presses++; }
            }
        }

        private static List<TestKey> RouteToUnexplored(UiNode from, Dictionary<UiNode, Dictionary<TestKey, UiNode>> edges)
        {
            var back = new Dictionary<UiNode, (UiNode, TestKey)> { [from] = (null, TestKey.Up) };
            var queue = new Queue<UiNode>(new[] { from });
            while (queue.Count > 0)
            {
                UiNode n = queue.Dequeue();
                if (n != from && (!edges.ContainsKey(n) || edges[n].Count < Arrows.Length))
                {
                    var route = new List<TestKey>();
                    for (UiNode m = n; m != from; m = back[m].Item1) route.Insert(0, back[m].Item2);
                    return route;
                }
                if (!edges.TryGetValue(n, out var outs)) continue;
                foreach (var pair in outs)
                    if (pair.Value != null && pair.Value != n && !back.ContainsKey(pair.Value)) { back[pair.Value] = (n, pair.Key); queue.Enqueue(pair.Value); }
            }
            return null;
        }

        /// <summary>방향 키로 그 요소까지 가서 Enter.</summary>
        public static void PressByKeys(this PlayerDriver p, string id)
        {
            p.FocusByKeys(id);
            p.PressKey(TestKey.Enter);
        }

        /// <summary>포커스된 목록에서 그 줄이 골라질 때까지 아래(또는 위) 키. 이동을 막는 화면의 목록에 쓴다.</summary>
        public static void SelectRowByKeys(this PlayerDriver p, UiCardGrid list, int row)
        {
            bool shift = ShiftFor(p);
            for (int i = 0; i < 200 && list.SelectedIndex != row; i++)
                Arrow(p, list.SelectedIndex < row ? TestKey.Down : TestKey.Up, shift);
            if (list.SelectedIndex != row) throw new InvalidOperationException("줄 " + row + "을 고르지 못했다(지금 " + list.SelectedIndex + ")");
        }
    }

    /// <summary>
    /// 포인터를 한 번도 쓰지 않고(포인터는 화면 밖에 둔다): 새 게임(이름까지) → 집에서 일과 고르기 → 마을 → 알바 미니게임 → 가게에서 사기 →
    /// 상자 놓고 넣기 → 자기 → 이어 하기. 걷기는 방향 키, 상호작용은 E, 화면 안은 방향 키(이동을 막지 않는 화면은 Shift+방향 키)와 Enter.
    /// 막히는 곳이 결과다: 지금은 상자 놓기(가구 배치는 HUD 단추와 방의 칸을 포인터로만 고른다)에서 막혀, 그 단계는 막힘을 확인하고 넘어간다.
    /// 근거는 장치 이벤트 주입(OS 입력 계층 미확인). 스크린샷은 output/host-keyboard-evidence/.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class KeyboardOnlyJourneyHostTests
    {
        private string _saves;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-keyboard-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Launch()
        {
            HostOptions options = HostOptions.Resolve(new[] { "--save-root", _saves, "--size", "1280x720" });
            options.TestMode = true;
            options.EvidenceDir = Path.Combine(options.RepoRoot, "output", "host-keyboard-evidence");
            Directory.CreateDirectory(options.EvidenceDir);
            var player = new PlayerDriver(options);
            player.MovePointer(-50, -50); // 포인터는 화면 밖: 아무것도 가리키지 않는다
            player.Frames(3);
            return player;
        }

        private static readonly string ShopLocation = "location.corner-store", ShopWork = "work.shift.corner-store";

        private static int Allowance(PlayerDriver p) => p.Game.Session.Inventory.CountOf(p.Game.Session.Item("item.allowance"));

        /// <summary>미니게임(순서 외우기)을 화면에 보인 순서대로 방향 키와 Enter로 누른다. 끝나면 Esc.</summary>
        private static void PlayShiftWithKeys(PlayerDriver p)
        {
            p.WalkToThing("part-time-work", ShopWork);
            p.PressKey(TestKey.E);
            p.WaitForScreen(ScreenCatalog.Minigame, 30);
            var screen = (MinigameScreen)p.Screens.Find(ScreenCatalog.Minigame);
            for (int guard = 0; guard < 8 && !screen.Session.IsComplete; guard++)
            {
                ChallengeSession challenge = screen.Session.Current;
                Assert.That(challenge.Mechanic, Is.EqualTo(MinigameMechanic.Sequence), "가게 알바는 순서 외우기");
                int[] order = challenge.PreviewSequence; // 미리 보기 동안 화면에 보이는 순서
                for (int i = 0; i < 600 && challenge.PreviewVisible; i++) p.Frames(1);
                foreach (int answer in order) p.PressByKeys(SymbolChoiceView.ChoiceId(answer));
                for (int i = 0; i < 120 && screen.Session.Current == challenge && !screen.Session.IsComplete; i++) p.Frames(1);
            }
            Assert.That(screen.Session.IsComplete && screen.Session.Succeeded, Is.True, "키보드로 알바 미니게임을 끝냈다");
            p.Frames(2);
            p.PressKey(TestKey.Escape);
            p.WaitForScreenClosed(ScreenCatalog.Minigame);
            p.DismissPopupsByKeys();
        }

        [Test]
        public void KEYBOARD_JOURNEY_001_NewGameToContinue_WithoutThePointer()
        {
            var steps = new List<string>();
            using (PlayerDriver p = Launch())
            {
                // 1. 새 게임: 슬롯 선택 → 캐릭터 만들기(이름은 글자 칸에 쳐서, 위·아래 키로 칸을 나온다) → 시작.
                Assert.That(p.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
                Assert.That(p.Screens.Tree.Focused?.Id, Is.EqualTo(SaveSlotScreen.NewGameId(0)), "열자마자 첫 슬롯의 새 게임에 포커스");
                p.PressKey(TestKey.Enter);
                p.WaitForScreen(ScreenCatalog.CharacterCreator);
                p.FocusByKeys(CharacterCreatorScreen.NameFieldId);
                Assert.That(p.Game.TextInputActive, Is.True, "키보드로 들어간 글자 칸도 입력기를 켠다");
                p.Compose("ㅎ"); p.Compose("하"); p.TypeText("하"); p.Compose("느"); p.TypeText("늘");
                var creator = (CharacterCreatorScreen)p.Screens.Find(ScreenCatalog.CharacterCreator);
                Assert.That(creator.PlayerName, Is.EqualTo("하늘"));
                p.PressKey(TestKey.F1); // 키 배정이 새지 않는다: 글자 칸에 포커스가 있으면 단축키 안내도 열리지 않는다(드라이버의 키 누름은 글자를 만들지 않는다)
                Assert.That(p.Screens.IsOpen(ScreenCatalog.KeyGuide), Is.False);
                Assert.That(p.Screens.Tree.Focused?.Id, Is.EqualTo(CharacterCreatorScreen.NameFieldId));
                p.PressKey(TestKey.Down); // 막는 화면: 아래 키만으로 칸을 나온다
                Assert.That(p.Game.TextInputActive, Is.False, "칸을 나오면 입력기가 꺼진다");
                p.ScreenshotScreen(ScreenCatalog.CharacterCreator, "keyboard-01-creator-focus");
                p.PressByKeys(CharacterCreatorScreen.ConfirmId);
                p.Frames(3);
                Assert.That(p.Game.World, Is.Not.Null, "시작하기로 월드에 들어간다");
                steps.Add("새 게임·이름: 됨");

                // 2. 집에서 일과 고르기(이동을 막지 않는 패널: Shift+방향 키, Enter). 닫기도 키로.
                p.WalkToThing(DailyPlanScreen.InteractionKind, null);
                p.PressKey(TestKey.E);
                p.WaitForScreen(ScreenCatalog.DailyPlan);
                var plan = (DailyPlanScreen)p.Screens.Find(ScreenCatalog.DailyPlan);
                var routes = (UiCardGrid)p.Node(DailyPlanScreen.RouteGridId);
                Assert.That(p.Screens.Tree.Focused, Is.SameAs(routes), "일과 칸에서 시작한다");
                p.KeyDown(TestKey.LeftShift); p.PressKey(TestKey.Down); p.PressKey(TestKey.Down); p.KeyUp(TestKey.LeftShift); // 테두리 → 첫 카드
                Assert.That(routes.SelectedIndex, Is.EqualTo(0));
                p.PressKey(TestKey.Enter);
                Assert.That(p.Game.Session.DailyPlan.Slots.Any(s => !string.IsNullOrEmpty(s.RouteId)), Is.True, "Enter로 일과를 칸에 넣었다");
                p.ScreenshotScreen(ScreenCatalog.DailyPlan, "keyboard-02-daily-plan");
                p.PressByKeys(DailyPlanScreen.CloseBottomId);
                p.WaitForScreenClosed(ScreenCatalog.DailyPlan);
                steps.Add("일과 고르기: 됨");

                // 3. 마을로(방향 키로 걸어 문).
                p.GoToByKeys("Town");
                steps.Add("마을로: 됨");

                // 4. 알바 미니게임(이동을 막는 모달: 방향 키, Enter).
                PlayShiftWithKeys(p);
                steps.Add("알바 미니게임: 됨");

                // 5. 가게에서 사기(장소 모달: 목록에서 아래 키로 줄을 고르고 Enter). 돈이 모자라면 한 번 더 일한다(기획 수치 — 키보드와 무관).
                var offers = p.Game.Session.FurnitureForSaleAt(ShopLocation);
                int price = offers.Count == 0 ? int.MaxValue : offers.Min(o => o.Price);
                for (int shift = 0; shift < 2 && Allowance(p) < price; shift++) PlayShiftWithKeys(p);
                p.WalkToThing(LocationScreen.InteractionKind, ShopLocation);
                p.PressKey(TestKey.E);
                p.WaitForScreen(ScreenCatalog.Location);
                var location = (LocationScreen)p.Screens.Find(ScreenCatalog.Location);
                var rows = (UiCardGrid)p.Node(LocationScreen.RowsId);
                int row = location.Model.Rows.ToList().FindIndex(r => r.Kind == LocationRowKind.Offer && r.Enabled);
                if (row < 0) steps.Add("가게에서 사기: 살 수 있는 물건이 없다(돈 " + Allowance(p) + " / 값 " + price + ") — 키보드와 무관");
                else
                {
                    int before = Allowance(p);
                    p.FocusByKeys(LocationScreen.RowsId);
                    p.SelectRowByKeys(rows, row);
                    UiRect shown = rows.CellRect(row);
                    Assert.That(shown.Y >= rows.View.Y - 0.5f && shown.Bottom <= rows.View.Bottom + 0.5f, Is.True, "고른 줄이 보이게 스크롤이 따라왔다");
                    p.ScreenshotScreen(ScreenCatalog.Location, "keyboard-03-shop-row");
                    p.PressKey(TestKey.Enter);
                    p.Frames(2);
                    Assert.That(Allowance(p), Is.LessThan(before), "Enter로 샀다");
                    steps.Add("가게에서 사기: 됨");
                }
                p.PressByKeys(LocationScreen.CloseId);
                p.WaitForScreenClosed(ScreenCatalog.Location);
                p.DismissPopupsByKeys();

                // 6. 상자 놓고 넣기: 가구 배치는 HUD 단추로만 열리고(HUD는 키보드 포커스를 받지 않는다), 방의 칸은 포인터로만 고른다 — 막힘.
                p.GoToByKeys("House");
                Assert.That(p.Screens.FocusScreen, Is.Null, "월드에서는 포커스를 받는 화면이 없다");
                foreach (TestKey key in new[] { TestKey.Tab, TestKey.Enter })
                {
                    p.KeyDown(TestKey.LeftShift); p.PressKey(TestKey.Right); p.KeyUp(TestKey.LeftShift);
                    Assert.That(p.Screens.IsOpen(ScreenCatalog.FurniturePlacement), Is.False);
                    if (key == TestKey.Tab) { p.PressKey(TestKey.Tab); p.WaitForScreen(ScreenCatalog.ObjectiveJournal); p.PressKey(TestKey.Tab); p.WaitForScreenClosed(ScreenCatalog.ObjectiveJournal); }
                }
                Assert.That(p.Screens.IsOpen(ScreenCatalog.FurniturePlacement), Is.False, "키보드로는 가구 배치를 열 길이 없다");
                steps.Add("상자 놓고 넣기: 막힘(가구 배치를 여는 키가 없다 — HUD 단추만, 방의 칸은 포인터만)");

                // 7. 자기: 잠자리까지 걸어 E → 하루 결과(다음 날 단추에서 시작) → Enter.
                DayResultScreen result = p.GoToBed();
                Assert.That(p.Screens.Tree.Focused?.Id, Is.EqualTo(DayResultScreen.NextDayId), "하루 결과는 다음 날 단추에서 시작한다");
                p.PressKey(TestKey.Down); // 첫 키는 테두리를 보인다
                p.ScreenshotScreen(ScreenCatalog.DayResult, "keyboard-04-day-result");
                p.PressKey(TestKey.Enter);
                p.WaitForScreenClosed(ScreenCatalog.DayResult);
                p.DismissPopupsByKeys();
                Assert.That(result.IsOpen, Is.False);
                steps.Add("자기: 됨");
            }

            // 8. 이어 하기: 다시 띄워 슬롯 선택에서 방향 키로 이어 하기 → Enter.
            using (PlayerDriver again = Launch())
            {
                again.PressByKeys(SaveSlotScreen.ContinueId(0));
                again.Frames(3);
                Assert.That(again.Game.World, Is.Not.Null, "이어 하기로 월드에 들어간다");
                Assert.That(again.Game.Session.Student.CurrentDay, Is.GreaterThanOrEqualTo(2), "잔 뒤의 날에서 이어 한다");
                again.Screenshot("keyboard-05-continued");
                steps.Add("이어 하기: 됨");
            }
            TestContext.WriteLine(string.Join("\n", steps));
        }
    }

    internal static class KeyboardPopups
    {
        /// <summary>집 ↔ 마을: 방향 키로 걸어 문을 지난다. 저절로 뜬 하루 이벤트는 키로 닫는다(PlayActions.GoTo는 포인터로 닫는다).</summary>
        public static void GoToByKeys(this PlayerDriver p, string map)
        {
            string door = map == "Town" ? "town" : "house";
            Assert.That(p.WalkThroughDoor(door), Is.EqualTo(map));
            p.DismissPopupsByKeys();
        }

        /// <summary>저절로 뜬 하루 이벤트가 있으면 키로 닫는다(닫기 단추까지 방향 키, Enter).</summary>
        public static void DismissPopupsByKeys(this PlayerDriver p)
        {
            p.Frames(2);
            if (!p.Screens.IsOpen(ScreenCatalog.DailyEvent)) return;
            p.PressByKeys(Rootborn.Screens.Dialogue.DailyEventScreen.CloseId);
            p.WaitForScreenClosed(ScreenCatalog.DailyEvent);
        }
    }
}
