using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Quests;
using Rootborn.Core.World;
using Rootborn.Screens;
using Rootborn.Simulation.Headless;

namespace Rootborn.Simulation.Tests
{
    /// <summary>자동 테스트: 관찰(할 수 있는 것 모으기)과 불변식 도우미가 스스로 맞는지.</summary>
    [TestFixture, NonParallelizable]
    public sealed class ObserverAndChecksTests
    {
        private static (HeadlessBody Body, PlayObserver Observer) NewGame()
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            var body = new HeadlessBody(atlas);
            var observer = new PlayObserver(atlas);
            var memory = new PlayMemory();
            for (int i = 0; i < 5 && !body.InWorld; i++)
            {
                PlayAction a = StartRoutine.Choose(observer.Observe(body), memory);
                if (a.Kind == PlayActionKind.Type) body.TypeInto(a.X, a.Y, a.Text); else body.Click(a.X, a.Y);
            }
            Assert.That(body.InWorld, Is.True, "시작 순서로 월드에 들어갔다");
            return (body, observer);
        }

        [Test]
        public void StartScreen_OffersOnlyTheSlotScreen_AndNoWorldOrKeys()
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            using var body = new HeadlessBody(atlas);
            PlayView view = new PlayObserver(atlas).Observe(body);
            Assert.That(view.InWorld, Is.False);
            Assert.That(view.Actions.Select(a => a.Key), Does.Contain("button:" + ScreenCatalog.SaveSlotSelect + "/save-slots.card.0.new"));
            Assert.That(view.Actions.All(a => a.Screen == ScreenCatalog.SaveSlotSelect), Is.True, "시작 화면 밖의 것은 없다: " + string.Join(", ", view.Actions.Select(a => a.Key)));
        }

        [Test]
        public void InTheHouse_OffersWalkableThingsDoorsKeysAndHudButtons()
        {
            var (body, observer) = NewGame();
            using (body)
            {
                PlayView view = observer.Observe(body);
                Assert.That(view.Actions.Any(a => a.Kind == PlayActionKind.WalkInteract && a.InteractionKind == "npc"), Is.True, "집의 NPC에게 걸어갈 수 있다");
                Assert.That(view.Actions.Any(a => a.Kind == PlayActionKind.Door), Is.True, "문으로 나갈 수 있다");
                Assert.That(view.Actions.Any(a => a.Key == "key:" + ScreenKey.Tab), Is.True);
                Assert.That(view.Actions.Any(a => a.Element != null && a.Element.StartsWith("hud.button.")), Is.True, "HUD 단추(포커스를 받지 않는 단추도)");
                Assert.That(view.Actions.Select(a => a.Key).Distinct().Count(), Is.EqualTo(view.Actions.Count), "행동 이름은 겹치지 않는다");
            }
        }

        [Test]
        public void WhileAModalIsOpen_NothingFromLowerLayersIsOffered()
        {
            var (body, observer) = NewGame();
            using (body)
            {
                PlayView view = observer.Observe(body);
                PlayAction plan = view.Actions.First(a => a.Kind == PlayActionKind.WalkInteract && a.InteractionKind == "npc");
                Assert.That(body.WalkNear(plan.X, plan.Y, body.Walk.Map.Grid.CellSize * 1.25f), Is.True);
                body.Interact();
                Assert.That(body.Screens.IsOpen(ScreenCatalog.Dialogue), Is.True, "대화(모달)가 열렸다");
                view = observer.Observe(body);
                foreach (PlayAction a in view.Actions.Where(a => a.Screen != null))
                    Assert.That(body.Screens.Find(a.Screen).Layer, Is.GreaterThanOrEqualTo(ScreenLayer.Modal), "모달 아래층 요소: " + a.Key);
                Assert.That(view.Actions.Any(a => a.Screen == ScreenCatalog.Dialogue), Is.True, "모달의 단추는 있다");
            }
        }

        [Test]
        public void Coverage_CountsTheScreenThatWasOpenBeforeThePlayStarted()
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            using var body = new HeadlessBody(atlas);
            var runner = new PlayRunner(body, new RandomPolicy(), 1, new PlaySettings { MaxSteps = 1 }, atlas, "coverage-start");
            Assert.That(runner.Result.ScreensOpened, Does.Contain(ScreenCatalog.SaveSlotSelect));
        }

        [Test]
        public void IdleFrame_DoesNotAllocate()
        {
            var (body, _) = NewGame();
            using (body)
            {
                double bytes = body.AllocatedBytesPerIdleFrame(600);
                TestContext.WriteLine("가만히 선 프레임 한 번에 새로 잡는 바이트: " + bytes.ToString("0.0") + ", 분이 바뀐 프레임 최대 " + body.MaxBytesInMinuteFrame
                    + "(" + body.MinuteFrames + "번), NPC 걸음이 끝난 프레임 최대 " + body.MaxBytesInNpcStepFrame);
                Assert.That(body.MeasuredIdleFrames, Is.GreaterThan(300), "잰 프레임이 전체(600)의 절반을 넘어야 한다 — 아무것도 재지 않고 통과하지 않게");
                Assert.That(bytes, Is.LessThanOrEqualTo(PlayLimits.IdleFrameBytes), "매 프레임 쓰레기(coding-standards 성능)");
                Assert.That(body.MinuteFrames, Is.GreaterThan(0), "흐르는 시계: 600프레임(10초)이면 게임 24분이 간다");
                Assert.That(body.MaxBytesInMinuteFrame, Is.LessThanOrEqualTo(PlayLimits.MinuteFrameBytes), "분마다 HUD가 글자를 새로 만드는 비용(매 프레임이 아니다)");
                Assert.That(body.MaxBytesInNpcStepFrame, Is.LessThanOrEqualTo(PlayLimits.NpcStepFrameBytes), "NPC 어슬렁 걸음이 끝날 때 다음 길을 만드는 비용(걸음마다, 매 프레임이 아니다)");
            }
        }

        [Test]
        public void DoorDirection_GoesToTheNearestEdgeOfAWideDoor()
        {
            var wide = new AreaRect(496f, 2012.8f, 1408f, 44.8f); // 맵 아래쪽을 따라 긴 문(해변)
            Assert.That(DoorDirection.Toward(wide, 1584f, 1968f), Is.EqualTo((0, 1)), "옆이 아니라 아래로");
            var tall = new AreaRect(576f, 416f, 32f, 64f);
            Assert.That(DoorDirection.Toward(tall, 500f, 440f), Is.EqualTo((1, 0)));
        }

        [Test]
        public void ObjectiveIds_ReadTargetsAndAcceptedIds_IncludingAlternatives()
        {
            var ids = new HashSet<string>();
            ObjectiveIds.Collect(new TalkQuestObjective("k", 1, "npc.a"), ids);
            ObjectiveIds.Collect(new PartTimeWorkQuestObjective("k", 1, new[] { "work.a", "work.b" }), ids);
            ObjectiveIds.Collect(new AnyOfQuestObjective("k", 1, new QuestObjective[] { new LocationVisitQuestObjective("k", 1, "location.a"), new TalkQuestObjective("k", 1, "npc.b") }), ids);
            Assert.That(ids, Is.EquivalentTo(new[] { "npc.a", "work.a", "work.b", "location.a", "npc.b" }));
        }

        [Test]
        public void Snapshot_DiffAndDuplicates()
        {
            Assert.That(StateSnapshot.Diff(new[] { "a", "b" }, new[] { "a", "b" }), Is.Empty);
            Assert.That(StateSnapshot.Diff(new[] { "a", "b" }, new[] { "a", "c" }), Is.EqualTo(new[] { "- b", "+ c" }));
            Assert.That(StateSnapshot.Duplicates(new[] { "chain:x:r1", "quest:q", "chain:x:r1" }), Is.EqualTo(new[] { "chain:x:r1" }));
            Assert.That(StateSnapshot.Duplicates(new[] { "a", "b" }), Is.Empty);
        }

        [Test]
        public void EveryPolicyIsRegistered_AndNamesAreTheLogNames()
        {
            PlayPolicyCatalog catalog = PlayPolicyCatalog.Default();
            Assert.That(catalog.Names, Is.EquivalentTo(new[] { PlayPolicyCatalog.Random, PlayPolicyCatalog.MainChain, PlayPolicyCatalog.OffSchool, PlayPolicyCatalog.Novelty, PlayPolicyCatalog.Wander, PlayPolicyCatalog.StayUp }));
            foreach (string name in catalog.Names) Assert.That(catalog.Create(name, FastPlayTests.Atlas).Name, Is.EqualTo(name));
        }
    }
}
