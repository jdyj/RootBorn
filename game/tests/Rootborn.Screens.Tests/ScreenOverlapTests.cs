using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Tests.AdultFacility;
using Rootborn.Screens.Tests.Furniture;
using Rootborn.Screens.Tests.Hud;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// 동시에 떠 있을 수 있는 화면들이 서로 겹쳐 보이지 않는지: HUD 전체(추적 목표 포함) 위에 등록 표의 패널·모달을 하나씩 띄우고(집과 마을,
    /// 1920x1080·1280x720·1920x1200·2560x1080·1280x800의 논리 크기), 보이는 겉면(UiSurfaces)끼리 겹치는 곳을 찾는다. 화면 목록은 ScreenCatalog에서 읽는다
    /// (월드 없이 뜨는 시작 화면은 HUD와 함께 뜨지 않으므로 뺀다). 열려면 상호작용이나 예약이 필요한 화면은 Openers에 여는 법을 둔다.
    /// 패널·모달이 덮는 HUD 요소는 화면 쌓기가 숨긴다(ScreenStack.UpdateCoveredHud). 그래서 여기서 걸리는 것은
    /// HUD끼리의 겹침, 덮였는데도 보이는 HUD, 화면 밖으로 나간 패널, 그리고 연 화면을 끄는 HUD 단추가 덮이는 것이다. 자동 테스트다.
    /// </summary>
    public sealed class ScreenOverlapTests
    {
        /// <summary>16:9 둘과 16:10·21:9(창 비율이 다르면 논리 크기의 폭이나 높이가 늘어난다).</summary>
        private static readonly (int W, int H)[] Sizes = { (1920, 1080), (1280, 720), (1920, 1200), (2560, 1080), (1280, 800) };

        /// <summary>그냥 Open(id)로는 내용이 없어 바로 닫히는 화면: 실제로 여는 길(상호작용, 예약).</summary>
        private static readonly Dictionary<string, Action<TestSession>> Openers = new Dictionary<string, Action<TestSession>>(StringComparer.Ordinal)
        {
            [ScreenCatalog.Dialogue] = t => t.Stack.Interaction(new InteractionEvent("House", "npc", "npc.first-guide")),
            [ScreenCatalog.Location] = t => t.Stack.Interaction(new InteractionEvent("Town", "location-activity", "location.library")),
            [ScreenCatalog.Minigame] = t => t.Stack.Interaction(new InteractionEvent("Town", "part-time-work", "work.shift.corner-store")),
            [ScreenCatalog.InteractionRefusal] = t => Rootborn.Screens.Hud.InteractionRefusals.Show(t.Services, "ui.prompt.no-time"),
        };

        /// <summary>시험 준비가 따로 필요한 화면(성인 단계): 따로 연다.</summary>
        private static readonly string[] OwnFixture = { ScreenCatalog.Facility };

        public static IEnumerable<TestCaseData> Cases()
        {
            foreach (bool house in new[] { true, false })
                foreach ((int w, int h) in Sizes)
                    yield return new TestCaseData(house, w, h).SetName($"Screens_DoNotOverlap_{(house ? "House" : "Town")}_{w}x{h}");
        }

        [TestCaseSource(nameof(Cases))]
        public void Screens_DoNotOverlap(bool house, int width, int height)
        {
            ScreenFit fit = ScreenFit.For(width, height);
            var found = new List<string>();
            var checkedScreens = new List<string>();
            string where = (house ? "집" : "마을") + $" {width}x{height}";

            using (TestSession shared = World(house, fit))
            {
                Assert.That(shared.Screen<TrackedObjectiveHudScreen>(ScreenCatalog.HudTracked).IsShown, Is.True, "추적 목표 HUD도 띄운다");
                Inspect(shared.Stack, null, fit, where, found);
                foreach (ScreenEntry entry in ScreenCatalog.Create().Screens)
                {
                    if (entry.Layer == ScreenLayer.Hud || entry.WithoutWorld || OwnFixture.Contains(entry.Id)) continue;
                    bool own = Openers.TryGetValue(entry.Id, out Action<TestSession> open);
                    TestSession fresh = own ? World(house, fit) : null; // 상호작용·예약으로 여는 화면은 새 판에서(앞 화면이 쓴 시간·예약에 기대지 않는다)
                    try
                    {
                        TestSession t = fresh ?? shared;
                        if (own) open(t);
                        else t.Stack.Open(entry.Id);
                        t.Emit(); t.Emit();
                        Assert.That(t.Stack.IsOpen(entry.Id), Is.True, entry.Id + "가 열려야 한다(여는 법이 필요하면 Openers에)");
                        Inspect(t.Stack, entry.Id, fit, where, found);
                        checkedScreens.Add(entry.Id);
                        foreach (GameScreen screen in t.Stack.OpenScreens.Where(x => x.Layer != ScreenLayer.Hud).Reverse().ToList()) t.Stack.Close(screen);
                    }
                    finally { fresh?.Dispose(); }
                }
            }

            using (var f = new FacilityFixture())
            {
                f.Stack.Layout(fit.LogicalWidth, fit.LogicalHeight);
                f.Stack.Interaction(new InteractionEvent("Town", Rootborn.Screens.AdultFacility.FacilityScreen.InteractionKind, FacilityFixture.Chef));
                Assert.That(f.Stack.IsOpen(ScreenCatalog.Facility), Is.True);
                f.Stack.Emit(new DrawList(), new TestMeasurer());
                Inspect(f.Stack, ScreenCatalog.Facility, fit, where, found);
                checkedScreens.Add(ScreenCatalog.Facility);
            }

            var expected = ScreenCatalog.Create().Screens.Where(e => e.Layer != ScreenLayer.Hud && !e.WithoutWorld).Select(e => e.Id);
            Assert.That(checkedScreens.OrderBy(s => s), Is.EqualTo(expected.OrderBy(s => s)), "등록된 모든 패널·모달을 봤다");
            Assert.That(found, Is.Empty, "겹침:\n" + string.Join("\n", found));
        }

        /// <summary>새 게임, 그 맵에 들어가 HUD를 띄우고 수첩에서 메인 체인을 추적한다(추적 목표 HUD가 보인다).</summary>
        private static TestSession World(bool house, ScreenFit fit)
        {
            var t = new TestSession();
            if (house)
            {
                var room = new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap());
                t.Session.EnterHouse(room.Grid, "house");
                t.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
            }
            else t.Stack.EnterMap("Town");
            t.Stack.OpenHud();
            t.Session.TalkTo("npc.first-guide");
            TrackedObjectiveHudTests.TrackMainChainFromJournal(t);
            t.Stack.Layout(fit.LogicalWidth, fit.LogicalHeight);
            t.Emit();
            t.Stack.Layout(fit.LogicalWidth, fit.LogicalHeight);
            return t;
        }

        private static void Inspect(ScreenStack stack, string openId, ScreenFit fit, string where, List<string> found)
        {
            var hud = new List<(string Screen, UiNode Node)>();
            var others = new List<(string Screen, UiNode Node)>();
            foreach (GameScreen screen in stack.OpenScreens)
            {
                var surfaces = new List<UiNode>();
                UiSurfaces.Collect(screen.Root, surfaces);
                (screen.Layer == ScreenLayer.Hud ? hud : others).AddRange(surfaces.Select(n => (screen.Id, n)));
            }
            string label = where + " [" + (openId ?? "HUD만") + "] ";

            for (int i = 0; i < hud.Count; i++)
                for (int j = i + 1; j < hud.Count; j++)
                    if (UiSurfaces.Overlap(hud[i].Node.Rect, hud[j].Node.Rect))
                        found.Add(label + Name(hud[i]) + " + " + Name(hud[j]) + " | HUD끼리 겹침");
            foreach (var h in hud)
                if (OffScreen(h.Node.Rect, fit)) found.Add(label + Name(h) + " | HUD가 화면 밖");

            foreach (var o in others)
            {
                foreach (var h in hud)
                    if (UiSurfaces.Overlap(o.Node.Rect, h.Node.Rect)) found.Add(label + Name(o) + " + " + Name(h) + " | 패널이 보이는 HUD와 겹침");
                if (OffScreen(o.Node.Rect, fit)) found.Add(label + Name(o) + " | 화면 밖");
            }

            // 연 화면을 끄는 HUD 단추(켬/끔)는 그 화면이 떠 있는 동안 보여야 한다.
            if (openId == null) return;
            foreach (HudButtonSpec spec in HudButtonsScreen.Buttons)
            {
                if (spec.Action != HudButtonAction.Toggle || spec.ScreenId != openId) continue;
                UiNode button = stack.Tree.Root.Find(spec.Id);
                if (button != null && button.Visible && button.Covered) found.Add(label + spec.Id + " | 연 화면이 그 켬/끔 단추를 덮음");
            }
        }

        private static bool OffScreen(UiRect r, ScreenFit fit) =>
            r.X < -0.5f || r.Y < -0.5f || r.Right > fit.LogicalWidth + 0.5f || r.Bottom > fit.LogicalHeight + 0.5f;

        private static string Name((string Screen, UiNode Node) s) => s.Screen + ":" + (s.Node.Id ?? s.Node.GetType().Name);

        [Test]
        public void ThePanelCoversTheHudItOverlaps_AndTheHudComesBackWhenItCloses()
        {
            using TestSession t = World(house: false, ScreenFit.For(1920, 1080));
            UiNode clock = t.Node(ClockHudScreen.PanelId);
            Assert.That(clock.Covered, Is.False);
            t.Stack.Key(ScreenKey.Inventory, false);
            Assert.That(UiSurfaces.Overlap(t.Node("inventory").Rect, clock.Rect), Is.True, "가방이 시계 칸 아래쪽에 걸친다");
            Assert.That(clock.Covered, Is.True, "덮인 HUD는 숨는다");
            string clockText = ((UiLabel)t.Node(ClockHudScreen.LabelId)).Text;
            Assert.That(clockText, Is.Not.Empty);
            Assert.That(t.Texts(t.Emit()), Has.None.EqualTo(clockText), "덮인 시계 글자는 그리지 않는다");
            Assert.That(t.Stack.Tree.HitTest(clock.Rect.CenterX, clock.Rect.Y + 4), Is.Not.SameAs(clock), "덮인 HUD는 포인터를 받지 않는다");
            t.Stack.Key(ScreenKey.Inventory, false);
            Assert.That(clock.Covered, Is.False, "닫으면 다시 보인다");
        }

        [Test]
        public void EnergyAndFocus_HideTogether_WhenAPanelCoversOnlyOne()
        {
            using TestSession t = World(house: false, ScreenFit.For(1920, 1080));
            UiNode energy = t.Node("hud.energy"), focus = t.Node("hud.focus");
            t.Stack.Key(ScreenKey.Inventory, false);
            Assert.That(UiSurfaces.Overlap(t.Node("inventory").Rect, energy.Rect), Is.True);
            Assert.That(UiSurfaces.Overlap(t.Node("inventory").Rect, focus.Rect), Is.False, "가방은 집중 칸에 닿지 않는다");
            Assert.That((energy.Covered, focus.Covered), Is.EqualTo((true, true)), "한 묶음으로 숨는다");
            Assert.That(t.Node(ClockHudScreen.PanelId).Covered, Is.True);
            Assert.That(t.Node("hud.button.encyclopedia").Covered, Is.False, "버튼 줄은 덮인 것만 숨는다");
        }

        [Test]
        public void TheChecker_FindsOverlappingHud()
        {
            var stack = new ScreenStack(ProbeRegistry(), new ScreenServices(TestData.Text("ko"), null));
            stack.Layout(1920, 1080);
            stack.OpenHud();
            var found = new List<string>();
            Inspect(stack, null, ScreenFit.For(1920, 1080), "시험", found);
            Assert.That(found, Is.EquivalentTo(new[] { "시험 [HUD만] probe.a:a + probe.b:b | HUD끼리 겹침" }));
        }

        private static ScreenRegistry ProbeRegistry()
        {
            var r = new ScreenRegistry();
            r.Add("probe.a", ScreenLayer.Hud, false, s => new Probe(s, "a", 0));
            r.Add("probe.b", ScreenLayer.Hud, false, s => new Probe(s, "b", 50));
            return r;
        }

        private sealed class Probe : GameScreen
        {
            private readonly string _id;
            private readonly float _x;
            public Probe(ScreenServices s, string id, float x) : base(s) { _id = id; _x = x; }

            protected override UiNode Build()
            {
                var root = new UiNode();
                root.Add(new UiPanel { Id = _id }).Place(0, 0, _x, 0, 100, 100);
                return root;
            }
        }
    }
}
