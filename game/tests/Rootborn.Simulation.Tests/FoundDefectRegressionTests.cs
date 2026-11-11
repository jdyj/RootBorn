using System;
using NUnit.Framework;
using Rootborn.Screens;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트: 시뮬레이션이 찾아 고친 결함의 가장 작은 재현(다시 생기지 않게). 아직 못 고친 결함은 KnownDefects와
    /// 그 지금 동작을 고정하는 재현 테스트(건너뜀 없음)에 둔다.
    /// </summary>
    public sealed class FoundDefectRegressionTests
    {
        private static ScreenStack HudStack()
        {
            var text = GameText.LoadMerged(GameText.DefaultBaseLanguage,
                System.IO.Path.Combine(PlayAtlas.FindRepoRoot(), "data", "localization"), System.IO.Path.Combine(PlayAtlas.FindRepoRoot(), "game", "localization"));
            var registry = new ScreenRegistry();
            registry.Add("probe.a", ScreenLayer.Hud, false, s => new ProbeScreen(s));
            registry.Add("probe.b", ScreenLayer.Hud, false, s => new ProbeScreen(s));
            var stack = new ScreenStack(registry, new ScreenServices(text, null));
            stack.OpenHud();
            return stack;
        }

        private static double PerCall(Action a)
        {
            for (int i = 0; i < 10; i++) a();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) a();
            return (GC.GetAllocatedBytesForCurrentThread() - before) / 1000.0;
        }

        /// <summary>
        /// SIM-001(고침): ScreenStack.Tick이 매 프레임 열린 화면 배열을 새로 만들었고(Snapshot().ToArray()), EnterMap은 같은 맵·같은 성질이어도
        /// IReadOnlyCollection으로 받은 배열을 foreach해 열거자를 만들었다. 둘 다 Host가 매 프레임 부른다(coding-standards "성능": 매 프레임 새 객체 금지).
        /// </summary>
        [Test]
        public void SIM001_ScreenStackTickAndEnterMap_DoNotAllocateOnIdleFrames()
        {
            ScreenStack stack = HudStack();
            double tick = PerCall(() => stack.Tick(1f / 60f));
            var features = new[] { MapFeatures.FurniturePlacement };
            stack.EnterMap("map", features);
            double enter = PerCall(() => stack.EnterMap("map", features));
            TestContext.WriteLine("Tick " + tick + "바이트, EnterMap(같은 맵) " + enter + "바이트");
            Assert.That(tick, Is.EqualTo(0), "ScreenStack.Tick은 가만히 선 프레임에 새 객체를 만들지 않는다");
            Assert.That(enter, Is.EqualTo(0), "같은 맵·같은 성질의 EnterMap은 새 객체를 만들지 않는다");
        }

        private sealed class ProbeScreen : GameScreen
        {
            public ProbeScreen(ScreenServices s) : base(s) { }
            protected override Rootborn.Ui.UiNode Build() => new Rootborn.Ui.UiNode { Id = "probe" };
        }
    }
}
