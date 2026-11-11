using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Data.Save;
using Rootborn.Screens;
using Rootborn.Screens.Start;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 세이브 안전의 Host 쪽: 손상된 슬롯은 시작을 거절하고 슬롯 선택 위에 알림(돌아가기만)을 띄운다(슬롯을 골랐을 때와 --slot으로 띄웠을 때),
    /// --day-copies / ROOTBORN_DAY_COPIES는 GameSession.KeepDayCopies로 간다. 실제 창과 게임 루프, 포인터 이벤트만 쓴다
    /// (장치 이벤트 주입, OS 입력 계층 미확인). 세이브 폴더는 임시 폴더. 스크린샷: output/host-screens-evidence/save-damaged-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class SaveSafetyHostTests
    {
        private const string Slot = "slot-0";
        private string _saves;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-save-safety-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Launch(params string[] extra)
        {
            var args = new List<string> { "--save-root", _saves, "--size", "1920x1080" };
            args.AddRange(extra);
            HostOptions options = HostOptions.Resolve(args.ToArray());
            options.TestMode = true;
            options.EvidenceDir = Path.Combine(options.RepoRoot, "output", "host-screens-evidence");
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private string SlotFolder => Path.Combine(_saves, Slot);

        /// <summary>
        /// 슬롯 하나를 실제로 만들고(--slot으로 새 게임을 띄우면 슬롯 정보와 월드 자리를 쓴다) 이 플레이어의 캠페인 파일을 깨진 JSON으로 써서
        /// 손상시킨다. 그 파일 이름을 돌려준다. GameSession은 "읽을 수 없는 파일"로 알린다.
        /// </summary>
        private string MakeDamagedSlot()
        {
            using (PlayerDriver player = Launch("--slot", Slot)) Assert.That(player.Game.Session, Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(SlotFolder, SaveFileNames.Metadata)), Is.True, "슬롯 선택에 보이려면 슬롯 정보가 있어야 한다");
            string campaign = SaveFileNames.Campaign(SaveFileNames.DefaultPlayerId);
            File.WriteAllText(Path.Combine(SlotFolder, campaign), "{ \"Records\": ");
            return campaign;
        }

        private Dictionary<string, byte[]> SlotFiles() => Directory.GetFiles(SlotFolder, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(SlotFolder, f), File.ReadAllBytes);

        private static void AssertSameFiles(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
        {
            Assert.That(after.Keys.OrderBy(k => k), Is.EqualTo(before.Keys.OrderBy(k => k)), "거절한 시작은 파일을 더하거나 지우지 않는다");
            foreach (var pair in before) Assert.That(after[pair.Key], Is.EqualTo(pair.Value), "거절한 시작은 파일을 바꾸지 않는다: " + pair.Key);
        }

        [Test]
        public void SAVE_DAMAGE_001_ContinueADamagedSlot_ShowsTheNotice_BackReturnsToTheSlots_FilesUntouched()
        {
            string snapshot = MakeDamagedSlot();
            var before = SlotFiles();
            using PlayerDriver player = Launch();
            Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);

            player.PressButton(SaveSlotScreen.ContinueId(0));
            player.WaitForScreen(ScreenCatalog.SaveSlotDamaged);
            Assert.That(player.Game.World, Is.Null, "손상된 슬롯으로 월드를 띄우지 않는다");
            Assert.That(player.Game.SlotDamage?.Problems.Select(p => p.FileName), Does.Contain(snapshot));
            var notice = (SaveSlotDamagedScreen)player.Screens.Find(ScreenCatalog.SaveSlotDamaged);
            Assert.That(notice.Model.Problems.Select(p => (p.Key, p.FileName)), Does.Contain(("ui.save.damaged.unreadable", snapshot)));
            player.MovePointer(-50, -50);
            player.Frames(2);
            Assert.That(player.Node(SaveSlotScreen.ErrorId).Visible, Is.False, "알림 아래로 일반 실패 문구가 비치지 않는다");
            player.ScreenshotScreen(ScreenCatalog.SaveSlotDamaged, "save-damaged-01-from-slot-select");

            player.PressButton(SaveSlotDamagedScreen.BackId);
            player.WaitForScreenClosed(ScreenCatalog.SaveSlotDamaged);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True, "돌아가기는 슬롯 선택으로");
            Assert.That(player.Node(SaveSlotScreen.ErrorId).Visible, Is.False, "알림이 문제를 말했으므로 일반 실패 문구는 남기지 않는다");
            player.Frames(2);
            AssertSameFiles(before, SlotFiles());
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, notice.TextKeys);
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }

        [Test]
        public void SAVE_DAMAGE_002_LaunchWithADamagedSlotArgument_ShowsTheSlotsWithTheNotice_InsteadOfCrashing()
        {
            string snapshot = MakeDamagedSlot();
            var before = SlotFiles();
            using PlayerDriver player = Launch("--slot", Slot);
            Assert.That(player.Game.World, Is.Null);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True, "슬롯 선택이 아래에 뜬다");
            Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotDamaged), Is.True);
            Assert.That(player.Game.SlotDamage?.Problems.Select(p => p.FileName), Does.Contain(snapshot));
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.SaveSlotDamaged, "save-damaged-02-slot-argument");

            player.PressButton(SaveSlotDamagedScreen.BackId);
            player.WaitForScreenClosed(ScreenCatalog.SaveSlotDamaged);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
            AssertSameFiles(before, SlotFiles());
        }

        [Test]
        public void DAY_COPIES_001_ArgumentAndEnvironment_ReachTheSession()
        {
            using (PlayerDriver player = Launch("--slot", Slot, "--day-copies", "1"))
                Assert.That(player.Game.Session.DayCopiesToKeep, Is.EqualTo(1));
            using (PlayerDriver player = Launch("--slot", Slot))
                Assert.That(player.Game.Session.DayCopiesToKeep, Is.EqualTo(Rootborn.Game.GameSession.DefaultDayCopiesToKeep), "주지 않으면 기본값");
        }

        [Test]
        public void DAY_COPIES_002_Options_ArgumentBeatsEnvironment_ZeroTurnsOff_BadValuesStop()
        {
            string Env(string name, string value) => name == HostOptions.DayCopiesVariable ? value : Environment.GetEnvironmentVariable(name);
            Assert.That(HostOptions.Resolve(new[] { "--sample" }, n => Env(n, null)).DayCopies, Is.Null);
            Assert.That(HostOptions.Resolve(new[] { "--sample" }, n => Env(n, "5")).DayCopies, Is.EqualTo(5));
            Assert.That(HostOptions.Resolve(new[] { "--sample", "--day-copies", "0" }, n => Env(n, "5")).DayCopies, Is.EqualTo(0));
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--sample", "--day-copies", "-1" }, n => Env(n, null)));
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--sample", "--day-copies", "many" }, n => Env(n, null)));
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--sample" }, n => Env(n, "x")));
        }
    }
}
