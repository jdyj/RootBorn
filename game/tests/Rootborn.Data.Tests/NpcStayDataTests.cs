using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Dialogue;
using Rootborn.Core.NpcMovement;
using Rootborn.Data.Builders;

namespace Rootborn.Data.Tests
{
    /// <summary>자동 테스트: NPC 일과표 줄의 머무는 행동(stay)을 데이터에서 읽는다. 화면 근거가 아니다.</summary>
    public sealed class NpcStayDataTests
    {
        [Test]
        public void 일과표의_머무는_행동을_읽는다()
        {
            LoadResult result = GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors.Take(20)));

            var stays = new List<INpcStayBehavior>();
            foreach (NpcSchedule schedule in result.Definitions.All<NpcSchedule>())
                foreach (NpcScheduleEntry entry in schedule.Entries)
                    stays.Add(entry.Stay);

            Assert.That(stays, Is.Not.Empty);
            Assert.Multiple(() =>
            {
                foreach (INpcStayBehavior stay in stays)
                {
                    Assert.That(stay, Is.InstanceOf<NpcWanderStay>());
                    var wander = (NpcWanderStay)stay;
                    Assert.That((wander.RadiusCells, wander.PauseMinSeconds, wander.PauseMaxSeconds), Is.EqualTo((3f, 2f, 6f)));
                    // Core는 이웃한 두 시간대의 Stay가 같은 객체일 때 머무름을 잇는다 — 같은 참조는 같은 객체 하나여야 한다.
                    Assert.That(ReferenceEquals(stay, stays[0]), Is.True, "모든 줄이 같은 Stay_Wander_Default 객체를 가리킨다");
                }
            });
        }

        [Test]
        public void stay가_없는_줄은_서_있기다()
        {
            using var dir = new TempDir();
            dir.Write("npcs/NpcScheduleDefinition.json", @"[
              { ""type"": ""NpcScheduleDefinition"", ""id"": ""Schedule_Test"", ""npc"": null, ""fallbackLocation"": null,
                ""entries"": [ { ""location"": null, ""dialogue"": null, ""dialogueKey"": """", ""interactionHintKey"": """",
                                 ""eventNoticeKey"": """", ""priority"": 1, ""rules"": [] } ] } ]");

            LoadResult result = GameDataLoader.Load(dir.Path, RootbornBuilders.Register);

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            NpcSchedule schedule = result.Definitions.Get<NpcSchedule>("Schedule_Test");
            Assert.That(schedule.Entries.Single().Stay, Is.SameAs(NpcStandStay.Instance));
        }
    }
}
