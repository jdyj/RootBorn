using System;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/Minigames/MinigameSessionTests.cs
    public sealed class MinigameSessionTests
    {
        private static SequenceChallenge Sequence(float timeLimitSeconds = 20f)
            => new SequenceChallenge("seq", new[] { 0, 1, 2 }, new[] { "a", "b", "c" }, revealSeconds: 2f, timeLimitSeconds: timeLimitSeconds, mistakeAllowance: 3);

        private static TimingChallenge Timing() => new TimingChallenge("timing", new[] { 1f, 2f }, windowSeconds: .15f, timeLimitSeconds: 3f);

        private static PlacementChallenge Placement(GridPoint? boardSize = null, GridPoint[] blocked = null)
            => new PlacementChallenge("place", boardSize ?? new GridPoint(4, 3), new[] { new GridPoint(2, 1), new GridPoint(1, 2) }, blocked, null, timeLimitSeconds: 30f);

        private static TargetDodgeChallenge Targets()
            => new TargetDodgeChallenge("target", new[] { new PlanePoint(.5f, .5f) }, new[] { 0 }, obstacleIntervalSeconds: 1f, targetRadius: .1f, targetWobble: 0f, laneCount: 3, timeLimitSeconds: 2f);

        private static Minigame Game(params IChallenge[] challenges) => new Minigame("game", "game", challenges, null);
        private static MinigameSession Session(IChallenge challenge) => new MinigameSession(Game(challenge), null, 123);

        [Test]
        public void SymbolSprites_AreKeptOnTheChallengeAndTheSession_InOrder()
        {
            var sprites = new[] { "art/a.png", "", "art/c.png" };
            var challenge = new SequenceChallenge("seq", new[] { 0, 1, 2 }, new[] { "a", "b", "c" }, symbolSprites: sprites);
            sprites[0] = "changed";
            Assert.That(challenge.SymbolSprites, Is.EqualTo(new[] { "art/a.png", "", "art/c.png" }), "정의는 넘긴 배열을 복사한다");
            Assert.That(Session(challenge).Current.SymbolSprites, Is.EqualTo(new[] { "art/a.png", "", "art/c.png" }));
            Assert.That(Sequence().SymbolSprites, Is.Empty);
            Assert.That(Session(Timing()).Current.SymbolSprites, Is.Empty);
        }

        [Test]
        public void Sequence_PreviewRejectsInput_ThenCorrectOrderCompletes()
        {
            var s = Session(Sequence());
            Assert.That(s.Choose(0), Is.False);
            s.AdvanceTime(2f);
            Assert.That(s.Choose(0), Is.True);
            s.Choose(1);
            s.Choose(2);
            Assert.That(s.IsComplete, Is.True);
            Assert.That(s.Score, Is.EqualTo(100));
        }

        [Test]
        public void Sequence_WrongOrderCannotEarnPerfectScore()
        {
            var s = Session(Sequence());
            s.AdvanceTime(2f);
            s.Choose(2); s.Choose(0); s.Choose(1); s.Choose(2);
            Assert.That(s.Score, Is.LessThan(100));
        }

        [Test]
        public void Choices_RecordWrongAnswersAndScoreActualSuccesses()
        {
            var s = Session(new ChoiceChallenge("choice", new[] { 0, 1 }, new[] { "p0", "p1" }, new[] { "a", "b" }));
            s.Choose(1); s.Choose(1);
            Assert.That(s.IsComplete, Is.True);
            Assert.That(s.Score, Is.EqualTo(50));
        }

        [Test]
        public void CompletedSession_RejectsFurtherInputsAndPreservesScore()
        {
            var s = Session(new ChoiceChallenge("choice", new[] { 0 }, new[] { "p" }, new[] { "a", "b" }));
            s.Choose(0);
            Assert.That(s.Choose(1), Is.False);
            s.AdvanceTime(100f);
            Assert.That(s.Score, Is.EqualTo(100));
        }

        [Test]
        public void CancelledSession_NeverBecomesRewardable()
        {
            var s = Session(Sequence());
            s.Cancel();
            s.AdvanceTime(5f);
            Assert.That(s.Choose(0), Is.False);
            Assert.That(s.IsCancelled, Is.True);
            Assert.That(s.Succeeded, Is.False);
        }

        [Test]
        public void TimeoutWithoutInput_IsFailure()
        {
            var s = Session(Sequence(timeLimitSeconds: 5f));
            s.AdvanceTime(10f);
            Assert.That(s.IsComplete, Is.True);
            Assert.That(s.Succeeded, Is.False);
            Assert.That(s.Score, Is.EqualTo(0));
        }

        [Test]
        public void Timing_OnBeatsCompletesAtMaximumScore()
        {
            var s = Session(Timing());
            s.AdvanceTime(1f); s.Tap(); s.AdvanceTime(1f); s.Tap(); s.AdvanceTime(1f);
            Assert.That(s.IsComplete, Is.True);
            Assert.That(s.Score, Is.EqualTo(100));
        }

        [Test]
        public void Timing_OutsideWindowDoesNotCount()
        {
            var s = Session(Timing());
            s.AdvanceTime(.25f); s.Tap(); s.AdvanceTime(3f);
            Assert.That(s.Succeeded, Is.False);
            Assert.That(s.Score, Is.EqualTo(0));
        }

        [Test]
        public void Timing_RepeatedTapCannotCreditSameBeatTwice()
        {
            var s = Session(Timing());
            s.AdvanceTime(1f); s.Tap();
            Assert.That(s.Tap(), Is.False);
            s.AdvanceTime(3f);
            Assert.That(s.Score, Is.LessThanOrEqualTo(50));
        }

        [Test]
        public void Placement_BoundsAndOverlapAreRejectedWithoutChangingBoard()
        {
            var s = Session(Placement());
            Assert.That(s.Place(0, new GridPoint(-1, 0), 0), Is.False);
            Assert.That(s.Place(0, new GridPoint(0, 1), 0), Is.True);
            Assert.That(s.Place(1, new GridPoint(1, 1), 0), Is.False);
            Assert.That(s.IsComplete, Is.False);
        }

        [Test]
        public void Placement_RotationFitsNarrowSpaceAndCompletes()
        {
            var s = Session(Placement(new GridPoint(2, 3)));
            Assert.That(s.Place(0, new GridPoint(0, 1), 1), Is.True);
            Assert.That(s.Place(1, new GridPoint(1, 1), 0), Is.True);
            Assert.That(s.IsComplete, Is.True);
            Assert.That(s.Score, Is.EqualTo(100));
        }

        [Test]
        public void Placement_ReservedEntryCellsStayClear()
        {
            var s = Session(Placement(blocked: new[] { new GridPoint(0, 0) }));
            Assert.That(s.Place(0, new GridPoint(0, 0), 0), Is.False);
        }

        [Test]
        public void TargetDodge_RequiresRealAimAndAvoidanceInputs()
        {
            var s = Session(Targets());
            Assert.That(s.Aim(new PlanePoint(.1f, .1f)), Is.False);
            Assert.That(s.Aim(new PlanePoint(.5f, .5f)), Is.True);
            s.MoveLane(1);
            s.AdvanceTime(3f);
            Assert.That(s.IsComplete, Is.True);
            Assert.That(s.Score, Is.GreaterThan(0));
        }

        [Test]
        public void TargetDodge_ObstacleCollisionReducesScore()
        {
            var d = Targets();
            var hit = Session(d);
            var safe = Session(d);
            hit.Aim(new PlanePoint(.5f, .5f));
            safe.Aim(new PlanePoint(.5f, .5f));
            safe.MoveLane(1);
            hit.AdvanceTime(3f);
            safe.AdvanceTime(3f);
            Assert.That(hit.Score, Is.LessThan(safe.Score));
        }

        [Test]
        public void TraitBonus_WidensTimingWindowWithoutChangingTrait()
        {
            var rule = new GrowthMinigameBonusRule("fine", null, MinigameBonusTarget.TimingWindow, .01f, .3f);
            var p = new StudentLifeProgress("mini", "p", 10, 10);
            p.AddTrait("fine", 10);
            var game = new Minigame("game", "game", new IChallenge[] { Timing() }, new IMinigameBonusRule[] { rule });
            var s = new MinigameSession(game, p, 123);
            Assert.That(s.Current.TimingWindow, Is.EqualTo(.25f).Within(.001f));
            Assert.That(p.GetTraitValue("fine"), Is.EqualTo(10));
        }

        [Test]
        public void MultiStageGame_RequiresEveryChallengeBeforeReward()
        {
            var first = new ChoiceChallenge("choice", new[] { 0 }, new[] { "p" }, new[] { "a", "b" });
            var s = new MinigameSession(Game(first, Sequence()), null, 123);
            s.Choose(0);
            Assert.That(s.IsComplete, Is.False);
            s.AdvanceTime(2f);
            s.Choose(0); s.Choose(1); s.Choose(2);
            Assert.That(s.Succeeded, Is.True);
        }

        [Test]
        public void EmptyDefinition_FailsClosed()
        {
            Assert.Throws<ArgumentException>(() => new MinigameSession(Game(), null, 123));
            Assert.Throws<ArgumentException>(() => new MinigameSession(null, null, 123));
        }

        [Test]
        public void NegativeTimeCannotRewindTheChallenge()
        {
            var s = Session(Sequence());
            s.AdvanceTime(2f);
            Assert.That(s.AdvanceTime(-1f), Is.False);
            Assert.That(s.Choose(0), Is.True);
        }

        // 원래 테스트가 안 다루던 경계

        [Test]
        public void InvalidChallenge_MakesWholeGameInvalid()
        {
            Assert.That(Game(new ChoiceChallenge("bad", new[] { 5 }, new[] { "p" }, new[] { "a", "b" })).IsValid, Is.False);
            Assert.That(Game(new TimingChallenge("late", new[] { 2.9f }, .15f, 3f)).IsValid, Is.False);
            Assert.That(Game(new SequenceChallenge("reveal", new[] { 0 }, new[] { "a", "b" }, revealSeconds: 30f)).IsValid, Is.False);
            Assert.That(Game(Sequence()).IsValid, Is.True);
        }

        [Test]
        public void SuccessScore_IsClampedAndDecidesSuccess()
        {
            var strict = new Minigame("strict", "strict", new IChallenge[] { new ChoiceChallenge("c", new[] { 0, 1 }, new[] { "p0", "p1" }, new[] { "a", "b" }) }, null, successScore: 500);
            Assert.That(strict.SuccessScore, Is.EqualTo(100));
            var s = new MinigameSession(strict, null, 1);
            s.Choose(0); s.Choose(0);
            Assert.That(s.Score, Is.EqualTo(50));
            Assert.That(s.Succeeded, Is.False);
        }

        [Test]
        public void Placement_TimeoutScoreIsCappedBelowPerfect()
        {
            var s = Session(Placement());
            s.Place(0, new GridPoint(0, 0), 0);
            s.AdvanceTime(31f);
            Assert.That(s.IsComplete, Is.True);
            Assert.That(s.Score, Is.EqualTo(50));
        }
    }
}
