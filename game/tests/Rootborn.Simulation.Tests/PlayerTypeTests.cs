using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Simulation.Pace;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트: 플레이어 유형 정책 넷(설계 4절)의 고르기 — 관찰을 손으로 만든다. 정책은 엔티티 id를 모르고 행동의 분류·종류·이름,
    /// HUD에 보이는 돈만 본다.
    /// </summary>
    public sealed class PlayerTypeTests
    {
        private static PlayView World(int money, params (string Key, string Kind)[] spots)
        {
            var v = new PlayView { InWorld = true, Day = 1, MapId = "m", Money = money };
            foreach (var (key, kind) in spots)
                v.Actions.Add(new PlayAction { Kind = PlayActionKind.WalkInteract, Key = key, Category = "interaction", InteractionKind = kind, Subject = key });
            v.Actions.Add(new PlayAction { Kind = PlayActionKind.Door, Key = "door:x", Category = "door", InteractionKind = "door", Subject = "x" });
            v.Actions.Add(new PlayAction { Kind = PlayActionKind.Key, Key = "key:Tab", Category = "key" });
            return v;
        }

        private static readonly (string, string) A = ("walk:part-time-work/a", "part-time-work"), B = ("walk:part-time-work/b", "part-time-work");

        [Test]
        public void Catalog_HasTheFourTypes_StoryIsTheMainChainPolicy()
        {
            Assert.That(PlayerTypeCatalog.Names, Is.EqualTo(new[] { PlayerTypeCatalog.Story, PlayerTypeCatalog.Money, PlayerTypeCatalog.Explorer, PlayerTypeCatalog.OneThing }));
            Assert.That(PlayerTypeCatalog.Create(PlayerTypeCatalog.Story, null), Is.InstanceOf<MainChainPolicy>());
            foreach (string n in PlayerTypeCatalog.Names) Assert.That(PlayerTypeCatalog.Create(n, null), Is.Not.Null, n);
        }

        /// <summary>돈·이것저것·한 가지 유형은 잠자리를 고르지 않는다(자는 때는 달리는 쪽의 하루 마감 순서가 정한다).</summary>
        [Test]
        public void TypesOtherThanStory_LeaveSleepToBedtime()
        {
            var bed = ("walk:" + Rootborn.Screens.Day.DayResultScreen.InteractionKind + "/bed", Rootborn.Screens.Day.DayResultScreen.InteractionKind);
            foreach (string type in new[] { PlayerTypeCatalog.Money, PlayerTypeCatalog.Explorer, PlayerTypeCatalog.OneThing })
                for (int seed = 0; seed < 20; seed++)
                    Assert.That(PlayerTypeCatalog.Create(type, null).Choose(World(0, bed), new PlayMemory(), new Random(seed))?.Key, Is.Not.EqualTo(bed.Item1), type + " 씨앗 " + seed);
        }

        /// <summary>돈을 모으는 사람: 해 본 뒤 HUD의 돈이 가장 많이 늘었던 일을 되풀이한다.</summary>
        [Test]
        public void Money_RepeatsTheWorkThatRaisedTheVisibleMoneyMost()
        {
            var p = new MoneyPolicy();
            var m = new PlayMemory();
            var r = new Random(1);
            PlayView v = World(100, A, B);
            m.Note(v, p.Credit(v, v.Find(A.Item1)));     // a를 했다 → 돈 +5
            v = World(105, A, B);
            m.Note(v, p.Credit(v, v.Find(B.Item1)));     // b를 했다 → 돈 +40
            v = World(145, A, B);
            p.Choose(v, m, r);                           // 돈 변화를 본다
            for (int seed = 0; seed < 10; seed++)
                Assert.That(p.Choose(World(145, A, B), m, new Random(seed)).Key, Is.EqualTo(B.Item1), "씨앗 " + seed);
        }

        /// <summary>해 봤는데 돈이 안 늘은 일은 문 너머(다른 맵의 일)보다 뒤다(2026-11-13 7일 판: 집의 계획판만 오가다 07:07에 잤다).</summary>
        [Test]
        public void Money_LeavesWorkThatPaidNothing_ForTheDoor()
        {
            var p = new MoneyPolicy();
            var m = new PlayMemory();
            PlayView v = World(100, A);
            m.Note(v, p.Credit(v, v.Find(A.Item1)));
            Assert.That(p.Choose(World(100, A), m, new Random(1)).Key, Is.EqualTo("door:x"));
        }

        /// <summary>이것저것 해 보는 사람: 해 본 적 없는 일을 먼저(해 본 횟수에 끝이 없다 — 20번에서 멈추지 않는다).</summary>
        [Test]
        public void Explorer_PrefersWhatItHasNotTried_WithoutACap()
        {
            var p = new ExplorerPolicy();
            var m = new PlayMemory();
            PlayView v = World(0, A, B);
            for (int i = 0; i < 50; i++) m.Note(v, v.Find(A.Item1));
            for (int i = 0; i < 30; i++) m.Note(v, v.Find(B.Item1));
            Assert.That(p.Choose(World(0, A, B), m, new Random(1)).Key, Is.EqualTo("door:x"), "둘 다 많이 했으면 문 너머");
            Assert.That(p.Choose(World(0, A, B, ("walk:exploration/c", "exploration")), m, new Random(1)).Key, Is.EqualTo("walk:exploration/c"));
        }

        /// <summary>한 가지만 파는 사람: 처음 고른 일만 되풀이하고, 그것이 안 보이면 서 있는다.</summary>
        [Test]
        public void OneThing_KeepsDoingTheFirstThing_AndStandsWhenItIsNotThere()
        {
            var p = new OneThingPolicy(null);
            var m = new PlayMemory();
            string first = p.Choose(World(0, A, B), m, new Random(5)).Key;
            for (int seed = 0; seed < 10; seed++)
                Assert.That(p.Choose(World(0, A, B, ("walk:exploration/c", "exploration")), m, new Random(seed)).Key, Is.EqualTo(first));
            PlayAction none = p.Choose(World(0, first == A.Item1 ? B : A), m, new Random(1));
            Assert.That(none.Key, Does.StartWith(PlayView.IdlePrefix), "할 것이 없으면 서 있는다(할 것 없음)");
        }
    }
}
