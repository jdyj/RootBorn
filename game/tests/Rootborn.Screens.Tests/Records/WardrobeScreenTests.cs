using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Characters;
using Rootborn.Screens.Records;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Records
{
    /// <summary>실제 data/의 칸·옷 규칙으로 도는 시험용 옷장 뒤쪽(메모리에만 저장). 미리보기 그림은 옷 id로 만든 가짜 PNG 이름이다.</summary>
    internal sealed class TestWardrobe : IWardrobeBackend
    {
        private readonly WearableState _state = new WearableState();
        public int Chosen { get; private set; }

        public TestWardrobe(Rootborn.Game.GameSession session)
        {
            Catalog = new WearableCatalog(session.Definitions.All<WearableSlotRule>(), session.Definitions.All<WearableRule>(), session.Definitions.All<GrowthStageRule>());
            StageId = session.GrowthStageId();
        }

        public WearableCatalog Catalog { get; }
        public string StageId { get; }

        public IReadOnlyCollection<string> WornIds =>
            WearableRules.ResolveLayers(Catalog, _state, StageId, string.Empty).Select(l => l.WearableId).ToArray();

        public IReadOnlyList<WardrobeSprite> Preview(string wearableId)
        {
            var preview = WearableState.FromEntries(_state.ToEntries());
            WearableRule rule = Catalog.FindWearable(wearableId);
            WearableRules.Choose(Catalog, preview, rule.SlotId, wearableId, StageId);
            var sprites = new List<WardrobeSprite> { new WardrobeSprite("test/body.png", new PixelRect(0, 0, 32, 48)) };
            foreach (WearableLayer layer in WearableRules.ResolveLayers(Catalog, preview, StageId, string.Empty))
                sprites.Add(new WardrobeSprite("test/" + layer.WearableId + ".png", new PixelRect(0, 0, 32, 48)));
            return sprites;
        }

        public bool Choose(string wearableId)
        {
            WearableRule rule = Catalog.FindWearable(wearableId);
            if (rule == null || !WearableRules.Choose(Catalog, _state, rule.SlotId, wearableId, StageId)) return false;
            Chosen++;
            return true;
        }
    }

    public sealed class WardrobeScreenTests
    {
        private static TestSession Open(string language = "ko")
        {
            var t = new TestSession(language);
            t.Services.Wardrobe = new TestWardrobe(t.Session);
            t.Stack.OpenHud();
            Assert.That(t.Stack.Open(ScreenCatalog.Wardrobe), Is.Not.Null);
            t.Emit();
            return t;
        }

        [Test]
        public void HudButton_OpensIt_AsAMovementBlockingPanel_CenteredWithUnityWidth()
        {
            using var t = new TestSession();
            t.Services.Wardrobe = new TestWardrobe(t.Session);
            t.Stack.OpenHud();
            t.Emit();
            t.Click("hud.button.wardrobe");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Wardrobe), Is.True);
            Assert.That(t.Stack.MovementBlocked, Is.True, "Unity: 옷장은 입력을 잠갔다");
            t.Emit();
            UiRect panel = t.Node(WardrobeScreen.PanelId).Rect;
            Assert.That(panel.W, Is.EqualTo(1040f));
            Assert.That(panel.CenterX, Is.EqualTo(960f).Within(0.01f));
            Assert.That(panel.CenterY, Is.EqualTo(540f).Within(0.01f));
            Assert.That(panel.H, Is.GreaterThanOrEqualTo(420f));
        }

        [Test]
        public void ShowsOneRowPerSlotWithChoices_CardsInDataOrder_WornOnesMarked()
        {
            using TestSession t = Open();
            var screen = t.Screen<WardrobeScreen>(ScreenCatalog.Wardrobe);
            var backend = (TestWardrobe)t.Services.Wardrobe;
            string stage = backend.StageId;
            int most = backend.Catalog.Slots.Max(s => backend.Catalog.Choices(s.Id, stage).Count);
            int minimum = most >= 2 ? 2 : 1;
            string[] expectedSlots = backend.Catalog.Slots.Where(s => backend.Catalog.Choices(s.Id, stage).Count >= minimum).Select(s => s.Id).ToArray();
            Assert.That(screen.Sections.Count, Is.EqualTo(expectedSlots.Length));
            Assert.That(screen.Sections.Count, Is.GreaterThan(0), "옷장이 비지 않는다");
            Assert.That(screen.VisibleCardCount, Is.EqualTo(screen.Sections.Sum(s => s.Cards.Count)));
            foreach (WardrobeSectionModel section in screen.Sections)
                Assert.That(section.Cards.Count(c => c.Selected), Is.LessThanOrEqualTo(1), "한 칸에서 입은 옷은 하나");
            Assert.That(backend.WornIds.All(id => screen.Sections.SelectMany(s => s.Cards).Where(c => c.Selected).Select(c => c.WearableId).Contains(id)
                || !screen.Sections.SelectMany(s => s.Cards).Any(c => c.WearableId == id)), Is.True);
        }

        [Test]
        public void ClickingACard_WearsIt_AndRedrawsWithThatCardSelected()
        {
            using TestSession t = Open();
            var screen = t.Screen<WardrobeScreen>(ScreenCatalog.Wardrobe);
            var backend = (TestWardrobe)t.Services.Wardrobe;
            WardrobeSectionModel section = screen.Sections.FirstOrDefault(s => s.Cards.Count >= 2);
            if (section == null) Assert.Pass("고를 옷이 둘 이상인 칸이 아직 없다(첫 단계)");
            WardrobeCardModel target = section.Cards.First(c => !c.Selected);
            int cardsBefore = screen.VisibleCardCount;
            t.Click(screen.CardIdOf(target.WearableId));
            Assert.That(backend.Chosen, Is.EqualTo(1));
            Assert.That(backend.WornIds, Does.Contain(target.WearableId));
            Assert.That(screen.Sections.SelectMany(s => s.Cards).Single(c => c.WearableId == target.WearableId).Selected, Is.True);
            Assert.That(screen.VisibleCardCount, Is.EqualTo(cardsBefore), "카드를 새로 만들지 않고 다시 쓴다");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Wardrobe), Is.True, "옷을 입어도 옷장은 열려 있다");
        }

        [Test]
        public void ReopeningDoesNotCreateNewNodes()
        {
            using TestSession t = Open();
            var screen = t.Screen<WardrobeScreen>(ScreenCatalog.Wardrobe);
            int nodes = Count(t.Stack.Tree.Root);
            t.Stack.Close(ScreenCatalog.Wardrobe);
            t.Stack.Open(ScreenCatalog.Wardrobe);
            t.Stack.Close(ScreenCatalog.Wardrobe);
            t.Stack.Open(ScreenCatalog.Wardrobe);
            Assert.That(Count(t.Stack.Tree.Root), Is.EqualTo(nodes));
            Assert.That(screen.VisibleCardCount, Is.GreaterThan(0));
        }

        private static int Count(UiNode node) => 1 + node.Children.Sum(Count);

        [Test]
        public void CardsShowThePreviewLayers_BodyThenClothes_FittedInsideTheUnityBox()
        {
            using TestSession t = Open();
            var screen = t.Screen<WardrobeScreen>(ScreenCatalog.Wardrobe);
            WardrobeCardModel card = screen.Sections[0].Cards[0];
            UiNode node = t.Node(screen.CardIdOf(card.WearableId));
            UiImage[] images = node.Children.OfType<UiImage>().Where(i => i.Visible).ToArray();
            Assert.That(images.Select(i => i.Image), Is.EqualTo(card.Sprites.Select(s => s.Image)));
            foreach (UiImage image in images)
            {
                Assert.That(image.Rect.W, Is.LessThanOrEqualTo(128.01f));
                Assert.That(image.Rect.H, Is.LessThanOrEqualTo(180.01f));
                Assert.That(image.Rect.Y, Is.GreaterThanOrEqualTo(node.Rect.Y));
                Assert.That(image.Rect.Bottom, Is.LessThanOrEqualTo(node.Rect.Bottom));
            }
        }

        [Test]
        public void CardsDoNotOverlap_AndStayInsideThePanel()
        {
            using TestSession t = Open();
            var screen = t.Screen<WardrobeScreen>(ScreenCatalog.Wardrobe);
            UiRect panel = t.Node(WardrobeScreen.PanelId).Rect;
            var rects = Enumerable.Range(0, screen.VisibleCardCount).Select(i => t.Node(WardrobeScreen.CardId(i)).Rect).ToArray();
            for (int i = 0; i < rects.Length; i++)
            {
                Assert.That(rects[i].X, Is.GreaterThanOrEqualTo(panel.X));
                Assert.That(rects[i].Right, Is.LessThanOrEqualTo(panel.Right));
                Assert.That(rects[i].Y, Is.GreaterThanOrEqualTo(panel.Y));
                Assert.That(rects[i].Bottom, Is.LessThanOrEqualTo(panel.Bottom));
                for (int j = i + 1; j < rects.Length; j++)
                    Assert.That(rects[i].X < rects[j].Right && rects[j].X < rects[i].Right && rects[i].Y < rects[j].Bottom && rects[j].Y < rects[i].Bottom, Is.False, "카드 " + i + "과 " + j + "이 겹친다");
            }
        }

        [Test]
        public void WithoutABackend_TheWardrobeIsEmptyButOpens()
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            t.Stack.Open(ScreenCatalog.Wardrobe);
            Assert.That(t.Screen<WardrobeScreen>(ScreenCatalog.Wardrobe).VisibleCardCount, Is.Zero);
        }

        [Test]
        public void CloseButton_AndEscape_CloseIt()
        {
            using (TestSession t = Open())
            {
                t.Click(WardrobeScreen.CloseId);
                Assert.That(t.Stack.IsOpen(ScreenCatalog.Wardrobe), Is.False);
                Assert.That(t.Stack.MovementBlocked, Is.False);
            }
            using (TestSession t = Open())
            {
                Assert.That(t.Stack.Key(ScreenKey.Escape, false), Is.True);
                Assert.That(t.Stack.IsOpen(ScreenCatalog.Wardrobe), Is.False);
            }
        }

        [Test]
        public void ArrowKeysMoveBetweenCards_AndEnterWearsTheFocusedOne()
        {
            using TestSession t = Open();
            var backend = (TestWardrobe)t.Services.Wardrobe;
            for (int i = 0; i < 12 && !(t.Stack.Tree.Focused?.Id ?? string.Empty).StartsWith("wardrobe.card."); i++)
                Assert.That(t.Stack.Key(ScreenKey.Down, false), Is.True, "이동을 막는 화면은 방향 키만으로 버튼을 옮긴다");
            Assert.That(t.Stack.Tree.Focused?.Id, Does.StartWith("wardrobe.card."));
            Assert.That(t.Stack.Key(ScreenKey.Enter, false), Is.True);
            Assert.That(backend.Chosen, Is.EqualTo(1), "포커스한 카드를 Enter로 입었다");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Wardrobe), Is.True);
        }

        [TestCase("ko")]
        [TestCase("en")]
        public void Text_ExistsInBothLanguages(string language)
        {
            using TestSession t = Open(language);
            var screen = t.Screen<WardrobeScreen>(ScreenCatalog.Wardrobe);
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, screen.TextKeys);
        }

        [Test]
        public void ChangingTheLanguage_ChangesTheTitle()
        {
            using TestSession t = Open();
            string before = ((UiLabel)t.Node(WardrobeScreen.TitleId)).Text;
            t.Text.Language = "en";
            t.Stack.NotifyChanged(ChangeTopics.Language);
            Assert.That(((UiLabel)t.Node(WardrobeScreen.TitleId)).Text, Is.Not.EqualTo(before));
        }
    }
}
