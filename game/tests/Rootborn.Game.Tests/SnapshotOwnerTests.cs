using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Core.Quests;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 플레이어 스냅샷(adult-career-snapshot*.json)을 읽을 때의 보호. 자동 테스트다.
    /// 옮겨 옴: Assets/Tests/EditMode/Careers/AdultCareerSnapshotTests.cs(002·004·005·012),
    /// Careers/PlayerLifeAggregateTests.cs(002·003·008), StudentLife/DailyPlanSchedulePersistenceTests.cs(WrongEmbeddedIdentity).
    /// Unity 판은 스냅샷 안의 플레이어·슬롯 id가 하나라도 다르거나 모르는 아이템이 있으면 복원 전체를 거절했다(복구 필요).
    /// MonoGame 판은 복사한 슬롯을 지금 슬롯으로 읽기로 했으므로(OlderSlotTests) 슬롯 이름은 맞추고,
    /// 다른 플레이어의 id와 모르는 아이템은 손상으로 보고 시작을 거절한다. 거절하면 슬롯의 어떤 파일도 바꾸지 않는다.
    /// </summary>
    public sealed class SnapshotOwnerTests
    {
        private const string Other = "another-player";

        private static TempSaves Played()
        {
            var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.VisitLocation("location.town-square");
            return saves;
        }

        private static SortedDictionary<string, byte[]> Files(TempSaves saves) => SlotCheckpoints.Read(saves, TestWorld.Slot);

        public static IEnumerable<TestCaseData> ForeignOwners()
        {
            yield return new TestCaseData("student").SetName("ForeignPlayer_Student");
            yield return new TestCaseData("adult").SetName("ForeignPlayer_Adult");
            yield return new TestCaseData("world").SetName("ForeignPlayer_World");
            yield return new TestCaseData("chain").SetName("ForeignPlayer_QuestChain");
            yield return new TestCaseData("furniture").SetName("ForeignPlayer_FurnitureUnlocks");
            yield return new TestCaseData("daily-plan").SetName("ForeignPlayer_DailyPlan");
            yield return new TestCaseData("candidates").SetName("ForeignPlayer_CareerCandidates");
        }

        private static void MakeForeign(AdultCareerSnapshotSaveData snapshot, string part)
        {
            switch (part)
            {
                case "student": snapshot.Student.PlayerId = Other; break;
                case "adult": snapshot.Adult.PlayerId = Other; break;
                case "world": snapshot.World.PlayerId = Other; break;
                case "chain":
                    Assert.That(snapshot.QuestChains.Chains, Is.Not.Empty, "체인 기록이 있어야 시험이 된다");
                    snapshot.QuestChains.Chains[0].PlayerId = Other;
                    break;
                case "furniture": snapshot.FurnitureUnlocks.PlayerId = Other; break;
                case "daily-plan":
                    snapshot.Student.DailyPlan ??= new DailyPlanProgressSaveData();
                    snapshot.Student.DailyPlan.PlayerId = Other;
                    snapshot.Student.DailyPlan.CurrentDay = snapshot.Student.CurrentDay;
                    snapshot.Student.DailyPlan.AppliedCompletionKeys = new[] { "foreign-reward" };
                    break;
                default:
                    snapshot.Student.CareerCandidates ??= new CareerCandidateProgressSaveData();
                    snapshot.Student.CareerCandidates.PlayerId = Other;
                    snapshot.Student.CareerCandidates.Candidates = new[] { new CareerCandidateProgressSaveData.CandidateEntry { CandidateId = "candidate.chef", UnderstandingScore = 3 } };
                    break;
            }
        }

        [TestCaseSource(nameof(ForeignOwners))]
        public void ASnapshotCarryingAnotherPlayersRecord_RefusesToStart_AndChangesNothing(string part)
        {
            using var saves = Played();
            SaveEdits.EditSnapshot(saves.Store, s => MakeForeign(s, part));
            var before = Files(saves);

            var error = Assert.Throws<SaveSlotDamagedException>(() => TestWorld.Start(saves.Store));

            Assert.That(error.Problems.Select(p => p.FileName), Does.Contain(SaveFileNames.AdultCareerSnapshot(TestWorld.Player)));
            Assert.That(SlotCheckpoints.Same(before, Files(saves)), Is.True, "거절한 시작이 슬롯의 파일을 바꾸지 않는다");
        }

        [Test]
        public void ASnapshotWithAnotherSlotName_IsACopiedSlot_AndContinuesAsThisSlot()
        {
            using var saves = Played();
            SaveEdits.EditSnapshot(saves.Store, s =>
            {
                s.Student.SaveSlot = "slot-elsewhere";
                s.Adult.SaveSlot = "slot-elsewhere";
                s.World.SaveSlot = "slot-elsewhere";
                foreach (var chain in s.QuestChains.Chains) chain.SaveSlot = "slot-elsewhere";
                s.FurnitureUnlocks.SaveSlot = "slot-elsewhere";
            });

            var game = TestWorld.Start(saves.Store);

            Assert.That(game.Student.SaveSlot, Is.EqualTo(TestWorld.Slot));
            Assert.That(game.Student.PlayerId, Is.EqualTo(TestWorld.Player));
        }

        [Test]
        public void AnUnknownItemInTheSnapshot_RefusesToStart_RatherThanDroppingIt()
        {
            using var saves = Played();
            SaveEdits.EditSnapshot(saves.Store, s => SaveEdits.Fill(s, "item.not-in-data", 3));
            var before = Files(saves);

            var error = Assert.Throws<SaveSlotDamagedException>(() => TestWorld.Start(saves.Store));

            Assert.That(error.ToString(), Does.Contain("item.not-in-data"), "어느 아이템인지 알린다");
            Assert.That(SlotCheckpoints.Same(before, Files(saves)), Is.True, "다음 저장이 아이템을 지우지 않게 아무것도 쓰지 않는다");
        }

        [Test]
        public void KnownItems_StillLoad()
        {
            using var saves = Played();
            SaveEdits.EditSnapshot(saves.Store, s => SaveEdits.Fill(s, "item.allowance", 5));
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.Inventory.CountOf(game.Item("item.allowance")), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TheEquippedToolSelection_StaysInTheSameSnapshotAsTheInventory()
        {
            using var saves = Played();
            SaveEdits.EditSnapshot(saves.Store, s =>
            {
                SaveEdits.Fill(s, "item.allowance", 1);
                s.Inventory.EquippedToolItemId = "item.allowance";
            });

            var game = TestWorld.Start(saves.Store);
            game.VisitLocation("location.town-square");

            Assert.That(saves.Store.TryRead<AdultCareerSnapshotSaveData>(TestWorld.Slot, SaveFileNames.AdultCareerSnapshot(TestWorld.Player), out var saved), Is.True);
            Assert.That(saved.Inventory.EquippedToolItemId, Is.EqualTo("item.allowance"), "다음 저장이 장비 선택을 지우지 않는다");
        }

        [Test]
        public void AnEquippedToolThatIsNotInData_RefusesToStart()
        {
            using var saves = Played();
            SaveEdits.EditSnapshot(saves.Store, s => s.Inventory.EquippedToolItemId = "item.not-in-data");
            var before = Files(saves);

            Assert.Throws<SaveSlotDamagedException>(() => TestWorld.Start(saves.Store));
            Assert.That(SlotCheckpoints.Same(before, Files(saves)), Is.True);
        }

        [Test]
        public void AVersion1SnapshotWithoutChainsOrFurniture_ReadsThemAsEmptyForThisPlayer()
        {
            using var saves = Played();
            SaveEdits.EditSnapshot(saves.Store, s =>
            {
                s.Version = 1;
                s.QuestChains = null;
                s.FurnitureUnlocks = null;
            });

            var game = TestWorld.Start(saves.Store);
            Assert.That(game.FurnitureUnlocks.PlayerId, Is.EqualTo(TestWorld.Player));
            game.VisitLocation("location.town-square");

            Assert.That(saves.Store.TryRead<AdultCareerSnapshotSaveData>(TestWorld.Slot, SaveFileNames.AdultCareerSnapshot(TestWorld.Player), out var saved), Is.True);
            Assert.That(saved.FurnitureUnlocks.PlayerId, Is.EqualTo(TestWorld.Player));
            Assert.That(saved.FurnitureUnlocks.UnlockTokens, Is.Empty);
            Assert.That(saved.QuestChains.Chains.All(c => c.PlayerId == TestWorld.Player), Is.True);
        }

        [Test]
        public void EmptyOwnerFields_FromAnEngineShell_AreReadAsThisPlayer()
        {
            // Unity JsonUtility는 빠진 안쪽 객체를 빈 껍데기로 채운다. 빈 id는 다른 플레이어가 아니다.
            using var saves = Played();
            SaveEdits.EditSnapshot(saves.Store, s =>
            {
                s.Student.CareerCandidates = new CareerCandidateProgressSaveData();
                s.Student.DailyPlan = new DailyPlanProgressSaveData();
                s.FurnitureUnlocks = new FurnitureUnlockProgressSaveData();
            });

            var game = TestWorld.Start(saves.Store);
            Assert.That(game.FurnitureUnlocks.PlayerId, Is.EqualTo(TestWorld.Player));
            Assert.That(game.CareerCandidates.PlayerId, Is.EqualTo(TestWorld.Player));
        }
    }
}
