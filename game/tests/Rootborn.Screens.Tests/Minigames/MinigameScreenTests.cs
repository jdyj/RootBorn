using System;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Interaction;
using Rootborn.Core.Minigames;
using Rootborn.Screens.Minigames;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Minigames
{
    /// <summary>
    /// 미니게임 화면: 예약하면 열리고, 도전 종류마다 키·포인터로 끝까지 하면 GameSession에 정산된다(성공·실패·시간 초과·포기).
    /// 판정은 Core가 하고 화면은 입력만 넘긴다. 입력은 UiTree 포인터 판정(Click)과 화면 쌓기 키(Key)로만 넣는다.
    /// </summary>
    public sealed class MinigameScreenTests
    {
        private const float Frame = 1f / 30f;

        private static MinigameScreen Begin(TestSession t, string workId, string request = "req-test")
        {
            AdultCareerResult begun = t.Session.BeginPartTimeWork(workId, request, 7);
            Assert.That(begun.Kind, Is.EqualTo(AdultCareerResultKind.Applied), "알바 예약: " + workId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Minigame), Is.True, "예약하면 미니게임이 열린다");
            Assert.That(t.Stack.MovementBlocked, Is.True, "미니게임은 이동을 막는다");
            return t.Screen<MinigameScreen>(ScreenCatalog.Minigame);
        }

        private static MinigameScreen BeginPractice(TestSession t, string practiceId, string request = "req-practice")
        {
            AdultCareerResult begun = t.Session.BeginPractice(practiceId, request, 7);
            Assert.That(begun.Kind, Is.EqualTo(AdultCareerResultKind.Applied), "연습 예약: " + practiceId);
            return t.Screen<MinigameScreen>(ScreenCatalog.Minigame);
        }

        private static void Tick(TestSession t, float seconds)
        {
            for (float done = 0; done < seconds; done += Frame) t.Stack.Tick(Frame);
        }

        /// <summary>순서 외우기: 보여 주는 동안 순서를 읽고(안내 글), 끝나면 그 순서로 단추를 누른다.</summary>
        private static void SolveSequence(TestSession t, MinigameScreen screen)
        {
            ChallengeSession c = screen.Session.Current;
            Assert.That(c.Mechanic, Is.EqualTo(MinigameMechanic.Sequence));
            Assert.That(c.PreviewVisible, Is.True);
            int[] order = c.PreviewSequence;
            Assert.That(Feedback(t), Does.Contain(t.Text.Get("minigame.memorize")));
            Assert.That(t.Node(SymbolChoiceView.ChoiceId(0)).Enabled, Is.False, "보여 주는 동안 단추를 막는다");
            while (screen.Session.Current == c && c.PreviewVisible) t.Stack.Tick(Frame);
            Assert.That(Feedback(t), Is.EqualTo(t.Text.Get("minigame.repeat")));
            foreach (int answer in order) t.Click(SymbolChoiceView.ChoiceId(answer));
        }

        private static string Feedback(TestSession t) => ((UiTextBlock)t.Node(MinigameScreen.FeedbackId)).Text;
        private static string Status(TestSession t) => ((UiLabel)t.Node(MinigameScreen.StatusId)).Text;

        private static void AssertSettledOnce(TestSession t, MinigameScreen screen, string request)
        {
            Assert.That(screen.LastSettlement?.Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(screen.Settlements, Is.EqualTo(1));
            Assert.That(t.Session.Student.HasPendingMinigame, Is.False, "정산하면 예약이 지워진다");
            Tick(t, 1f);
            t.Stack.NotifyChanged(ChangeTopics.All);
            Assert.That(screen.Settlements, Is.EqualTo(1), "끝난 판을 다시 정산하지 않는다");
            Assert.That(t.Session.SettleMinigame(request, true).Applied, Is.False, "Core도 같은 요청을 두 번 받지 않는다");
        }

        [Test]
        public void Sequence_PlayedToTheEnd_PaysTheWorkOnce()
        {
            using var t = new TestSession();
            int moneyBefore = t.Session.Inventory.CountOf(t.Session.Item("item.allowance"));
            MinigameScreen screen = Begin(t, "work.shift.corner-store", "req-seq");
            t.Emit();
            Assert.That(((UiLabel)t.Node(MinigameScreen.TitleId)).Text, Is.EqualTo(t.Text.Get("minigame.title.sequence")));
            Assert.That(Status(t), Does.Contain("1/1"), "진행 줄: 단계·남은 초·실수");
            Assert.That(t.Node(MinigameScreen.PanelId).Rect, Is.EqualTo(new UiRect(510, 230, 900, 620)), "Unity 900x620 가운데");

            SolveSequence(t, screen);

            Assert.That(screen.Session.IsComplete && screen.Session.Succeeded, Is.True);
            Assert.That(Status(t), Is.EqualTo(t.Text.Format("minigame.result", screen.Session.Score)));
            Assert.That(((UiTextBlock)t.Node(MinigameScreen.InstructionId)).Text, Is.EqualTo(t.Text.Get("minigame.success")));
            Assert.That(t.Session.Inventory.CountOf(t.Session.Item("item.allowance")), Is.GreaterThan(moneyBefore), "성공하면 알바 보상을 받는다");
            AssertSettledOnce(t, screen, "req-seq");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Minigame), Is.True, "결과를 보여 주며 떠 있다(Unity와 같음)");
            t.Stack.Key(ScreenKey.Escape, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Minigame), Is.False, "Esc로 닫는다");
            Assert.That(screen.Settlements, Is.EqualTo(1));

            t.Emit();
            t.Text.Language = "en";
            t.Emit();
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, screen.TextKeys);
        }

        [Test]
        public void Sequence_TooManyMistakes_FailsAndForfeitsWithoutPay()
        {
            using var t = new TestSession();
            int moneyBefore = t.Session.Inventory.CountOf(t.Session.Item("item.allowance"));
            MinigameScreen screen = Begin(t, "work.shift.corner-store", "req-fail");
            int[] order = screen.Session.Current.PreviewSequence;
            Tick(t, 5f);
            int wrong = (order[0] + 1) % screen.Session.Current.Symbols.Length;
            for (int i = 0; i < 20 && !screen.Session.IsComplete; i++) t.Click(SymbolChoiceView.ChoiceId(wrong));

            Assert.That(screen.Session.IsComplete, Is.True);
            Assert.That(screen.Session.Succeeded, Is.False);
            Assert.That(((UiTextBlock)t.Node(MinigameScreen.InstructionId)).Text, Is.EqualTo(t.Text.Get("minigame.retry")));
            Assert.That(t.Session.Inventory.CountOf(t.Session.Item("item.allowance")), Is.EqualTo(moneyBefore), "실패하면 보상이 없다");
            AssertSettledOnce(t, screen, "req-fail");
        }

        [Test]
        public void TimeRunsOnlyWithTicks_AndTimeoutSettles()
        {
            using var t = new TestSession();
            MinigameScreen screen = Begin(t, "work.shift.corner-store", "req-timeout");
            float before = screen.Session.Current.Elapsed;
            t.Emit();
            t.Emit();
            Assert.That(screen.Session.Current.Elapsed, Is.EqualTo(before), "그리기만으로는 시간이 흐르지 않는다(화면은 시계를 읽지 않는다)");
            Tick(t, 40f);
            Assert.That(screen.Session.IsComplete, Is.True, "제한 시간 35초가 지나면 끝난다");
            Assert.That(screen.Session.Score, Is.EqualTo(0));
            AssertSettledOnce(t, screen, "req-timeout");
        }

        [Test]
        public void EscapeMidGame_ForfeitsTheReservation_AndCloses()
        {
            using var t = new TestSession();
            int energy = t.Session.Student.Energy;
            MinigameScreen screen = Begin(t, "work.shift.corner-store", "req-esc");
            Tick(t, 1f);
            Assert.That(t.Stack.Key(ScreenKey.Interact, false), Is.True, "판이 떠 있는 동안 상호작용 키는 월드로 가지 않는다");
            t.Stack.Key(ScreenKey.Escape, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Minigame), Is.False);
            Assert.That(screen.Session.IsCancelled, Is.True);
            Assert.That(t.Session.Student.HasPendingMinigame, Is.False, "포기 명령으로 예약이 지워진다");
            Assert.That(t.Session.Student.Energy, Is.LessThan(energy), "쓴 비용은 돌아오지 않는다(Core 규칙)");
            Assert.That(t.Session.ForfeitMinigame("req-esc").Applied, Is.False, "같은 판은 두 번 포기되지 않는다");
            Assert.That(screen.Settlements, Is.EqualTo(1));
        }

        [Test]
        public void CloseButton_IsTheSameAsEscape()
        {
            using var t = new TestSession();
            MinigameScreen screen = Begin(t, "work.shift.corner-store", "req-close");
            t.Click(MinigameScreen.CloseId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Minigame), Is.False);
            Assert.That(screen.Session.IsCancelled, Is.True);
            Assert.That(t.Session.Student.HasPendingMinigame, Is.False);
        }

        [Test]
        public void SequenceThenTiming_SpaceOnTheBeats_SucceedsThePractice()
        {
            using var t = new TestSession();
            MinigameScreen screen = BeginPractice(t, "practice.chef", "req-chef");
            SolveSequence(t, screen);
            Assert.That(screen.Session.IsComplete, Is.False, "두 번째 단계(박자)");
            ChallengeSession c = screen.Session.Current;
            Assert.That(c.Mechanic, Is.EqualTo(MinigameMechanic.Timing));
            Assert.That(((UiLabel)t.Node(MinigameScreen.TitleId)).Text, Is.EqualTo(t.Text.Get("minigame.title.timing")));
            UiNode cursor = t.Node(TimingView.CursorId);
            float x0 = cursor.Rect.X;
            foreach (float beat in c.BeatTimes)
            {
                while (c.Elapsed < beat) t.Stack.Tick(Frame);
                Assert.That(t.Stack.Key(ScreenKey.Space, false), Is.True, "Space로 누른다");
            }
            Assert.That(cursor.Rect.X, Is.GreaterThan(x0), "커서가 시간에 따라 움직인다");
            Tick(t, 2f);
            Assert.That(screen.Session.IsComplete && screen.Session.Succeeded, Is.True, "점수 " + screen.Session.Score);
            AssertSettledOnce(t, screen, "req-chef");
        }

        [Test]
        public void Timing_TapButtonWithThePointer_CountsAsATap()
        {
            using var t = new TestSession();
            MinigameScreen screen = BeginPractice(t, "practice.chef", "req-chef-tap");
            SolveSequence(t, screen);
            ChallengeSession c = screen.Session.Current;
            while (c.Elapsed < c.BeatTimes[0]) t.Stack.Tick(Frame);
            t.Click(TimingView.TapId);
            Assert.That(c.ProgressIndex, Is.EqualTo(1));
            t.Click(TimingView.TapId);
            Assert.That(c.Mistakes, Is.EqualTo(1), "박자 밖에서 누르면 실수");
        }

        [Test]
        public void Choice_ThenSequence_AnswersFromTheData_Succeed()
        {
            using var t = new TestSession();
            MinigameScreen screen = BeginPractice(t, "practice.emergency-care", "req-care");
            ChallengeSession c = screen.Session.Current;
            Assert.That(c.Mechanic, Is.EqualTo(MinigameMechanic.Choice));
            int[] answers = { 0, 1, 2 }; // data/studentlife/ChoiceChallengeDefinition.json Care1.answers
            for (int i = 0; i < answers.Length; i++)
            {
                Assert.That(((UiTextBlock)t.Node(MinigameScreen.InstructionId)).Text, Is.EqualTo(t.Text.Get(c.PromptKey)), "질문마다 안내가 바뀐다");
                t.Click(SymbolChoiceView.ChoiceId(answers[i]));
            }
            SolveSequence(t, screen);
            Assert.That(screen.Session.IsComplete && screen.Session.Succeeded, Is.True);
            AssertSettledOnce(t, screen, "req-care");
            t.Emit();
            t.Text.Language = "en";
            t.Emit();
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, screen.TextKeys);
        }

        [Test]
        public void Choice_KeyboardFocusAndEnter_AlsoChoose()
        {
            using var t = new TestSession();
            MinigameScreen screen = BeginPractice(t, "practice.emergency-care", "req-care-keys");
            ChallengeSession c = screen.Session.Current;
            t.Stack.Key(ScreenKey.Right, false); // 이동을 막는 화면: 방향 키가 포커스를 옮긴다
            Assert.That(t.Stack.Tree.Focused, Is.Not.Null);
            t.Stack.Key(ScreenKey.Enter, false);
            Assert.That(c.ProgressIndex, Is.EqualTo(1), "Enter로 고른다");
        }

        [Test]
        public void Placement_PointerOnCells_AndRKey_FillTheBoard()
        {
            using var t = new TestSession();
            MinigameScreen screen = Begin(t, "work.studio-helper", "req-place");
            ChallengeSession c = screen.Session.Current;
            Assert.That(c.Mechanic, Is.EqualTo(MinigameMechanic.Placement));
            var view = (PlacementView)screen.View;
            t.Stack.Key(ScreenKey.Rotate, false);
            Assert.That(view.Rotation, Is.EqualTo(1), "R로 돌린다");
            t.Stack.Key(ScreenKey.Rotate, false); t.Stack.Key(ScreenKey.Rotate, false); t.Stack.Key(ScreenKey.Rotate, false);
            Assert.That(view.Rotation, Is.EqualTo(0));

            t.Click(PlacementView.CellId(2, 0));
            Assert.That(c.TryGetPieceRect(0, out _), Is.False, "막힌 칸에는 놓지 못한다");
            Assert.That(Feedback(t), Is.EqualTo(t.Text.Get("minigame.placement-invalid")));

            for (int guard = 0; guard < 10 && !screen.Session.IsComplete; guard++)
            {
                int piece = view.SelectedPiece;
                bool placed = false;
                for (int y = 0; y < c.BoardSize.Y && !placed; y++)
                    for (int x = 0; x < c.BoardSize.X && !placed; x++)
                    {
                        if (c.TryGetPieceRect(piece, out _)) { placed = true; break; }
                        t.Click(PlacementView.CellId(x, y));
                        placed = c.TryGetPieceRect(piece, out _) || screen.Session.IsComplete;
                    }
            }
            Assert.That(screen.Session.IsComplete && screen.Session.Succeeded, Is.True, "조각을 다 놓으면 끝난다");
            Assert.That(screen.Session.Score, Is.EqualTo(100));
            AssertSettledOnce(t, screen, "req-place");
        }

        [Test]
        public void Placement_Timeout_SettlesAsFailure()
        {
            using var t = new TestSession();
            MinigameScreen screen = Begin(t, "work.studio-helper", "req-place-timeout");
            Tick(t, 55f);
            Assert.That(screen.Session.IsComplete, Is.True);
            Assert.That(screen.Session.Succeeded, Is.False);
            AssertSettledOnce(t, screen, "req-place-timeout");
        }

        [Test]
        public void TargetDodge_AimWithThePointer_DodgeWithKeys_Succeeds()
        {
            using var t = new TestSession();
            MinigameScreen screen = BeginPractice(t, "practice.soldier", "req-dodge");
            ChallengeSession c = screen.Session.Current;
            Assert.That(c.Mechanic, Is.EqualTo(MinigameMechanic.TargetDodge));
            t.Stack.Key(ScreenKey.AltRight, false);
            Assert.That(c.Lane, Is.EqualTo(1), "D로 줄을 옮긴다");
            t.Stack.Key(ScreenKey.Left, false);
            Assert.That(c.Lane, Is.EqualTo(0), "←로 줄을 옮긴다");
            t.Click(TargetDodgeView.LaneId(2));
            Assert.That(c.Lane, Is.EqualTo(2), "줄 단추를 눌러도 옮긴다");

            UiNode surface = t.Node(TargetDodgeView.SurfaceId);
            for (int frame = 0; frame < 30 * 25 && !screen.Session.IsComplete; frame++)
            {
                if (c.NextObstacleLane == c.Lane) t.Stack.Key(c.Lane > 0 ? ScreenKey.AltLeft : ScreenKey.AltRight, false);
                if (c.HasTarget && frame % 10 == 0)
                {
                    PlanePoint p = c.TargetPosition;
                    UiRect r = surface.Rect;
                    float x = r.X + p.X * r.W, y = r.Y + (1f - p.Y) * r.H;
                    Assert.That(t.Stack.Tree.HitTest(x, y), Is.SameAs(surface), "과녁 위를 눌러도 조준 판이 받는다");
                    t.Stack.Pointer(x, y, false, 0); t.Stack.Pointer(x, y, true, 0); t.Stack.Pointer(x, y, false, 0);
                }
                t.Stack.Tick(Frame);
            }
            Assert.That(screen.Session.IsComplete, Is.True);
            Assert.That(screen.Session.Succeeded, Is.True, "점수 " + screen.Session.Score);
            AssertSettledOnce(t, screen, "req-dodge");
        }

        [Test]
        public void WorkPointInteraction_ReservesAndOpensTheMinigame()
        {
            using var t = new TestSession();
            t.Stack.Interaction(new InteractionEvent("Town", "part-time-work", "work.shift.corner-store"));
            Assert.That(t.Session.Student.HasPendingMinigame, Is.True, "일터 지점에서 상호작용하면 알바를 예약한다");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Minigame), Is.True);
            var screen = t.Screen<MinigameScreen>(ScreenCatalog.Minigame);
            Assert.That(screen.Model.RequestId, Is.EqualTo(t.Session.Student.PendingMinigame.RequestId));
        }

        [Test]
        public void ReservationLeftInTheSave_OnTheSameDay_OpensRightAfterLoading()
        {
            using var t = new TestSession();
            MinigameScreen first = Begin(t, "work.shift.corner-store", "req-left");
            string request = first.Model.RequestId;
            // 정산하지 않고 게임을 끝낸 뒤 다시 불러온다(같은 날).
            using TestSession again = t.Reopen();
            Assert.That(again.Session.Student.HasPendingMinigame, Is.True);
            Assert.That(again.Stack.IsOpen(ScreenCatalog.Minigame), Is.True, "같은 날 남은 예약은 불러오자마자 미니게임 화면을 연다");
            Assert.That(again.Screen<MinigameScreen>(ScreenCatalog.Minigame).Model.RequestId, Is.EqualTo(request));
            again.Session.TalkTo("npc.first-guide");
            Assert.That(again.Screen<MinigameScreen>(ScreenCatalog.Minigame).Settlements, Is.Zero, "다른 알림으로 다시 열거나 정산하지 않는다");
        }

        [Test]
        public void StaleReservation_WithAnEarlierDay_DoesNotOpen()
        {
            using var t = new TestSession();
            t.Session.BeginPartTimeWork("work.shift.corner-store", "req-stale", 1);
            Assert.That(MinigameModel.CanResumeOnLoad(t.Session), Is.True, "오늘 예약");
            Assert.That(MinigameModel.CanResumeOnLoad(t.Session, t.Session.StudentDay + 1), Is.False, "날이 지난 예약은 열지 않는다(Core가 포기 처리)");
        }
    }
}
