using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Host.World;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 집에서 NPC와 물건 앞까지 방향 키로 걸어가 대상이 잡히는지, 상호작용 키를 누르면 무엇과 상호작용했는지 Core 이벤트가 나오는지
    /// 실제 창과 게임 루프로 본다. 장치 이벤트 주입, OS 입력 계층 미확인.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class InteractionScreenTests
    {
        private PlayerDriver _player;
        private string _repo;

        [OneTimeSetUp]
        public void StartGame()
        {
            // 오전(9시): 첫 안내 NPC의 일정은 집(location.home). 배속 0이면 시간이 멈춰 일정이 바뀌지 않는다.
            HostOptions options = HostOptions.Resolve(new[] { "--world", "House", "--hour", "9", "--time-scale", "0", "--size", "1280x720" });
            options.TestMode = true;
            _repo = options.RepoRoot;
            _player = new PlayerDriver(options);
            _player.Frames(3);
        }

        [OneTimeTearDown]
        public void StopGame()
        {
            _player?.Dispose();
            _player = null;
        }

        private WorldScreen Screen => _player.Game.World;

        [Test, Order(1)]
        public void INTERACT_PLAY_001_FirstGuideStandsAtHomeAndNothingIsTargetedFromTheDoor()
        {
            Assert.That(Screen.Npcs.Select(n => n.NpcId), Is.EqualTo(new[] { "npc.first-guide" }));
            Assert.That(Screen.Interactions.Target == null || !Screen.Interactions.Target.Acts, Is.True, "도착 지점에서는 상호작용할 것이 없다");
            _player.PressKey(TestKey.E);
            Assert.That(Screen.InteractionLog, Is.Empty);
        }

        [Test, Order(2)]
        public void INTERACT_PLAY_002_WalkingUpToTheNpcTargetsItAndEFiresItsEvent()
        {
            NpcOnMap guide = Screen.Npcs.Single();
            _player.WalkNear(guide.Pose.X, guide.Pose.Y + 32f, 20f); // NPC 바로 아래 칸
            InteractionCandidate target = Screen.Interactions.Target;
            Assert.That(target, Is.Not.Null);
            Assert.That((target.Kind, target.Id), Is.EqualTo(("npc", "npc.first-guide")));
            _player.Screenshot("interact-01-npc-targeted");
            _player.PressKey(TestKey.E);
            Assert.That(Screen.InteractionLog.Count, Is.EqualTo(1));
            Assert.That(Screen.InteractionLog[0], Is.EqualTo(new InteractionEvent("House", "npc", "npc.first-guide")));
        }

        [Test, Order(3)]
        public void INTERACT_PLAY_003_PlanBoardIsTargetedAndSpaceAlsoInteracts()
        {
            InteractionThing board = InteractionData.Load(System.IO.Path.Combine(_repo, InteractionData.DefaultRelativePath)).Map("House").Things.First(t => t.Kind == "daily-plan-board");
            _player.WalkNear(board.X, board.Y, 24f);
            Assert.That(Screen.Interactions.Target?.Kind, Is.EqualTo("daily-plan-board"));
            _player.Screenshot("interact-02-board-targeted");
            _player.PressKey(TestKey.Space);
            Assert.That(Screen.InteractionLog.Last(), Is.EqualTo(new InteractionEvent("House", "daily-plan-board", "house.daily-plan-board")));
            Assert.That(_player.CollisionViolations, Is.EqualTo(0));
        }
    }
}
