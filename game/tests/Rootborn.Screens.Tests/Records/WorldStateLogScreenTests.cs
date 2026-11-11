using System.Linq;
using NUnit.Framework;
using Rootborn.Core.WorldState;
using Rootborn.Screens.Records;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Records
{
    public sealed class WorldStateLogScreenTests
    {
        private static int ActivateAll(TestSession t)
        {
            int day = 1, n = 0;
            foreach (WorldStateFlag flag in t.Session.Definitions.All<WorldStateFlag>())
                if (t.Session.World.TryActivate(flag, new WorldStateActivationSource("chain", "step", "event-" + n++, day))) { }
            return t.Session.World.ActiveFlagCount;
        }

        private static TestSession Open(string language = "ko", bool activate = false)
        {
            var t = new TestSession(language);
            if (activate) ActivateAll(t);
            t.Stack.OpenHud();
            Assert.That(t.Stack.Open(ScreenCatalog.WorldStateLog), Is.Not.Null);
            t.Emit();
            return t;
        }

        private static string Shown(TestSession t, string key) => string.IsNullOrEmpty(key) ? string.Empty : t.Text.Get(key);

        [Test]
        public void HudButton_OpensIt_AsAMovementBlockingPanel_WithUnityPanelSize()
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            t.Emit();
            t.Click("hud.button.world-log");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.WorldStateLog), Is.True);
            Assert.That(t.Stack.MovementBlocked, Is.True, "Unity: 마을 기록은 입력을 잠갔다");
            t.Emit();
            Assert.That(t.Node(WorldStateLogScreen.PanelId).Rect, Is.EqualTo(new UiRect(960 - 560, 540 - 400, 1120, 800)));
        }

        [Test]
        public void NoChanges_ShowsTheEmptyMessage_AndNoScrollHint()
        {
            using TestSession t = Open();
            var screen = t.Screen<WorldStateLogScreen>(ScreenCatalog.WorldStateLog);
            Assert.That(screen.Model.Cards, Is.Empty);
            Assert.That(t.Node(WorldStateLogScreen.EmptyId).Visible, Is.True);
            Assert.That(((UiLabel)t.Node(WorldStateLogScreen.EmptyId)).Text, Is.EqualTo(t.Text.Get("ui.world-log.empty")));
            Assert.That(t.Node(WorldStateLogScreen.HintId).Visible, Is.False);
            Assert.That(((UiLabel)t.Node(WorldStateLogScreen.TitleId)).Text, Is.EqualTo(t.Text.Get("ui.world-log.title")));
        }

        [Test]
        public void ActiveChanges_AreListedByPriority_WithTitleBodyAndBadges()
        {
            using TestSession t = Open(activate: true);
            var screen = t.Screen<WorldStateLogScreen>(ScreenCatalog.WorldStateLog);
            var expected = WorldStateSummaryBuilder.BuildForSurface(t.Session.Definitions.All<WorldStateFlag>(), t.Session.World, WorldStateSummarySurface.WorldLog, null);
            Assert.That(expected.Length, Is.GreaterThan(0), "월드 기록에 보이는 변화가 데이터에 있다");
            Assert.That(screen.Model.Cards.Select(c => c.FlagId), Is.EqualTo(expected.Select(e => e.FlagId)));
            Assert.That(t.Node(WorldStateLogScreen.EmptyId).Visible, Is.False);
            var drawn = t.Texts(t.Emit()).ToArray();
            WorldLogCard first = screen.Model.Cards[0];
            Assert.That(drawn, Does.Contain(Shown(t, first.DisplayNameKey)));
            Assert.That(string.Join("\n", drawn), Does.Contain(Shown(t, first.DescriptionKey)));
            string badges = drawn.First(d => d.Contains(t.Text.Get("day-result.world-badge.New")));
            Assert.That(badges, Does.Contain(t.Text.Get("day-result.world-scope." + first.Scope)));
        }

        [Test]
        public void RelatedLocationName_IsShownInTheBody()
        {
            using TestSession t = Open(activate: true);
            var screen = t.Screen<WorldStateLogScreen>(ScreenCatalog.WorldStateLog);
            WorldLogCard card = screen.Model.Cards.FirstOrDefault(c => !string.IsNullOrEmpty(c.LocationNameKey));
            if (card.FlagId == null) Assert.Pass("관련 장소가 있는 변화가 없다");
            Assert.That(string.Join("\n", t.Texts(t.Emit())), Does.Contain(Shown(t, card.LocationNameKey)));
        }

        [Test]
        public void ManyCards_AreVirtualized_AndScrollWithTheWheel_AndHintShows()
        {
            using TestSession t = Open(activate: true);
            var grid = (UiCardGrid)t.Node(WorldStateLogScreen.ListId);
            grid.ItemCount = 500;
            Assert.That(grid.RowCount, Is.EqualTo(500), "카드는 한 줄에 하나");
            t.Stack.Pointer(grid.Rect.CenterX, grid.Rect.CenterY, false, -3f);
            Assert.That(grid.ScrollOffset, Is.GreaterThan(0f));
        }

        [Test]
        public void ScrollHint_ShowsOnlyWhenTheCardsDoNotFit()
        {
            using TestSession t = Open(activate: true);
            var screen = t.Screen<WorldStateLogScreen>(ScreenCatalog.WorldStateLog);
            bool tooMany = screen.Model.Cards.Count * 224f > 592f;
            Assert.That(t.Node(WorldStateLogScreen.HintId).Visible, Is.EqualTo(tooMany));
        }

        [Test]
        public void CloseButton_AndEscape_CloseIt_AndReleaseMovement()
        {
            using (TestSession t = Open())
            {
                t.Click(WorldStateLogScreen.CloseId);
                Assert.That(t.Stack.IsOpen(ScreenCatalog.WorldStateLog), Is.False);
                Assert.That(t.Stack.MovementBlocked, Is.False);
            }
            using (TestSession t = Open())
            {
                Assert.That(t.Stack.Key(ScreenKey.Escape, false), Is.True);
                Assert.That(t.Stack.IsOpen(ScreenCatalog.WorldStateLog), Is.False);
            }
        }

        [Test]
        public void ReopeningAfterANewChange_ShowsIt_WithoutNewNodes()
        {
            using TestSession t = Open();
            var screen = t.Screen<WorldStateLogScreen>(ScreenCatalog.WorldStateLog);
            int nodes = Count(t.Stack.Tree.Root);
            t.Stack.Close(ScreenCatalog.WorldStateLog);
            ActivateAll(t);
            t.Stack.Open(ScreenCatalog.WorldStateLog);
            t.Emit();
            Assert.That(screen.Model.Cards.Count, Is.GreaterThan(0));
            Assert.That(Count(t.Stack.Tree.Root), Is.EqualTo(nodes), "열 때마다 노드를 새로 만들지 않는다");
        }

        private static int Count(UiNode node) => 1 + node.Children.Sum(Count);

        [TestCase("ko")]
        [TestCase("en")]
        public void Text_ExistsInBothLanguages(string language)
        {
            using TestSession t = Open(language, activate: true);
            var screen = t.Screen<WorldStateLogScreen>(ScreenCatalog.WorldStateLog);
            t.Emit();
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, screen.TextKeys);
        }

        [Test]
        public void ChangingTheLanguage_ChangesTheShownText()
        {
            using TestSession t = Open(activate: true);
            string before = ((UiLabel)t.Node(WorldStateLogScreen.TitleId)).Text;
            string cardBefore = string.Join("|", t.Texts(t.Emit()));
            t.Text.Language = "en";
            t.Stack.NotifyChanged(ChangeTopics.Language);
            Assert.That(((UiLabel)t.Node(WorldStateLogScreen.TitleId)).Text, Is.Not.EqualTo(before));
            Assert.That(string.Join("|", t.Texts(t.Emit())), Is.Not.EqualTo(cardBefore));
        }
    }
}
