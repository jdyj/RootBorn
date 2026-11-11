using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Screens;
using Rootborn.Screens.Dialogue;
using Rootborn.Screens.Discoveries;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 발견 지점: 실제 맵 데이터(game/world/interactions.json)의 발견 지점(마을 북쪽 광장의 임시 지점)에 새 게임에서 걸어가 E를 누르면
    /// 알림 층에 발견 알림이 뜨고 결과(성장)가 적용된다. 같은 슬롯으로 다시 띄워도 발견은 남고, 다시 살펴보면 "이미 살펴봄"이다.
    /// 방향 키·E만 쓴다(장치 이벤트 주입, OS 입력 계층 미확인). 스크린샷: output/host-screens-evidence/discovery-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class DiscoveryHostTests
    {
        private const string Slot = "slot-1";
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-discovery-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Launch()
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", Slot, "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "host-screens-evidence"),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private static void CloseDailyEventIfOpen(PlayerDriver player)
        {
            player.Frames(2);
            if (!player.Screens.IsOpen(ScreenCatalog.DailyEvent)) return;
            player.PressButton(DailyEventScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.DailyEvent);
        }

        /// <summary>지금 맵의 발견 지점(실제 데이터에서 찾는다)까지 걸어가 그것이 E의 대상인지 본다.</summary>
        private static InteractionThing WalkToDiscovery(PlayerDriver player)
        {
            InteractionThing thing = player.Game.World.HereData.Things.FirstOrDefault(t => t.Kind == DiscoveryNoticeScreen.InteractionKind);
            Assert.That(thing, Is.Not.Null, player.World.Map.Id + "에 발견 지점이 없다");
            player.WalkNear(thing.X, thing.Y, player.World.Map.Grid.CellSize * 0.5f); // 지점 칸에 선다(다른 지점은 상호작용 범위 밖 — World TemporaryPointsTests)
            var target = player.Game.World.Interactions.Target;
            Assert.That((target?.Kind, target?.Id), Is.EqualTo((thing.Kind, thing.Id)), "걸어간 자리에서 E의 대상");
            return thing;
        }

        [Test]
        public void DISCOVERY_001_WalkToTheTownDiscoveryPoint_E_ShowsTheNotice_AndItStaysAfterReloading()
        {
            InteractionThing point;
            Discovery discovery;
            DiscoveryTraitDeltaOutcome trait;
            int before;
            using (PlayerDriver player = Launch())
            {
                Assert.That(player.World.Map.Id, Is.EqualTo("House"), "새 게임은 집에서 시작한다");
                Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
                CloseDailyEventIfOpen(player);
                point = WalkToDiscovery(player);
                Assert.That(player.Game.Session.Definitions.TryGet(point.Id, out discovery), Is.True, "지점 id가 data/의 발견 정의다");
                trait = discovery.Outcomes.OfType<DiscoveryTraitDeltaOutcome>().First();
                before = player.Game.Session.Student.GetTraitValue(trait.TraitId);
                Assert.That(player.Game.Screens.Find(ScreenCatalog.HudPrompt) is Rootborn.Screens.Hud.InteractionPromptScreen prompt
                    && prompt.Text.Contains(player.Game.Text.Get(discovery.DisplayNameKey)), Is.True, "안내 글자에 발견 이름");

                player.PressKey(TestKey.E);
                player.WaitForScreen(ScreenCatalog.DiscoveryNotice);
                var notice = (DiscoveryNoticeScreen)player.Screens.Find(ScreenCatalog.DiscoveryNotice);
                Assert.That(notice.Model.Applied, Is.True);
                Assert.That(player.Game.Session.Student.GetTraitValue(trait.TraitId), Is.EqualTo(before + trait.Delta));
                Assert.That(((UiLabel)player.Node(DiscoveryNoticeScreen.MessageId)).Text, Is.EqualTo(player.Game.Text.Get("ui.discovery.found")));
                Assert.That(player.Screens.MovementBlocked, Is.False, "알림이 떠 있어도 걷는다");
                player.Frames(2);
                player.Screenshot("discovery-01-found");

                // 알림은 시간이 지나면 저절로 닫힌다(고정 프레임 간격).
                player.WaitForScreenClosed(ScreenCatalog.DiscoveryNotice, (int)(notice.RemainingSeconds * 60f) + 30);
                Assert.That(player.CollisionViolations, Is.EqualTo(0));
                Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, notice.TextKeys);
                Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
            }

            // 같은 슬롯으로 다시 띄운다: 발견은 저장돼 있고, 다시 살펴보면 성장은 그대로다.
            using (PlayerDriver player = Launch())
            {
                Assert.That(player.Game.Session.Student.HasAppliedRequest(DiscoveryRunner.RequestIdFor(discovery)), Is.True, "다시 띄워도 발견이 남는다");
                Assert.That(player.Game.Session.Student.GetTraitValue(trait.TraitId), Is.EqualTo(before + trait.Delta));
                if (player.World.Map.Id != "Town") Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
                CloseDailyEventIfOpen(player);
                WalkToDiscovery(player);
                player.PressKey(TestKey.E);
                player.WaitForScreen(ScreenCatalog.DiscoveryNotice);
                var notice = (DiscoveryNoticeScreen)player.Screens.Find(ScreenCatalog.DiscoveryNotice);
                Assert.That(notice.Model.MessageKey, Is.EqualTo("ui.discovery.already"));
                Assert.That(player.Game.Session.Student.GetTraitValue(trait.TraitId), Is.EqualTo(before + trait.Delta), "두 번 주지 않는다");
                player.Frames(2);
                player.Screenshot("discovery-02-already");
            }
        }
    }
}
