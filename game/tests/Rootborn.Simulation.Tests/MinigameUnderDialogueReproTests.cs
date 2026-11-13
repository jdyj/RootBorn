using NUnit.Framework;
using Rootborn.Screens;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// SIM-005 재현(고치지 않음 — 게임 코드 결정): 대화(Dialogue)는 이동을 막지 않는 모달이라 떠 있는 채 걸어가 E로 알바를 시작할 수 있고,
    /// 그러면 미니게임이 대화 위에 뜬다. 화면 쌓기는 떠 있는 화면 중 하나라도 시계를 멈추면 멈추므로(ScreenStack.PausesClock) 그 미니게임
    /// 동안 혼자 하기의 시계가 서 있다 — 흐르는 시간 설계 2절 "미니게임 중에는 흐른다"와 다르다. 2026-11-13 이것저것 유형 7일 판 4일째
    /// (대화 > 미니게임, 4일째 964분에서 시계가 서 있었다). 실제 등록 표의 성질을 옮긴 시험 화면 둘로 지금 동작을 고정한다 —
    /// 고쳐지면 이 테스트가 실패하므로 그때 이 테스트와 KnownDefects의 SIM-005를 함께 지운다.
    /// </summary>
    public sealed class MinigameUnderDialogueReproTests
    {
        [Test]
        public void SIM005_AMinigameStartedWithTheDialogueOpen_RunsWithTheClockPaused()
        {
            ScreenRegistry real = ScreenCatalog.Create();
            Assert.That(real.TryGet(ScreenCatalog.Dialogue, out ScreenEntry dialogue) && real.TryGet(ScreenCatalog.Minigame, out _), "등록 표에 두 화면");
            real.TryGet(ScreenCatalog.Minigame, out ScreenEntry minigame);
            Assert.That(dialogue.BlocksMovement, Is.False, "대화가 떠 있어도 걷고 E를 누를 수 있다");
            Assert.That(dialogue.PausesClock, Is.True, "대화는 시계를 멈춘다");
            Assert.That(minigame.PausesClock, Is.False, "미니게임은 시계를 멈추지 않는 화면이다");

            var text = GameText.LoadMerged(GameText.DefaultBaseLanguage,
                System.IO.Path.Combine(PlayAtlas.FindRepoRoot(), "data", "localization"), System.IO.Path.Combine(PlayAtlas.FindRepoRoot(), "game", "localization"));
            var probe = new ScreenRegistry();
            probe.Add("probe.talk", dialogue.Layer, dialogue.BlocksMovement, s => new Probe(s));
            probe.Add("probe.game", minigame.Layer, minigame.BlocksMovement, s => new Probe(s)).KeepsClockRunning();
            var stack = new ScreenStack(probe, new ScreenServices(text, null));
            stack.Open("probe.talk");
            Assert.That(stack.MovementBlocked, Is.False);
            stack.Open("probe.game");
            Assert.That(stack.IsOpen("probe.talk") && stack.IsOpen("probe.game"), "대화 위에 미니게임");
            Assert.That(stack.PausesClock, Is.True, "지금 동작: 미니게임 동안 시계가 서 있다");
        }

        private sealed class Probe : GameScreen
        {
            public Probe(ScreenServices s) : base(s) { }
            protected override Rootborn.Ui.UiNode Build() => new Rootborn.Ui.UiNode { Id = "probe" };
        }
    }
}
