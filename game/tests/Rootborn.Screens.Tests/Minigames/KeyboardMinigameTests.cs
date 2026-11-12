using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Minigames;
using Rootborn.Screens.Minigames;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Minigames
{
    /// <summary>
    /// 미니게임 도전 종류마다 포인터 없이 끝까지: 방향 키로 단추·칸을 옮기고 Enter, 박자는 Space, 조각 돌리기는 R, 줄은 A/D·←/→.
    /// 미니게임은 이동을 막는 화면이라 방향 키만으로 옮긴다. 예약은 GameSession 명령(준비), 푸는 것은 ScreenStack.Key만. 자동 테스트다.
    /// </summary>
    public sealed class KeyboardMinigameTests
    {
        private const float Frame = 1f / 30f;

        private static MinigameScreen Practice(TestSession t, string practiceId, string request)
        {
            Assert.That(t.Session.BeginPractice(practiceId, request, 7).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            return t.Screen<MinigameScreen>(ScreenCatalog.Minigame);
        }

        private static MinigameScreen Work(TestSession t, string workId, string request)
        {
            Assert.That(t.Session.BeginPartTimeWork(workId, request, 7).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            return t.Screen<MinigameScreen>(ScreenCatalog.Minigame);
        }

        private static void Press(TestSession t, string id) => KeyboardPath.Press(t.Stack, t.Node(id), shift: false);

        private static void SolveSequenceWithKeys(TestSession t, MinigameScreen screen)
        {
            ChallengeSession c = screen.Session.Current;
            Assert.That(c.Mechanic, Is.EqualTo(MinigameMechanic.Sequence));
            int[] order = c.PreviewSequence;
            while (screen.Session.Current == c && c.PreviewVisible) t.Stack.Tick(Frame);
            foreach (int answer in order) Press(t, SymbolChoiceView.ChoiceId(answer));
        }

        [Test]
        public void Sequence_WithArrowsAndEnter_Succeeds()
        {
            using var t = new TestSession();
            MinigameScreen screen = Work(t, "work.shift.corner-store", "req-keys-seq");
            for (int guard = 0; guard < 8 && !screen.Session.IsComplete; guard++) SolveSequenceWithKeys(t, screen);
            Assert.That(screen.Session.IsComplete && screen.Session.Succeeded, Is.True, "점수 " + screen.Session.Score);
        }

        [Test]
        public void Choice_ThenSequence_WithArrowsAndEnter_Succeed()
        {
            using var t = new TestSession();
            MinigameScreen screen = Practice(t, "practice.emergency-care", "req-keys-care");
            ChallengeSession c = screen.Session.Current;
            Assert.That(c.Mechanic, Is.EqualTo(MinigameMechanic.Choice));
            foreach (int answer in new[] { 0, 1, 2 }) Press(t, SymbolChoiceView.ChoiceId(answer)); // data/studentlife/ChoiceChallengeDefinition.json Care1.answers
            SolveSequenceWithKeys(t, screen);
            Assert.That(screen.Session.IsComplete && screen.Session.Succeeded, Is.True);
        }

        [Test]
        public void Timing_SpaceOnTheBeats_AfterAKeyboardSequence_Succeeds()
        {
            using var t = new TestSession();
            MinigameScreen screen = Practice(t, "practice.chef", "req-keys-chef");
            SolveSequenceWithKeys(t, screen);
            ChallengeSession c = screen.Session.Current;
            Assert.That(c.Mechanic, Is.EqualTo(MinigameMechanic.Timing));
            foreach (float beat in c.BeatTimes)
            {
                while (c.Elapsed < beat) t.Stack.Tick(Frame);
                t.Stack.Key(ScreenKey.Space, false);
            }
            for (int i = 0; i < 60; i++) t.Stack.Tick(Frame);
            Assert.That(screen.Session.IsComplete && screen.Session.Succeeded, Is.True, "점수 " + screen.Session.Score);
        }

        [Test]
        public void Timing_TapButton_CanAlsoBePressedWithEnter()
        {
            using var t = new TestSession();
            MinigameScreen screen = Practice(t, "practice.chef", "req-keys-chef-tap");
            SolveSequenceWithKeys(t, screen);
            ChallengeSession c = screen.Session.Current;
            while (c.Elapsed < c.BeatTimes[0]) t.Stack.Tick(Frame);
            Press(t, TimingView.TapId);
            Assert.That(c.ProgressIndex, Is.EqualTo(1), "박자 단추에 포커스를 두고 Enter");
        }

        [Test]
        public void Placement_CellsWithArrowsAndEnter_RotateWithR_FillTheBoard()
        {
            using var t = new TestSession();
            MinigameScreen screen = Work(t, "work.studio-helper", "req-keys-place");
            ChallengeSession c = screen.Session.Current;
            Assert.That(c.Mechanic, Is.EqualTo(MinigameMechanic.Placement));
            var view = (PlacementView)screen.View;
            t.Stack.Key(ScreenKey.Rotate, false);
            Assert.That(view.Rotation, Is.EqualTo(1), "R로 돌린다");
            for (int i = 0; i < 3; i++) t.Stack.Key(ScreenKey.Rotate, false);

            for (int guard = 0; guard < 10 && !screen.Session.IsComplete; guard++)
            {
                int piece = view.SelectedPiece;
                bool placed = false;
                for (int y = 0; y < c.BoardSize.Y && !placed; y++)
                    for (int x = 0; x < c.BoardSize.X && !placed; x++)
                    {
                        if (!t.Node(PlacementView.CellId(x, y)).IsActive) continue;
                        Press(t, PlacementView.CellId(x, y));
                        placed = c.TryGetPieceRect(piece, out _) || screen.Session.IsComplete;
                    }
            }
            Assert.That(screen.Session.IsComplete && screen.Session.Succeeded, Is.True, "조각을 다 놓으면 끝난다");
        }

        [Test]
        public void Placement_PieceButtons_CanBeChosenWithEnter()
        {
            using var t = new TestSession();
            MinigameScreen screen = Work(t, "work.studio-helper", "req-keys-piece");
            var view = (PlacementView)screen.View;
            ChallengeSession c = screen.Session.Current;
            if (!t.Stack.Tree.Root.Find(PlacementView.PieceId(1))?.IsActive ?? true) Assert.Ignore("조각이 하나뿐인 데이터");
            Press(t, PlacementView.PieceId(1));
            Assert.That(view.SelectedPiece, Is.EqualTo(1), "조각 단추에 포커스를 두고 Enter로 고른다");
        }

        /// <summary>
        /// 조준·피하기: 줄은 A/D·←/→로 옮기지만, 움직이는 과녁을 맞히는 조준 판은 포인터로만 누른다(포커스를 받지 않는다).
        /// 키보드만으로는 장애물을 모두 피해 시간이 다 되면 끝나지만 과녁 몫의 점수를 얻지 못한다 — 조준 키(커서)는 새 키 배정이 필요한
        /// 구성 변경이라 고치지 않고 보고한다. 조준 키를 더하면 이 시험을 "키보드로 만점"으로 바꾼다.
        /// </summary>
        [Test]
        public void TargetDodge_LanesWorkWithKeys_ButAimingIsPointerOnly_KnownGap()
        {
            using var t = new TestSession();
            MinigameScreen screen = Practice(t, "practice.soldier", "req-keys-dodge");
            ChallengeSession c = screen.Session.Current;
            Assert.That(c.Mechanic, Is.EqualTo(MinigameMechanic.TargetDodge));
            t.Stack.Key(ScreenKey.Right, false);
            Assert.That(c.Lane, Is.EqualTo(1), "→로 줄을 옮긴다");
            t.Stack.Key(ScreenKey.AltLeft, false);
            Assert.That(c.Lane, Is.EqualTo(0), "A로 줄을 옮긴다");

            Assert.That(c.HasTarget, Is.True, "이 연습에는 과녁이 있다");
            UiNode surface = t.Node(TargetDodgeView.SurfaceId);
            Assert.That(surface.Focusable, Is.False, "알려진 빈 곳: 조준 판은 키보드 포커스를 받지 않는다(보고서 '필요한 구성 변경')");

            int targets = 0;
            for (int frame = 0; frame < 30 * 60 && !screen.Session.IsComplete; frame++)
            {
                if (c.NextObstacleLane == c.Lane) t.Stack.Key(c.Lane > 0 ? ScreenKey.Left : ScreenKey.Right, false);
                if (c.HasTarget) targets = 1;
                t.Stack.Tick(Frame);
            }
            Assert.That(screen.Session.IsComplete, Is.True, "키보드로 피하기만 해도 판은 끝난다");
            Assert.That(c.HasTarget, Is.True, "과녁은 하나도 맞히지 못했다");
            TestContext.WriteLine("키보드만: 성공 " + screen.Session.Succeeded + ", 점수 " + screen.Session.Score + " (포인터로 과녁까지 맞히면 만점 시험: MinigameScreenTests.TargetDodge_AimWithThePointer_DodgeWithKeys_Succeeds)");
            Assert.That(screen.Session.Score, Is.LessThan(100), "과녁 몫의 점수를 얻지 못한다");
        }
    }
}
