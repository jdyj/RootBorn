using System.Linq;
using NUnit.Framework;
using Rootborn.Screens.Objectives;
using Rootborn.Screens.Objectives.Sources;
using Rootborn.Core.Quests;
using Rootborn.Screens.Quests;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Quests
{
    /// <summary>
    /// 퀘스트 체인 기록(Unity QuestChainLogPanel): Unity 판에 여는 길이 없어 Open(id)로 연다(플레이어 경로가 아니다 — 보고서).
    /// 카드를 포인터로 눌러 고르고, 받기·추적 단추가 GameSession에 닿는지, 문구 키.
    /// </summary>
    public sealed class QuestChainLogScreenTests
    {
        [Test]
        public void Opens_ListsChains_CardSelectsDetail_LayoutLikeUnity()
        {
            using var t = new TestSession(definitions: VisibleTestChains.Definitions); // 체인이 둘 이상 보여야 카드를 고른다
            t.Stack.Open(ScreenCatalog.QuestChainLog);
            var screen = t.Screen<QuestChainLogScreen>(ScreenCatalog.QuestChainLog);
            Assert.That(t.Node(QuestChainLogScreen.PanelId).Rect, Is.EqualTo(new UiRect(580, 330, 760, 420)), "Unity 760x420 가운데");
            Assert.That(t.Stack.MovementBlocked, Is.False);
            Assert.That(screen.Model.Rows.Length, Is.GreaterThan(1));
            Assert.That(((UiLabel)t.Node(QuestChainLogScreen.CareerId)).Text, Is.EqualTo("진로: 전체"));
            Assert.That(screen.Model.Selected, Is.EqualTo(0));
            t.Click(QuestChainLogScreen.CardId(1));
            Assert.That(screen.Model.Selected, Is.EqualTo(1));
            Assert.That(((QuestChainCard)t.Node(QuestChainLogScreen.CardId(1))).Selected, Is.True);
            Assert.That(((UiTextBlock)t.Node(QuestChainLogScreen.DetailId)).Text, Does.StartWith(((QuestChainCard)t.Node(QuestChainLogScreen.CardId(1))).Title));
            t.Emit();
            t.Text.Language = "en";
            t.Emit();
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, screen.TextKeys);
        }

        [Test]
        public void DeveloperOnlyChains_AreNotListed_EvenWhenNoFilterIsSet()
        {
            using var t = new TestSession();
            Assume.That(t.Session.Definitions.All<QuestChainDefinition>().Any(c => c.DeveloperOnly), "실제 data/에 개발용 체인이 있다");
            t.Stack.Open(ScreenCatalog.QuestChainLog);
            var screen = t.Screen<QuestChainLogScreen>(ScreenCatalog.QuestChainLog);
            var shown = t.Session.Definitions.All<QuestChainDefinition>().Where(c => !c.DeveloperOnly).Select(c => c.Id);
            Assert.That(screen.Model.Rows.Select(r => r.ChainId), Is.EquivalentTo(shown), "목표 수첩과 같은 기준(DeveloperOnly)으로 거른다");
            foreach (var row in screen.Model.Rows)
                Assert.That(t.Text.Has(row.NameKey, "ko") && t.Text.Has(row.NameKey, "en"), Is.True, "보이는 체인은 이름 문구가 있다(키가 그대로 보이지 않는다): " + row.ChainId);
        }

        [Test]
        public void AcceptButton_StartsTheSelectedAvailableChain()
        {
            using var t = new TestSession(definitions: VisibleTestChains.Definitions); // 조건 없이 받을 수 있는 체인은 개발용뿐이다
            t.Stack.Open(ScreenCatalog.QuestChainLog);
            var screen = t.Screen<QuestChainLogScreen>(ScreenCatalog.QuestChainLog);
            int index = -1;
            QuestChainDefinition chain = null;
            for (int i = 0; i < screen.Model.Rows.Length && i < QuestChainLogModel.MaxCards; i++)
            {
                t.Click(QuestChainLogScreen.CardId(i));
                QuestChainDefinition c = t.Session.Chain(screen.Model.SelectedChainId);
                if (screen.Model.CanAccept && c.StartConditions.Count == 0 && string.IsNullOrEmpty(c.StartNpcId)) { index = i; chain = c; break; }
            }
            Assume.That(index, Is.GreaterThanOrEqualTo(0), "조건 없이 받을 수 있는 체인");
            t.Click(QuestChainLogScreen.AcceptId);
            Assert.That(t.Session.QuestChains.GetState(chain), Is.Not.EqualTo(QuestChainState.Available), "받기 단추가 GameSession.AcceptQuestChain에 닿는다");
            Assert.That(screen.Model.Rows.First(r => r.ChainId == chain.Id).State, Is.Not.EqualTo(QuestChainState.Available), "화면도 바뀐다");
        }

        [Test]
        public void TrackButton_IsEnabledOnlyForActiveChains_AndTracks()
        {
            using var t = new TestSession();
            t.Session.TalkTo("npc.first-guide");
            t.Stack.Open(ScreenCatalog.QuestChainLog);
            var screen = t.Screen<QuestChainLogScreen>(ScreenCatalog.QuestChainLog);
            int active = System.Array.FindIndex(screen.Model.Rows, r => r.State == QuestChainState.Tracked || r.State == QuestChainState.Active);
            Assume.That(active, Is.InRange(0, QuestChainLogModel.MaxCards - 1));
            t.Click(QuestChainLogScreen.CardId(active));
            Assert.That(t.Node(QuestChainLogScreen.TrackId).Enabled, Is.True);
            Assert.That(t.Node(QuestChainLogScreen.AcceptId).Enabled, Is.False, "이미 받은 체인");
            string chainId = screen.Model.SelectedChainId;
            t.Click(QuestChainLogScreen.TrackId);
            Assert.That(t.Session.ObjectiveTracking.StableKey, Is.EqualTo(QuestChainSource.StableKeyFor(chainId)), "목표 수첩과 같은 추적(GameSession.TrackObjective)");
            Assert.That(t.Session.ObjectiveTracking.CategoryId, Is.EqualTo(JournalCategories.Quests));
            using TestSession again = t.Reopen();
            Assert.That(again.Session.ObjectiveTracking.StableKey, Is.EqualTo(QuestChainSource.StableKeyFor(chainId)), "다시 불러와도 남는다");
        }
    }
}
