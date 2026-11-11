using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Screens.Objectives;
using Rootborn.Screens.Settings;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// 잘림과 읽기 어려운 글자(스크린샷에서 찾은 결함): 수첩 줄은 칸 끝에서 말줄임으로 줄이고, 닫기 단추는 테마의 닫기 아이콘을 그리고,
    /// 설정 언어 줄의 이름은 본문 크기에 테마 글자 색, 비활성 단추 글자는 비활성 색 이름(테마가 정한다)으로 그린다. 자동 테스트다.
    /// </summary>
    public sealed class ReadabilityTests
    {
        private static DrawList EmitNode(UiNode node, ITextMeasurer measurer)
        {
            var list = new DrawList();
            node.Emit(list, measurer);
            return list;
        }

        [Test]
        public void JournalRows_LongerThanTheirCell_EndWithAnEllipsis_AndFit()
        {
            using var t = new TestSession(extra: r => { });
            t.Session.TalkTo("npc.first-guide");
            t.Stack.Key(ScreenKey.Tab, false);
            var journal = t.Screen<ObjectiveJournalScreen>(ScreenCatalog.ObjectiveJournal);
            JournalList list = journal.List;
            string longTitle = string.Concat(Enumerable.Repeat("아주 긴 제목 ", 20));
            list.RowCount = 1;
            list.RowText = _ => (longTitle, "진행 " + longTitle);
            var measurer = new WideMeasurer();
            List<DrawCommand> texts = EmitNode(list, measurer).Commands.Where(c => c.Kind == DrawKind.Text).ToList();
            Assert.That(texts, Has.Count.EqualTo(2));
            foreach (DrawCommand text in texts)
            {
                Assert.That(text.Text, Does.EndWith(TextFit.Ellipsis), "글자 중간에서 잘리지 않고 말줄임");
                Assert.That(measurer.Width(text.Text, text.FontSize), Is.LessThanOrEqualTo(list.TextW + 0.01f), "칸 안에 들어간다");
            }
        }

        /// <summary>닫기 단추가 있는 화면을 열고, 닫기 단추가 글자 대신 테마의 닫기 아이콘을 단추에 맞는 크기로 그리는지 본다.</summary>
        [TestCase(ScreenCatalog.Location, "location.close")]
        [TestCase(ScreenCatalog.Minigame, "minigame.close")]
        [TestCase(ScreenCatalog.Encyclopedia, null)]
        [TestCase(ScreenCatalog.Wardrobe, null)]
        [TestCase(ScreenCatalog.WorldStateLog, null)]
        public void CloseButtons_DrawTheThemesCloseIcon_NotATinyGlyph(string screenId, string closeId)
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            if (screenId == ScreenCatalog.Location) t.Stack.Interaction(new InteractionEvent("Town", "location-activity", "location.library"));
            else if (screenId == ScreenCatalog.Minigame) t.Stack.Interaction(new InteractionEvent("Town", "part-time-work", "work.shift.corner-store"));
            else t.Stack.Open(screenId);
            Assert.That(t.Stack.IsOpen(screenId), Is.True);
            UiButton close = closeId != null
                ? (UiButton)t.Node(closeId)
                : Walk(t.Stack.Find(screenId).Root).OfType<UiButton>().Single(b => b.Id != null && b.Id.EndsWith(".close"));
            List<DrawCommand> commands = EmitNode(close, new WideMeasurer()).Commands;
            Assert.That(commands.Where(c => c.Kind == DrawKind.Text), Is.Empty, "글자 × 대신 그림");
            DrawCommand icon = commands.Single(c => c.Kind == DrawKind.Image);
            Assert.That(icon.Skin, Is.EqualTo("icon:close"));
            Assert.That(icon.Rect.W, Is.GreaterThanOrEqualTo(System.MathF.Min(close.Rect.W, close.Rect.H) * 0.5f), "단추에 맞는 크기");
        }

        [Test]
        public void SettingsLanguageLabel_IsBodySized_InTheThemesInk()
        {
            using var t = new TestSession();
            t.Stack.Open(ScreenCatalog.Settings);
            var label = (UiLabel)t.Node(SettingsScreen.LanguageLabelId);
            Assert.That(label.FontSize, Is.GreaterThanOrEqualTo(22f), "ui-standards 본문 18~22 이상(작아서 눈에 띄지 않았다)");
            Assert.That(label.Color, Is.EqualTo(UiTheme.Ink), "테마가 바꾸는 이름 붙은 글자 색(화면 코드의 갈색 대신)");
        }

        [Test]
        public void DisabledButtonText_UsesTheDisabledInkName_SoTheThemeDecidesItsColor()
        {
            var button = new UiButton { Text = "산 물건", Enabled = false };
            button.Place(0, 0, 0, 0, 200, 50);
            button.Layout(new UiRect(0, 0, 400, 300));
            DrawCommand text = EmitNode(button, new WideMeasurer()).Commands.Single(c => c.Kind == DrawKind.Text);
            Assert.That(text.Color, Is.EqualTo(UiTheme.InkDisabled));
            Assert.That(text.Color, Is.Not.EqualTo(UiTheme.InkMuted), "흐린 글자와 따로 둔다");
        }

        /// <summary>
        /// 이름 키가 비었거나 키가 아닌 영어 글인 퀘스트(Quest_DiscoveryClue_LibraryFollowUp, "Play clue follow-up")가 수첩에서 id·원문 대신
        /// id로 만든 규칙 키(DataTextKeys)의 문구로 보인다. 화면은 id로 분기하지 않는다. 두 언어 모두.
        /// </summary>
        [TestCase("ko")]
        [TestCase("en")]
        public void JournalQuestRows_ShowNames_NotIdsOrRawDataText(string language)
        {
            using var t = new TestSession(language);
            t.Session.TalkTo("npc.first-guide");
            t.Stack.Key(ScreenKey.Tab, false);
            var journal = t.Screen<ObjectiveJournalScreen>(ScreenCatalog.ObjectiveJournal);
            t.Click(ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Quests));
            var titles = Enumerable.Range(0, journal.List.RowCount).Select(i => journal.List.RowText(i).Title).ToList();
            var raw = new List<string>();
            foreach (var quest in t.Session.Definitions.All<Rootborn.Core.Quests.QuestDefinition>())
            {
                raw.Add(quest.Id);
                bool rawText = !string.IsNullOrEmpty(quest.DisplayNameKey) && !t.Text.Has(quest.DisplayNameKey, language);
                string ruleKey = DataTextKeys.Name(quest.Id);
                // 규칙 키의 문구가 원문과 같을 수 있다(영어 "Play clue follow-up"). 그때는 문구를 본 것이다.
                if (rawText && !(t.Text.Has(ruleKey, language) && t.Text.Get(ruleKey) == quest.DisplayNameKey)) raw.Add(quest.DisplayNameKey);
            }
            Assert.That(titles, Is.Not.Empty);
            Assert.That(titles.Intersect(raw), Is.Empty, "퀘스트 줄에 id나 키가 아닌 원문이 보이지 않는다");
            Assert.That(titles, Does.Contain(t.Text.Get(DataTextKeys.Name("Quest_DiscoveryClue_LibraryFollowUp"))));
            Assert.That(DataTextKeys.Name("Quest_DiscoveryClue_LibraryFollowUp"), Is.EqualTo("data.quest-discoveryclue-libraryfollowup.name"));
        }

        private static IEnumerable<UiNode> Walk(UiNode root)
        {
            yield return root;
            foreach (UiNode child in root.Children)
                foreach (UiNode node in Walk(child)) yield return node;
        }
    }
}
