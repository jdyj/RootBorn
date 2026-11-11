using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game;
using Rootborn.Screens.Objectives;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// 목표 수첩: 실제 data/와 GameSession으로 분류·목록·상세가 나오는지, 분류 단추·줄·추적 단추를 포인터로 눌러 값이 바뀌는지,
    /// 긴 목록에서 보이는 줄만 그리는지, 쓴 문구 키가 두 언어에 다 있는지.
    /// </summary>
    public sealed class ObjectiveJournalScreenTests
    {
        /// <summary>시험용 모으는 쪽: 발견 분류에 줄 n개.</summary>
        private sealed class ManyItemsSource : IObjectiveJournalSource
        {
            private readonly int _count;
            public ManyItemsSource(int count) { _count = count; }
            public int Builds { get; private set; }
            public string SourceId => "test-many";
            public string CategoryId => JournalCategories.Hints;

            public IReadOnlyList<JournalItem> BuildItems(GameSession session)
            {
                Builds++;
                var items = new JournalItem[_count];
                for (int i = 0; i < _count; i++)
                    items[i] = new JournalItem("test-many." + i.ToString("000"), CategoryId, JournalText.Raw("row " + i.ToString("000")), JournalText.Empty,
                        JournalText.Empty, JournalText.Key("ui.objectives.day_progress", i, 0, 0), JournalText.Empty, false, 1000 + i);
                return items;
            }
        }

        /// <summary>목록의 줄을 포인터로 누른다(보이게 넘긴 뒤 그 줄 가운데).</summary>
        internal static void ClickRow(TestSession t, ObjectiveJournalScreen journal, int row)
        {
            journal.List.EnsureVisible(row);
            UiRect r = journal.List.RowRect(row);
            float x = r.CenterX, y = r.CenterY;
            Assert.That(t.Stack.Tree.HitTest(x, y), Is.SameAs(journal.List), "줄 위에 다른 요소가 있다");
            t.Stack.Pointer(x, y, false, 0);
            t.Stack.Pointer(x, y, true, 0);
            t.Stack.Pointer(x, y, false, 0);
        }

        private static ObjectiveJournalScreen Open(TestSession t)
        {
            Assert.That(t.Stack.Key(ScreenKey.Tab, false), Is.True, "Tab으로 연다");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.ObjectiveJournal), Is.True);
            return t.Screen<ObjectiveJournalScreen>(ScreenCatalog.ObjectiveJournal);
        }

        [Test]
        public void Tab_OpensTheJournal_SizeAndCategoriesLikeUnity_DoesNotBlockMovement()
        {
            using var t = new TestSession();
            ObjectiveJournalScreen journal = Open(t);
            Assert.That(t.Node(ObjectiveJournalScreen.PanelId).Rect, Is.EqualTo(new UiRect(240, 120, 1440, 840)), "Unity 1440x840 가운데");
            Assert.That(t.Stack.MovementBlocked, Is.False, "수첩이 떠 있어도 걷는다");
            Assert.That(t.Stack.Key(ScreenKey.Up, false), Is.False, "방향 키는 캐릭터로 간다");
            string[] labels = JournalCategories.All.Select(c => ((JournalRailButton)t.Node(ObjectiveJournalScreen.CategoryButtonId(c.Id))).Text).ToArray();
            Assert.That(labels, Is.EqualTo(new[] { "목표", "심부름", "이야기", "발견" }), "Unity 분류 순서와 이름");
            Assert.That(journal.SelectedCategory, Is.EqualTo(JournalCategories.Goals));
            Assert.That(journal.Items.Select(i => i.StableKey), Does.Contain("daily-plan:choose").And.Some.StartsWith("student-day."), "새 게임: 하루 안내와 일과 고르기");
            Assert.That(journal.Detail.VisibleText, Does.Contain(t.Text.Get("ui.objectives.student_day_guide")));
            Assert.That(t.Stack.Key(ScreenKey.Tab, false), Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.ObjectiveJournal), Is.False, "Tab으로 닫는다");
        }

        [Test]
        public void CategoryButtons_AndRows_ChangeTheSelectionAndDetail()
        {
            using var t = new TestSession();
            t.Commands.Run(s => s.TalkTo("npc.first-guide"), ChangeTopics.Dialogue);
            ObjectiveJournalScreen journal = Open(t);

            t.Click(ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Quests));
            Assert.That(journal.SelectedCategory, Is.EqualTo(JournalCategories.Quests));
            Assert.That(((JournalRailButton)t.Node(ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Quests))).Selected, Is.True, "고른 분류 표시");
            Assert.That(journal.Items.Count, Is.GreaterThan(1));
            Assert.That(journal.Items.Any(i => i.StableKey.StartsWith("quest.")) && journal.Items.Any(i => i.StableKey.StartsWith("questchain.")), Is.True, "퀘스트와 체인");
            int[] order = journal.Items.Select(i => i.SortPriority).ToArray();
            Assert.That(order, Is.Ordered, "정렬 순서대로");

            string firstDetail = journal.Detail.VisibleText;
            ClickRow(t, journal, 1);
            Assert.That(journal.SelectedIndex, Is.EqualTo(1));
            Assert.That(journal.SelectedStableKey, Is.EqualTo(journal.Items[1].StableKey));
            Assert.That(journal.Detail.VisibleText, Is.Not.EqualTo(firstDetail), "상세가 그 줄로 바뀐다");

            t.Click(ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Campaign));
            Assert.That(journal.SelectedIndex, Is.EqualTo(0));
            t.Click(ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Hints));
            Assert.That(journal.Items.Count, Is.GreaterThan(0), "발견: 열린 일과 길·장래희망 등");
        }

        [Test]
        public void TrackButton_TracksTheSelectedRow_AndReopeningSelectsIt()
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            t.Commands.Run(s => s.TalkTo("npc.first-guide"), ChangeTopics.Dialogue);
            ObjectiveJournalScreen journal = Open(t);
            t.Click(ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Quests));
            int row = journal.Items.ToList().FindIndex(i => i.StableKey.StartsWith("questchain.") && i.IsTracked);
            ClickRow(t, journal, row);
            string key = journal.SelectedStableKey;
            t.Click(ObjectiveJournalScreen.TrackId);
            Assert.That(ObjectiveJournalTracking.For(t.Services).StableKey, Is.EqualTo(key));
            Assert.That(ObjectiveJournalTracking.For(t.Services).CategoryId, Is.EqualTo(JournalCategories.Quests));

            t.Click(ObjectiveJournalScreen.CloseId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.ObjectiveJournal), Is.False, "닫기 단추");
            t.Stack.Key(ScreenKey.Tab, false);
            t.Click(ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Goals));
            t.Stack.Key(ScreenKey.Tab, false);
            t.Stack.Key(ScreenKey.Tab, false);
            Assert.That(journal.SelectedCategory, Is.EqualTo(JournalCategories.Quests), "열면 추적 중인 항목의 분류로");
            Assert.That(journal.SelectedStableKey, Is.EqualTo(key));
        }

        [Test]
        public void GameProgress_RebuildsItems_AndKeepsTheSelectedRowByKey()
        {
            using var t = new TestSession();
            ObjectiveJournalScreen journal = Open(t);
            Assert.That(journal.Items.Any(i => i.StableKey == "daily-plan:choose"), Is.True);
            var routes = t.Session.AvailableDailyPlanRoutes();
            Assume.That(routes.Length, Is.GreaterThan(0));
            t.Commands.Run(s => s.SelectDailyPlanRoute(routes[0].Id), ChangeTopics.Quests);
            Assert.That(journal.Items.Any(i => i.StableKey == "daily-plan:" + routes[0].Id), Is.True, "일과를 고르면 그 길 줄이 생긴다");
        }

        [Test]
        public void LongList_DrawsOnlyVisibleRows_AndScrollsToTheClickedRow()
        {
            ManyItemsSource many = null;
            using var t = new TestSession(extra: r => r.AddListener(s => ObjectiveJournalSources.For(s).Add(many = new ManyItemsSource(200))));
            ObjectiveJournalScreen journal = Open(t);
            t.Click(ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Hints));
            Assert.That(journal.Items.Count, Is.GreaterThanOrEqualTo(200));
            DrawList list = t.Emit();
            Assert.That(journal.List.LastEmittedRows, Is.InRange(1, 8), "보이는 줄만 그린다");
            int texts = t.Texts(list).Count(x => x.StartsWith("row "));
            Assert.That(texts, Is.LessThanOrEqualTo(8));

            int builds = many.Builds;
            for (int i = 0; i < 30; i++) journal.List.OnWheel(-1);
            t.Emit();
            Assert.That(journal.List.ScrollOffset, Is.GreaterThan(0));
            Assert.That(journal.List.LastEmittedRows, Is.InRange(1, 8));
            Assert.That(many.Builds, Is.EqualTo(builds), "넘기기만으로는 항목을 다시 만들지 않는다");
            int target = journal.List.SelectedIndex + 40;
            ClickRow(t, journal, target);
            Assert.That(journal.SelectedIndex, Is.EqualTo(target));
            Assert.That(journal.Detail.VisibleText, Does.Contain(journal.Items[target].Title.ToString()));
        }

        [Test]
        public void TextKeys_ExistInBothLanguages()
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            t.Commands.Run(s => s.TalkTo("npc.first-guide"), ChangeTopics.Dialogue);
            ObjectiveJournalScreen journal = Open(t);
            foreach (string language in t.Text.Languages.ToArray())
            {
                t.Text.Language = language;
                foreach (JournalCategory category in JournalCategories.All)
                {
                    t.Click(ObjectiveJournalScreen.CategoryButtonId(category.Id));
                    for (int i = 0; i < journal.Items.Count; i++) ClickRow(t, journal, i);
                    t.Emit();
                }
            }
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, journal.TextKeys);
        }
    }
}
