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
            List<PlacedNpc> placed = NpcPlacement.Place(NpcArt.Load(Repo.Root).Order, LocationOf, Data.Map("House"));
            Assert.That(placed.Count, Is.EqualTo(1));
            Assert.That(placed[0].NpcId, Is.EqualTo("npc.first-guide"));
            Assert.That((placed[0].X, placed[0].Y), Is.EqualTo((624f, 432f)));
            Assert.That(LocationOf("npc.teacher"), Is.EqualTo("location.school"));
        }

        [Test]
        public void SecondNpcAtTheSamePlaceTakesTheNextSpotAndAMissingSpotHidesIt()
        {
            var map = new MapInteractions();
            map.NpcAnchors["square"] = new List<(float, float)> { (10f, 10f) };
            List<PlacedNpc> placed = NpcPlacement.Place(new[] { "a", "b" }, _ => "square", map);
            Assert.That(placed.Select(p => p.NpcId), Is.EqualTo(new[] { "a" }), "두 번째 자리가 없으면 b는 서지 않는다");
            map.NpcAnchors["square"].Add((20f, 10f));
            placed = NpcPlacement.Place(new[] { "a", "b" }, _ => "square", map);
            Assert.That(placed[1].X, Is.EqualTo(20f));
        }

        [Test]
        public void CandidatesIncludeThingsNpcsAndSilentAutoDoors()
        {
            LoadedMap house = Repo.Loader().LoadMap("House");
            List<InteractionCandidate> candidates = NpcPlacement.Candidates(Data, Data.Map("House"),
                new[] { new PlacedNpc("npc.first-guide", "location.home", 624f, 432f) }, house.World.Doors);
            Assert.That(candidates.Count(c => c.Kind == "npc" && c.Priority == 30 && c.Acts), Is.EqualTo(1));
            Assert.That(candidates.Where(c => c.Kind == "door"), Is.Not.Empty);
            Assert.That(candidates.Where(c => c.Kind == "door").All(c => !c.Acts && c.Priority == 110), Is.True);
        }
    }
}
