using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.World;
using Rootborn.Data.Tests;
using Rootborn.World;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 게임 한 판이 실제로 쓴 세이브 파일은 모두 등록표(Data 테스트의 SaveShapeCoverage)에 모양이 있어야 한다.
    /// 등록표의 손으로 적은 목록(SaveShapeRegistrationTests.DefaultPlayerFiles)과 달리, 새 세이브 파일을 더하고 등록을 잊으면 여기서 실패한다.
    /// 등록된 파일은 옛 세이브 고정 파일 테스트(읽힌다, 버리는 필드가 없다, 읽고 다시 써도 같다)를 받는다. 자동 테스트다.
    /// </summary>
    public sealed class SavedFilesAreRegisteredTests
    {
        public static IEnumerable<TestCaseData> WrittenFiles()
        {
            foreach (string checkpoint in SlotCheckpoints.Names)
                foreach (string file in SlotCheckpoints.FileNames(checkpoint))
                    yield return new TestCaseData(checkpoint, file).SetName("Registered_" + checkpoint + "_" + file);
        }

        [TestCaseSource(nameof(WrittenFiles))]
        public void EveryFileAGameWrites_HasARegisteredShape(string checkpoint, string file)
        {
            Assert.That(SaveShapeCoverage.ShapeFor(file), Is.Not.Null,
                file + ": 게임이 쓰는 세이브 파일인데 SaveShapeCoverage.Shapes에 모양이 없다(game/tests/Rootborn.Data.Tests/SaveShapeCoverage.cs)");
        }

        [Test]
        public void TheCheckpointsCoverTheFilesEveryCommandWrites()
        {
            var all = SlotCheckpoints.Names.SelectMany(SlotCheckpoints.FileNames).Distinct().ToArray();
            Assert.That(all, Does.Contain(SaveFileNames.AdultCareerSnapshot(TestWorld.Player)));
            Assert.That(all, Does.Contain(SaveFileNames.PlayerQuestLog(TestWorld.Player)));
            Assert.That(all, Does.Contain(SaveFileNames.HouseFurnitureLayout(0)), "가구를 놓은 지점이 있어야 배치 파일도 본다");
            Assert.That(all, Does.Contain(SaveFileNames.WorkspaceProgress(TestWorld.Player)));
            Assert.That(all, Does.Contain(SaveFileNames.HouseBuild), "꾸밈을 바꾼 지점이 있어야 꾸밈 파일도 본다");
            Assert.That(all, Does.Contain(SaveFileNames.WorkspaceLayout(SlotCheckpoints.ChefSurface, TestWorld.Player)), "일터에 들어간 지점이 있어야 일터 배치 파일도 본다");
        }

        /// <summary>
        /// 지금 프로그램이 쓴 슬롯을 고정 파일로 내보낸다. 평소에는 돌지 않는다. 세이브 형식을 바꾼 뒤(Version을 올린 뒤)에만 돌려
        /// 새 날짜 폴더를 더한다 — 이미 있는 폴더는 고치지 않는다(그 폴더가 "옛 프로그램이 쓴 세이브"로 남아야 한다).
        /// 쓰는 법: ROOTBORN_FIXTURE_OUT=&lt;새 폴더&gt; dotnet test game/tests/Rootborn.Game.Tests --filter Name~WriteCurrentSlotFixture
        /// </summary>
        [Test, Explicit("고정 파일을 새로 만들 때만 손으로 돌린다")]
        public void WriteCurrentSlotFixture()
        {
            string target = Environment.GetEnvironmentVariable("ROOTBORN_FIXTURE_OUT");
            Assert.That(target, Is.Not.Null.And.Not.Empty, "ROOTBORN_FIXTURE_OUT에 새 폴더를 준다");
            string slotFolder = Path.Combine(target, SlotCheckpoints.Slot);
            Assert.That(Directory.Exists(slotFolder), Is.False, "이미 있는 고정 폴더는 고치지 않는다: " + slotFolder);

            // 슬롯 정보와 지금 있는 자리는 월드가 쓴다(실행 프로그램과 같은 순서: 슬롯 시작 → 도착 기록 → 게임 한 판).
            using var saves = new TempSaves();
            var catalog = WorldTravelData.From(TestWorld.Definitions);
            var slot = new WorldSlot(saves.Store, SlotCheckpoints.Slot, catalog);
            WorldSlotStart start = slot.Begin(new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc).Ticks);
            Assert.That(start.Ok, Is.True, start.Problem);
            Assert.That(slot.Record(start.Entry.SceneName, start.Entry.SpawnId, string.Empty), Is.True, slot.LastProblem);
            SlotCheckpoints.WriteAll(saves, SlotCheckpoints.Files(SlotCheckpoints.Latest));

            Directory.CreateDirectory(slotFolder);
            foreach (var pair in SlotCheckpoints.Read(saves)) File.WriteAllBytes(Path.Combine(slotFolder, pair.Key), pair.Value);
        }
    }
}
