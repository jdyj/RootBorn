using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Records;
using Rootborn.Screens.Tests.Hud;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Records
{
    /// <summary>
    /// 장소 기록을 여는 길(사용자 결정 2026-11-12: 키로도, 다른 데서도): L 키(ScreenKey.LocationLog)와 HUD 버튼(Unity 판
    /// LocationStateRuntimeInstaller의 "장소 기록" 버튼, 문구 키 ui.location-log). 둘 다 열고 다시 누르면 닫는다(도감·옷장과 같은 켬/끔).
    /// 버튼은 오른쪽 위 열(도감·옷장) 아래에 놓아 여러 화면 비율에서 다른 HUD와 겹치지 않는다. 자동 테스트다(화면 근거 아님).
    /// </summary>
    public sealed class LocationStateLogOpenersTests
    {
        private const string ButtonId = "hud.button.location-log";

        private static TestSession World(bool visited = false, string language = "ko")
        {
            var t = new TestSession(language);
            if (visited) t.Session.VisitLocation("location.library");
            t.Stack.EnterMap("Town");
            t.Stack.OpenHud();
            t.Emit();
            return t;
        }

        private static void Click(TestSession t, string id)
        {
            UiRect r = t.Node(id).Rect;
            t.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
            t.Stack.Pointer(r.CenterX, r.CenterY, true, 0);
            t.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
        }

        [Test]
        public void LocationLogKey_OpensIt_AndPressingAgainClosesIt()
        {
            using TestSession t = World();
            Assert.That(ScreenCatalog.Create().TryGetByKey(ScreenKey.LocationLog, out string id), Is.True);
            Assert.That(id, Is.EqualTo(ScreenCatalog.LocationStateLog));

            Assert.That(t.Stack.Key(ScreenKey.LocationLog, false), Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.LocationStateLog), Is.True);
            Assert.That(t.Stack.MovementBlocked, Is.False, "Unity: 장소 기록 패널은 입력을 잠그지 않았다");
            Assert.That(t.Stack.Key(ScreenKey.LocationLog, false), Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.LocationStateLog), Is.False, "같은 키로 닫힌다");
        }

        [Test]
        public void LocationLogKey_ClosesAnotherBigPanel_LikeTheOtherPanelKeys()
        {
            using TestSession t = World();
            t.Stack.Key(ScreenKey.Inventory, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.True);
            t.Stack.Key(ScreenKey.LocationLog, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.LocationStateLog), Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.False, "큰 패널은 한 번에 하나");
        }

        [Test]
        public void HudButton_OpensIt_AndPressingAgainClosesIt()
        {
            using TestSession t = World();
            var button = (UiButton)t.Node(ButtonId);
            Assert.That(button.Visible, Is.True, "마을에서도 집에서도 보인다(맵 성질 조건 없음)");
            Assert.That(button.Text, Is.EqualTo(t.Text.Get("ui.location-log")));

            Click(t, ButtonId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.LocationStateLog), Is.True);
            t.Emit();
            Assert.That(t.Node(ButtonId).Covered, Is.False, "패널이 켬/끔 단추를 덮지 않는다");
            Click(t, ButtonId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.LocationStateLog), Is.False);
        }

        [Test]
        public void OpenedByTheButton_ShowsTheEmptyMessage_WhenNothingWasVisited()
        {
            using TestSession t = World();
            Click(t, ButtonId);
            t.Emit();
            Assert.That(t.Screen<LocationStateLogScreen>(ScreenCatalog.LocationStateLog).Model.Rows, Is.Empty);
            Assert.That(t.Node(LocationStateLogScreen.EmptyId).Visible, Is.True);
            Assert.That(t.Texts(t.Emit()), Has.Member(t.Text.Get("ui.location-log.empty")));
        }

        [Test]
        public void OpenedByTheKey_ShowsTodaysStates_FromTheSession()
        {
            using TestSession t = World(visited: true);
            t.Stack.Key(ScreenKey.LocationLog, false);
            t.Emit();
            var screen = t.Screen<LocationStateLogScreen>(ScreenCatalog.LocationStateLog);
            Assert.That(screen.Model.Rows.Select(r => r.StateId), Is.EqualTo(t.Session.LocationStates.TodaySummary.Select(s => s.StateId)));
            Assert.That(screen.Model.Rows, Is.Not.Empty);
            Assert.That(t.Node(LocationStateLogScreen.EmptyId).Visible, Is.False);
            Assert.That(t.Texts(t.Emit()), Has.Member(t.Text.Get(screen.Model.Rows[0].TitleKey)));
        }

        private static IEnumerable<string> Languages() => LongText.MergedTables().Keys.OrderBy(k => k);

        [TestCaseSource(nameof(Languages))]
        public void ButtonText_ExistsInEveryLanguage(string language)
        {
            using TestSession t = World(language: language);
            HudButtonSpec spec = HudButtonsScreen.Buttons.Single(b => b.Id == ButtonId);
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, new[] { spec.LabelKey });
            Assert.That(t.Text.Get(spec.LabelKey), Is.Not.EqualTo(spec.LabelKey));
        }

        [TestCaseSource(nameof(Languages))]
        public void DoubleLengthButtonText_StaysInsideTheButton(string language)
        {
            using var t = new TestSession(language);
            var stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(LongText.Create(language), t.Session));
            stack.Layout(1920, 1080);
            stack.EnterMap("Town");
            stack.OpenHud();
            var measurer = new WideMeasurer();
            stack.Emit(new DrawList(), measurer);
            UiNode button = stack.Tree.Root.Find(ButtonId);
            var list = new DrawList();
            button.Emit(list, measurer);
            List<DrawCommand> texts = list.Commands.Where(c => c.Kind == DrawKind.Text).ToList();
            Assert.That(texts, Is.Not.Empty);
            foreach (DrawCommand text in texts)
            {
                float right = text.Rect.X + measurer.Width(text.Text, text.FontSize);
                Assert.That(text.Rect.X, Is.GreaterThanOrEqualTo(button.Rect.X - 0.5f));
                Assert.That(right, Is.LessThanOrEqualTo(button.Rect.Right + 0.5f), "2배 문구는 줄이거나 말줄임한다: " + text.Text);
            }
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(1920, 1200)]
        [TestCase(2560, 1080)]
        public void HudButton_DoesNotOverlapOtherHud_AtThisWindowSize(int width, int height)
        {
            ScreenFit fit = ScreenFit.For(width, height);
            foreach (bool house in new[] { false, true })
            {
                using var t = new TestSession();
                if (house) t.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
                else t.Stack.EnterMap("Town");
                t.Stack.OpenHud();
                t.Session.TalkTo("npc.first-guide");
                TrackedObjectiveHudTests.TrackMainChainFromJournal(t);
                t.Stack.Layout(fit.LogicalWidth, fit.LogicalHeight);
                t.Emit();
                t.Stack.Layout(fit.LogicalWidth, fit.LogicalHeight);

                UiNode button = t.Node(ButtonId);
                UiRect r = button.Rect;
                Assert.That(r.X >= 0 && r.Y >= 0 && r.Right <= fit.LogicalWidth && r.Bottom <= fit.LogicalHeight, Is.True, "화면 안: " + r);
                var others = new List<UiNode>();
                foreach (GameScreen screen in t.Stack.OpenScreens.Where(s => s.Layer == ScreenLayer.Hud)) UiSurfaces.Collect(screen.Root, others);
                List<string> hits = others.Where(n => !ReferenceEquals(n, button) && n.Visible && UiSurfaces.Overlap(n.Rect, r)).Select(n => n.Id ?? n.GetType().Name).ToList();
                Assert.That(hits, Is.Empty, (house ? "집" : "마을") + $" {width}x{height}: 장소 기록 버튼과 겹친 HUD");
            }
        }
    }
}
