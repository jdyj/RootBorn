using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Simulation.Pace;

namespace Rootborn.Simulation.Tests
{
    /// <summary>자동 테스트: 새로운 것의 종류는 등록 표로 만들고, 같은 것은 처음 한 번만 사건이 된다(관찰을 손으로 만든다).</summary>
    public sealed class PaceNoveltyTests
    {
        private static List<PaceNovelty> Run(IPaceNoveltyKind kind, PaceProbe probe)
        {
            var found = new List<PaceNovelty>();
            kind.Collect(probe, found);
            return found;
        }

        private static IPaceNoveltyKind Kind(string name) => new PaceNoveltyCatalog().Create().Single(k => k.Name == name);

        [Test]
        public void FirstOpenedScreen_IsNewOnlyTheFirstTime()
        {
            IPaceNoveltyKind kind = Kind(PaceNoveltyCatalog.FirstScreen);
            var probe = new PaceProbe();
            probe.OpenScreens.Add("x");
            Assert.That(Run(kind, probe).Select(n => n.What), Is.EqualTo(new[] { "x" }));
            Assert.That(Run(kind, probe), Is.Empty, "다시 열어도 새것이 아니다");
            probe.OpenScreens.Add("y");
            Assert.That(Run(kind, probe).Select(n => n.What), Is.EqualTo(new[] { "y" }));
        }

        [Test]
        public void FirstMap_AndFirstInteractionTarget_AreNewOnce()
        {
            IPaceNoveltyKind map = Kind(PaceNoveltyCatalog.FirstMap);
            IPaceNoveltyKind target = Kind(PaceNoveltyCatalog.FirstTarget);
            var probe = new PaceProbe { MapId = "m1" };
            probe.Interactions.Add(new InteractionEvent("m1", "talk", "a"));
            probe.Interactions.Add(new InteractionEvent("m1", "talk", "a"));
            Assert.That(Run(map, probe), Has.Count.EqualTo(1));
            Assert.That(Run(target, probe).Select(n => n.What), Is.EqualTo(new[] { "talk/a" }), "같은 걸음에 두 번이어도 한 번");
            probe.Interactions.Clear();
            probe.Interactions.Add(new InteractionEvent("m2", "talk", "a"));
            Assert.That(Run(target, probe), Is.Empty, "맵이 달라도 같은 대상");
            probe.MapId = string.Empty;
            Assert.That(Run(map, probe), Is.Empty, "맵이 없으면(슬롯 선택) 사건이 아니다");
        }

        [Test]
        public void FirstMinigameKind_IsNewOnce()
        {
            IPaceNoveltyKind kind = Kind(PaceNoveltyCatalog.FirstMinigame);
            var probe = new PaceProbe { MinigameMechanic = "Timing" };
            Assert.That(Run(kind, probe), Has.Count.EqualTo(1));
            Assert.That(Run(kind, probe), Is.Empty);
        }

        [Test]
        public void Catalog_GivesFreshKinds_WithUniqueNames_AndSessionKindsIgnoreNoSession()
        {
            List<IPaceNoveltyKind> a = new PaceNoveltyCatalog().Create(), b = new PaceNoveltyCatalog().Create();
            Assert.That(a.Select(k => k.Name), Is.Unique);
            Assert.That(a.Zip(b, (x, y) => ReferenceEquals(x, y)), Has.None.True, "판마다 새 기억");
            var probe = new PaceProbe(); // 게임 한 판이 없다(슬롯 선택)
            foreach (IPaceNoveltyKind k in a) Assert.That(Run(k, probe), Is.Empty, k.Name);
            Assert.That(a.Where(k => k.Milestone).Select(k => k.Name), Is.Not.Empty, "지점 종류가 있다");
        }
    }
}
