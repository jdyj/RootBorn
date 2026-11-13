using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Screens;
using Rootborn.Screens.Objectives;
using Rootborn.Simulation.Headless;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트: 메인 체인 우선 정책은 추적할 것이 없으면(수첩을 두 번 훑어도 추적이 안 생기면) 그날은 수첩을 다시 열지 않고
    /// 관찰에서 고를 수 있는 다른 일(장소·알바·연습·탐색·NPC)로 넘어간다. 2026-11-13 속도 보고: 체인을 끝낸 뒤 하루에 Tab 46번.
    /// </summary>
    public sealed class MainChainNothingToTrackTests
    {
        private static PlayView World(int day)
        {
            var v = new PlayView { InWorld = true, Day = day, MapId = "m" };
            v.Actions.Add(new PlayAction { Kind = PlayActionKind.Key, Key = "key:" + ScreenKey.Tab, Category = "key", ScreenKey = ScreenKey.Tab });
            v.Actions.Add(new PlayAction { Kind = PlayActionKind.WalkInteract, Key = "walk:part-time-work/w", Category = "interaction", InteractionKind = "part-time-work", Subject = "w" });
            v.Actions.Add(new PlayAction { Kind = PlayActionKind.Door, Key = "door:x", Category = "door", InteractionKind = "door", Subject = "x" });
            return v;
        }

        /// <summary>수첩이 열렸는데 퀘스트 줄이 하나도 없다(추적할 것이 없다).</summary>
        private static PlayView EmptyJournal(int day)
        {
            PlayView v = World(day);
            v.OpenScreens.Add(ScreenCatalog.ObjectiveJournal);
            v.TopScreen = ScreenCatalog.ObjectiveJournal;
            v.Actions.Add(new PlayAction { Kind = PlayActionKind.Click, Key = "button:" + ScreenCatalog.ObjectiveJournal + "/" + ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Quests), Category = "button", Screen = ScreenCatalog.ObjectiveJournal, Element = ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Quests) });
            v.Actions.Add(new PlayAction { Kind = PlayActionKind.Click, Key = "button:" + ScreenCatalog.ObjectiveJournal + "/objective-journal.close", Category = "button", Screen = ScreenCatalog.ObjectiveJournal, Element = "objective-journal.close" });
            return v;
        }

        /// <summary>정책을 한 걸음: 고른 행동에 따라 다음 관찰(Tab이면 빈 수첩, 닫기면 월드)을 돌려준다.</summary>
        private static (PlayAction, bool Journal) Step(MainChainPolicy p, PlayMemory m, bool journal, int day, Random r)
        {
            PlayView v = journal ? EmptyJournal(day) : World(day);
            PlayAction a = p.Choose(v, m, r);
            m.Note(v, a);
            if (a.Key == "key:" + ScreenKey.Tab) return (a, true);
            if (a.Element == "objective-journal.close") return (a, false);
            return (a, journal);
        }

        [Test]
        public void AfterTwoFruitlessJournalPasses_ItDoesNotOpenTheJournalAgainThatDay_AndDoesOtherThings()
        {
            var p = new MainChainPolicy(null);
            var m = new PlayMemory();
            var r = new Random(1);
            bool journal = false;
            int tabs = 0, work = 0;
            for (int i = 0; i < 200; i++)
            {
                (PlayAction a, bool j) = Step(p, m, journal, 3, r);
                journal = j;
                if (a.Key == "key:" + ScreenKey.Tab) tabs++;
                if (a.Category == "interaction") work++;
            }
            Assert.That(tabs, Is.LessThanOrEqualTo(2), "하루에 수첩 훑기 두 번까지");
            Assert.That(work, Is.GreaterThan(0), "다른 일(알바 지점)로 넘어간다");
            bool reopened = false;
            journal = false;
            for (int i = 0; i < 5 && !reopened; i++) reopened = Step(p, m, journal, 4, r).Item1.Key == "key:" + ScreenKey.Tab;
            Assert.That(reopened, Is.True, "다음 날에는 다시 수첩을 본다");
        }

        /// <summary>실제 데이터로: 메인 체인을 끝낸 뒤의 날(4일째부터)에 Tab을 하루 열 번 넘게 누르지 않는다.</summary>
        [Test]
        public void AfterTheOpeningChain_JournalIsNotReopenedOverAndOver()
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            using var body = new HeadlessBody(atlas);
            var settings = new PlaySettings { Days = 5, StepsPerDay = 5000, WindDownSteps = 120, MaxSteps = 6000 };
            PlayResult r = new PlayRunner(body, PlayPolicyCatalog.Default().Create(PlayPolicyCatalog.MainChain, atlas), 1, settings, atlas, "main-chain-after-chain").Run();
            KnownDefects.AssertNoNewFailure(r);
            foreach (var day in r.Steps.Where(s => s.Day >= 4).GroupBy(s => s.Day))
                Assert.That(day.Count(s => s.Key == "key:" + ScreenKey.Tab), Is.LessThanOrEqualTo(10), day.Key + "일째");
        }
    }
}
