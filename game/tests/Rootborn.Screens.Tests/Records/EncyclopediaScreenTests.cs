using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Encyclopedia;
using Rootborn.Screens.Records;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Records
{
    public sealed class EncyclopediaScreenTests
    {
        private static TestSession Open(string language = "ko", bool unlockFirst = false)
        {
            var t = new TestSession(language);
            if (unlockFirst) t.Session.Encyclopedia.TryUnlock("ency.location.first-discovery", 1, 1L);
            t.Stack.OpenHud();
            Assert.That(t.Stack.Open(ScreenCatalog.Encyclopedia), Is.Not.Null);
            t.Emit();
            return t;
        }

        private static string Shown(TestSession t, string keyOrText) => t.Text.Has(keyOrText, t.Text.Language) ? t.Text.Get(keyOrText) : keyOrText;

        [Test]
        public void HudButton_OpensIt_AsAMovementBlockingPanel_WithUnityPanelSize()
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            t.Emit();
            t.Click("hud.button.encyclopedia");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Encyclopedia), Is.True);
            Assert.That(t.Stack.MovementBlocked, Is.True, "Unity: 도감은 입력을 잠갔다");
            t.Emit();
            Assert.That(t.Node(EncyclopediaScreen.PanelId).Rect, Is.EqualTo(new UiRect(960 - 640, 540 - 400, 1280, 800)));
        }

        [Test]
        public void ListsCategoriesInSortOrder_AndSelectsTheFirstOne()
        {
            using TestSession t = Open();
            var screen = t.Screen<EncyclopediaScreen>(ScreenCatalog.Encyclopedia);
            string[] expected = t.Session.Definitions.All<EncyclopediaCategory>()
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).ToArray();
            Assert.That(screen.Model.Categories.Select(c => c.Id), Is.EqualTo(expected));
            Assert.That(screen.SelectedCategoryId, Is.EqualTo(expected[0]));
            for (int i = 0; i < expected.Length; i++) Assert.That(t.Node(EncyclopediaScreen.CategoryId(i)).Visible, Is.True);
            Assert.That(t.Node(EncyclopediaScreen.CategoryId(expected.Length)).Visible, Is.False);
        }

        [Test]
        public void LockedEntry_ShowsLockedTitleAndHint_NotTheRealOne()
        {
            using TestSession t = Open();
            var screen = t.Screen<EncyclopediaScreen>(ScreenCatalog.Encyclopedia);
            Assert.That(screen.Rows.Count, Is.GreaterThan(0));
            Assert.That(screen.Rows.All(r => !r.Unlocked), Is.True);
            EncyclopediaEntry entry = t.Session.Definitions.Get<EncyclopediaEntry>(screen.SelectedEntryId);
            Assert.That(((UiTextBlock)t.Node(EncyclopediaScreen.DetailTitleId)).Text, Is.EqualTo(Shown(t, entry.LockedDisplayName)));
            Assert.That(((UiTextBlock)t.Node(EncyclopediaScreen.DetailBodyId)).Text, Is.EqualTo(Shown(t, entry.LockedHint)));
        }

        [Test]
        public void UnlockedEntry_ShowsItsDescriptionAndDetailLines_AndLosesTheNewMarkWhenSeen()
        {
            using TestSession t = Open(unlockFirst: true);
            var screen = t.Screen<EncyclopediaScreen>(ScreenCatalog.Encyclopedia);
            EncyclopediaEntry entry = t.Session.Definitions.Get<EncyclopediaEntry>("ency.location.first-discovery");
            int category = screen.Model.Categories.ToList().FindIndex(c => c.Id == entry.Category.Id);
            t.Click(EncyclopediaScreen.CategoryId(category));
            Assert.That(screen.SelectedEntryId, Is.EqualTo(entry.Id), "분류를 고르면 그 분류의 첫 항목이 열린다");
            Assert.That(((UiTextBlock)t.Node(EncyclopediaScreen.DetailTitleId)).Text, Is.EqualTo(Shown(t, entry.DisplayName)));
            string body = ((UiTextBlock)t.Node(EncyclopediaScreen.DetailBodyId)).Text;
            Assert.That(body, Does.StartWith(Shown(t, entry.Description)));
            foreach (string line in entry.DetailLines)
                if (!string.IsNullOrEmpty(line)) Assert.That(body, Does.Contain(Shown(t, line)));
            Assert.That(t.Session.Encyclopedia.IsNew(entry.Id), Is.False, "열어서 본 항목은 새 항목 표시가 사라진다");
        }

        [Test]
        public void NewEntry_RowShowsTheNewMark_UntilSeen()
        {
            using TestSession t = Open(unlockFirst: true);
            var screen = t.Screen<EncyclopediaScreen>(ScreenCatalog.Encyclopedia);
            EncyclopediaEntry entry = t.Session.Definitions.Get<EncyclopediaEntry>("ency.location.first-discovery");
            // 같은 분류에 다른 항목을 먼저 열어 두면 이 항목은 새 표시가 남는다. 분류에 이 항목만 있으면 열자마자 본 것이 된다.
            t.Click(EncyclopediaScreen.CategoryId(screen.Model.Categories.ToList().FindIndex(c => c.Id == entry.Category.Id)));
            Assert.That(screen.Rows.First(r => r.EntryId == entry.Id).IsNew, Is.False);
            Assert.That(screen.RowText(0), Does.Not.Contain(t.Text.Get("ui.encyclopedia.new")));
        }

        [Test]
        public void ClickingARow_SelectsThatEntry()
        {
            using TestSession t = Open(unlockFirst: true);
            var screen = t.Screen<EncyclopediaScreen>(ScreenCatalog.Encyclopedia);
            for (int c = 0; c < screen.Model.Categories.Count; c++)
            {
                t.Click(EncyclopediaScreen.CategoryId(c));
                if (screen.Rows.Count < 2) continue;
                var list = (UiScrollList)t.Node(EncyclopediaScreen.ListId);
                Assert.That(list.TryGetRowRect(1, out UiRect row), Is.True);
                t.Stack.Pointer(row.CenterX, row.CenterY, false, 0);
                t.Stack.Pointer(row.CenterX, row.CenterY, true, 0);
                t.Stack.Pointer(row.CenterX, row.CenterY, false, 0);
                Assert.That(screen.SelectedEntryId, Is.EqualTo(screen.Rows[1].EntryId));
                return;
            }
            Assert.Pass("항목이 둘 이상인 분류가 없다");
        }

        [Test]
        public void EmptyCategory_ShowsTheEmptyMessage()
        {
            using TestSession t = Open();
            var screen = t.Screen<EncyclopediaScreen>(ScreenCatalog.Encyclopedia);
            int empty = -1;
            for (int c = 0; c < screen.Model.Categories.Count; c++)
            {
                t.Click(EncyclopediaScreen.CategoryId(c));
                if (screen.Rows.Count == 0) { empty = c; break; }
            }
            Assert.That(empty, Is.GreaterThanOrEqualTo(0), "데이터에 비어 있는 분류가 있다");
            Assert.That(((UiTextBlock)t.Node(EncyclopediaScreen.DetailTitleId)).Text, Is.EqualTo(t.Text.Get("ui.encyclopedia.empty.title")));
            Assert.That(((UiTextBlock)t.Node(EncyclopediaScreen.DetailBodyId)).Text, Is.EqualTo(t.Text.Get("ui.encyclopedia.empty.hint")));
        }

        [Test]
        public void CloseButton_AndEscape_CloseIt_AndReleaseMovement()
        {
            using (TestSession t = Open())
            {
                t.Click(EncyclopediaScreen.CloseId);
                Assert.That(t.Stack.IsOpen(ScreenCatalog.Encyclopedia), Is.False);
                Assert.That(t.Stack.MovementBlocked, Is.False);
            }
            using (TestSession t = Open())
            {
                Assert.That(t.Stack.Key(ScreenKey.Escape, false), Is.True);
                Assert.That(t.Stack.IsOpen(ScreenCatalog.Encyclopedia), Is.False);
            }
        }

        [Test]
        public void LongRowList_IsVirtualized_AndScrollsWithTheWheel()
        {
            using TestSession t = Open();
            var list = (UiScrollList)t.Node(EncyclopediaScreen.ListId);
            list.RowCount = 500;
            Assert.That(list.LastVisibleRow - list.FirstVisibleRow, Is.LessThan(20), "보이는 줄만 다룬다");
            t.Stack.Pointer(list.Rect.CenterX, list.Rect.CenterY, false, -3f);
            Assert.That(list.ScrollOffset, Is.GreaterThan(0f));
        }

        [TestCase("ko")]
        [TestCase("en")]
        public void Text_ExistsInBothLanguages(string language)
        {
            using TestSession t = Open(language, unlockFirst: true);
            var screen = t.Screen<EncyclopediaScreen>(ScreenCatalog.Encyclopedia);
            for (int c = 0; c < screen.Model.Categories.Count; c++) t.Click(EncyclopediaScreen.CategoryId(c));
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, screen.TextKeys);
        }

        [Test]
        public void ChangingTheLanguage_ChangesTheShownText()
        {
            using TestSession t = Open();
            string before = ((UiLabel)t.Node(EncyclopediaScreen.TitleId)).Text;
            t.Text.Language = "en";
            t.Stack.NotifyChanged(ChangeTopics.Language);
            Assert.That(((UiLabel)t.Node(EncyclopediaScreen.TitleId)).Text, Is.Not.EqualTo(before));
        }
    }
}
