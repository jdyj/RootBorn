using System;
using NUnit.Framework;
using Rootborn.Screens;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트: 시뮬레이션이 찾은 결함 중 원인이 이 작업 범위 밖(게임 코드)인 것의 가장 작은 재현. 지금 동작을 고정한다 —
    /// 고쳐지면 이 테스트가 실패하므로 그때 KnownDefects 목록·PlayLimits와 함께 지운다. 건너뜀 표시를 하지 않는다.
    /// </summary>
    public sealed class KnownDefectReproTests
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
        /// SIM-001: ScreenStack.Tick이 매 프레임 열린 화면 배열을 새로 만든다(Snapshot().ToArray()), EnterMap은 같은 맵·같은 성질이어도
        /// IReadOnlyCollection으로 받은 배열을 foreach해 열거자를 만든다. 둘 다 Host가 매 프레임 부른다(coding-standards "성능": 매 프레임 새 객체 금지).
        /// 재현 씨앗: 아무 판(fast-*), 가만히 선 프레임. 원인: game/Rootborn.Screens/ScreenStack.cs Tick·SameFeatures.
        /// </summary>
        [Test]
        public void SIM001_ScreenStackTickAndEnterMap_AllocateEveryFrame()
        {
            ScreenStack stack = HudStack();
            double tick = PerCall(() => stack.Tick(1f / 60f));
            var features = new[] { MapFeatures.FurniturePlacement };
            stack.EnterMap("map", features);
            double enter = PerCall(() => stack.EnterMap("map", features));
            TestContext.WriteLine("Tick " + tick + "바이트, EnterMap(같은 맵) " + enter + "바이트");
            Assert.That(tick, Is.GreaterThan(0), "고쳐졌다면 이 테스트와 KnownDefects SIM-001, PlayLimits.IdleFrameBytes를 정리한다");
            Assert.That(enter, Is.GreaterThan(0), "고쳐졌다면 이 테스트와 KnownDefects SIM-001, PlayLimits.IdleFrameBytes를 정리한다");
        }

        private sealed class ProbeScreen : GameScreen
        {
            public ProbeScreen(ScreenServices s) : base(s) { }
            protected override Rootborn.Ui.UiNode Build() => new Rootborn.Ui.UiNode { Id = "probe" };
        }
    }
}
