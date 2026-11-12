using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Interaction;
using Rootborn.Core.StudentLife;
using Rootborn.Data;
using Rootborn.Data.Builders;

namespace Rootborn.World.Tests
{
    /// <summary>자동 테스트: 실제 상호작용 데이터, NPC 그림, NPC 일정으로 집에 설 NPC와 후보를 만든다. 화면 근거가 아니다.</summary>
    public sealed class InteractionDataTests
    {
        private static InteractionData Data => InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));

        [Test]
        public void HouseHasThePlanBoardDayEndAndTheHomeNpcSpot()
        {
            InteractionData data = Data;
            Assert.That(data.RadiusCells, Is.EqualTo(InteractionTargets.UnityRadiusCells));
            MapInteractions house = data.Map("House");
            Assert.That(house.Things.Select(t => t.Kind), Is.EqualTo(new[] { "daily-plan-board", "student-day-end" }));
            Assert.That(house.NpcAnchors["location.home"], Is.EqualTo(new List<(float, float)> { (624f, 432f) }), "Unity House.unity home 앵커 (5.5,-4.5)");
            Assert.That(data.Map("ShopInterior").Things, Is.Empty, "Unity 판 가게 안에도 문 말고는 없다");
        }

        [Test]
        public void NpcArtIsTheFrontFacingStandFrameCutFromTheTopLeft()
        {
            NpcArt art = NpcArt.Load(Repo.Root);
            Assert.That(art.Order, Is.EqualTo(new[] { "npc.first-guide", "npc.teacher", "npc.librarian", "npc.work-manager", "npc.shopkeeper" }));
            CharacterFrame guide = art.Frame("npc.first-guide");
            Assert.That(Path.GetFileName(guide.Texture), Is.EqualTo("Premade_Character_32x32_02.png"));
            Assert.That((guide.X, guide.Y, guide.W, guide.H), Is.EqualTo((96, 0, 32, 64)), "Unity 사각형 y 1248(아래 원점) → 1312 - 1248 - 64 = 0");
            Assert.That(art.Problems, Is.Empty);
        }

        [Test]
        public void FirstGuideStandsAtHomeAndOthersAreElsewhereInTheMorning()
        {
            GameDefinitions defs = GameDataLoader.Load(Path.Combine(Repo.Root, "data"), RootbornBuilders.Register).Definitions;
            var schedules = new NpcScheduleLookupCache(defs.All<NpcSchedule>());
            var access = new NpcScheduleAccess(schedules);
            string LocationOf(string npcId) => schedules.TryGetByNpcId(npcId, out NpcSchedule s) ? access.ResolveLocationId(s.Id, 1, LocationStateTimeSlots.Morning, null) : string.Empty;
            WorldMapLoader loader = Repo.Loader();
            NpcStage stage = NpcStageTests.RealStage(loader);
            stage.EnterMap("House", Data.Map("House"), loader.LoadMap("House").World.Doors);
            stage.Tick(1, 480);
            Assert.That(stage.Count, Is.EqualTo(1));
            Assert.That(stage[0].NpcId, Is.EqualTo("npc.first-guide"));
            Assert.That(Math.Abs(stage[0].Pose.X - 624f), Is.LessThanOrEqualTo(3 * 32f + 16f), "자리(624, 432) 둘레를 어슬렁거린다(반지름 3칸)");
            Assert.That(Math.Abs(stage[0].Pose.Y - 432f), Is.LessThanOrEqualTo(3 * 32f + 16f));
            Assert.That(LocationOf("npc.teacher"), Is.EqualTo("location.school"));
        }

        [Test]
        public void CandidatesIncludeThingsEveryNpcAndSilentAutoDoors()
        {
            WorldMapLoader loader = Repo.Loader();
            NpcStage stage = NpcStageTests.RealStage(loader);
            List<InteractionCandidate> candidates = stage.EnterMap("House", Data.Map("House"), loader.LoadMap("House").World.Doors);
            Assert.That(candidates.Count(c => c.Kind == "npc" && c.Priority == 30 && c.Acts), Is.EqualTo(NpcArt.Load(Repo.Root).Order.Count), "NPC 모두(꺼진 채로)");
            Assert.That(candidates.Where(c => c.Kind == "npc").All(c => !c.Active), Is.True, "Tick 전에는 모두 꺼져 있다");
            stage.Tick(1, 480);
            Assert.That(candidates.Where(c => c.Kind == "npc" && c.Active).Select(c => c.Id), Is.EqualTo(new[] { "npc.first-guide" }));
            Assert.That(candidates.Where(c => c.Kind == "door"), Is.Not.Empty);
            Assert.That(candidates.Where(c => c.Kind == "door").All(c => !c.Acts && c.Priority == 110), Is.True);
        }
    }
}
