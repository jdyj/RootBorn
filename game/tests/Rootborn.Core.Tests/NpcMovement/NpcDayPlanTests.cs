using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Dialogue;
using Rootborn.Core.NpcMovement;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Core.World;
using static Rootborn.Core.Tests.NpcMovement.NpcTestMaps;

namespace Rootborn.Core.Tests.NpcMovement
{
    public sealed class NpcDayPlanTests
    {
        // 게임 하루 = 실제 1440초 → 게임 1분 = 실제 1초. 하루는 07:00(420)~다음 날 07:00(1860).
        private static readonly TimeRules Rules = new TimeRules(1440f, 420, 1320, 0, 0);
        private const float Speed = 2.5f;

        private const string LocMorning = "location.a-morning";
        private const string LocAfternoon = "location.b-afternoon";
        private const string LocEvening = "location.a-evening";
        private const string LocHome = "location.home";

        /// <summary>장소 → 자리 목록. 부른 횟수를 센다.</summary>
        private sealed class Anchors : INpcAnchors
        {
            public readonly Dictionary<string, NpcSpot[]> All = new Dictionary<string, NpcSpot[]>();
            public int Calls;

            public bool TryGet(string locationId, int slot, out NpcSpot spot)
            {
                Calls++;
                spot = default;
                if (!All.TryGetValue(locationId, out var spots) || slot >= spots.Length) return false;
                spot = spots[slot];
                return true;
            }
        }

        /// <summary>
        /// A: 가운데 줄 22칸 복도, 오른쪽 끝(21,1) 칸에 B로 가는 문. B: 3x2 방, 도착 지점 (1,1).
        /// 오전 자리 A(1,1) → 오후 자리 B(2,1)은 A에서 19칸 + B에서 1칸 = 20칸.
        /// </summary>
        private static Maps TwoMaps(bool door = true)
        {
            var maps = new Maps();
            var doors = door
                ? new[] { new DoorArea("b", new AreaRect(21 * Cell, 1 * Cell, Cell, Cell), new TravelTarget("B", "fromA")) }
                : new DoorArea[0];
            maps.All["A"] = Map("A", Grid(
                "######################",
                "......................",
                "######################"), doors);
            maps.All["B"] = Map("B", Grid(
                "#####",
                "#...#",
                "#...#",
                "#####"), spawns: new Dictionary<string, (float X, float Y)> { ["fromA"] = Center(1, 1) });
            return maps;
        }

        private static Anchors DefaultAnchors()
        {
            var anchors = new Anchors();
            anchors.All[LocMorning] = new[] { Spot("A", 1, 1), Spot("A", 3, 1) };
            anchors.All[LocAfternoon] = new[] { Spot("B", 2, 1), Spot("B", 3, 1) };
            anchors.All[LocEvening] = new[] { Spot("A", 10, 1), Spot("A", 12, 1) };
            anchors.All[LocHome] = new[] { Spot("A", 1, 1), Spot("A", 3, 1) };
            return anchors;
        }

        private static NpcScheduleEntry Row(string locationId, string slotId, INpcStayBehavior stay = null) =>
            new NpcScheduleEntry(locationId, null, null, null, null, 1, new INpcScheduleRule[] { new TimeSlotScheduleRule(new[] { slotId }) }, stay);

        /// <summary>오전 A, 오후 B, 저녁 A(다른 자리), 그 밖(밤)은 집(A의 오전 자리).</summary>
        private static NpcSchedule Schedule(string npcId, INpcStayBehavior afternoonStay = null) =>
            new NpcSchedule("Schedule_" + npcId, new NpcDefinition(npcId, npcId + ".name", npcId + ".intro", LocHome, null, null, null, null, null), LocHome, new[]
            {
                Row(LocMorning, LocationStateTimeSlots.Morning),
                Row(LocAfternoon, LocationStateTimeSlots.Afternoon, afternoonStay),
                Row(LocEvening, LocationStateTimeSlots.Evening),
            });

        private static NpcWorld World(IReadOnlyList<NpcSchedule> schedules, Anchors anchors = null, Maps maps = null, float speed = Speed)
        {
            var ids = new List<string>();
            foreach (var s in schedules) ids.Add(s.Npc.Id);
            return new NpcWorld(ids, new NpcScheduleLookupCache(schedules), anchors ?? DefaultAnchors(), maps ?? TwoMaps(), new NpcWalkSettings(speed), Rules);
        }

        private static NpcWorld OneNpc(INpcStayBehavior afternoonStay = null, Anchors anchors = null, Maps maps = null, float speed = Speed, int day = 1)
        {
            NpcWorld world = World(new[] { Schedule("npc.one", afternoonStay) }, anchors, maps, speed);
            world.Prepare(day);
            return world;
        }

        private static void AssertAt(NpcPose pose, NpcSpot spot, bool moving, string message)
        {
            Assert.That(pose.Present, Is.True, message);
            Assert.That(pose.MapId, Is.EqualTo(spot.MapId), message);
            Assert.That(pose.X, Is.EqualTo(spot.X).Within(1e-3f), message);
            Assert.That(pose.Y, Is.EqualTo(spot.Y).Within(1e-3f), message);
            Assert.That(pose.Moving, Is.EqualTo(moving), message);
        }

        [Test]
        public void SlotStarts는_FromMinutes와_같은_경계다()
        {
            var starts = new List<(int Minutes, string SlotId)>();
            LocationStateTimeSlots.SlotStarts(Rules.DayStartMinutes, Rules.DayEndMinutes, starts);

            Assert.That(starts, Is.EqualTo(new List<(int, string)>
            {
                (420, LocationStateTimeSlots.Morning),
                (720, LocationStateTimeSlots.Afternoon),
                (1020, LocationStateTimeSlots.Evening),
                (1260, LocationStateTimeSlots.Night),
            }));
            for (int i = 0; i < starts.Count; i++)
            {
                var (m, id) = starts[i];
                Assert.That(LocationStateTimeSlots.FromMinutes(m, Rules.DayStartMinutes), Is.EqualTo(id), m.ToString());
                if (i > 0) Assert.That(LocationStateTimeSlots.FromMinutes(m - 1, Rules.DayStartMinutes), Is.Not.EqualTo(id), m.ToString());
            }

            LocationStateTimeSlots.SlotStarts(Rules.DayStartMinutes, Rules.DayEndMinutes, starts);
            Assert.That(starts.Count, Is.EqualTo(4), "넘긴 목록은 비우고 채운다");
        }

        [Test]
        public void 오전에는_오전_자리에_있다()
        {
            NpcWorld world = OneNpc();
            Assert.That(world.Count, Is.EqualTo(1));
            Assert.That(world.NpcId(0), Is.EqualTo("npc.one"));
            AssertAt(world.PoseOf(0, 500), Spot("A", 1, 1), false, "오전");
            Assert.That(world.PoseOf(0, 500).Facing, Is.EqualTo(Facing.Down), "서 있기");
        }

        [Test]
        public void 경계_시각에_출발한다()
        {
            NpcWorld world = OneNpc();
            NpcPose atBoundary = world.PoseOf(0, 720);
            Assert.That(atBoundary.MapId, Is.EqualTo("A"));
            Assert.That((atBoundary.X, atBoundary.Y), Is.EqualTo(Center(1, 1)), "경계 시각에는 아직 오전 자리");

            NpcPose after = world.PoseOf(0, 721);
            Assert.That(after.Moving, Is.True);
            Assert.That(after.MapId, Is.EqualTo("A"));
            Assert.That(after.X, Is.EqualTo(Center(1, 1).X + 2.5f * Cell).Within(1e-2f), "1분(=1초)에 2.5칸");
            Assert.That(after.Facing, Is.EqualTo(Facing.Right));
        }

        [Test]
        public void 걸리는_시간은_칸_수를_속도로_나눈_것이다()
        {
            NpcWorld world = OneNpc();
            Assert.That(world.PoseOf(0, 727.9).Moving, Is.True, "20칸 / 초당 2.5칸 = 8분 → 728분에 도착");
            AssertAt(world.PoseOf(0, 728.01), Spot("B", 2, 1), false, "도착");
            AssertAt(world.PoseOf(0, 729), Spot("B", 2, 1), false, "도착 뒤 오후 자리에 서 있다");
        }

        [Test]
        public void 이동_중_어느_시각에도_정확히_한_맵에_있다()
        {
            NpcWorld world = OneNpc();
            string map = null;
            int changes = 0;
            for (int i = 0; i <= 180; i++)
            {
                double t = 720 + i * 0.05;
                NpcPose pose = world.PoseOf(0, t);
                Assert.That(pose.Present, Is.True, t.ToString());
                Assert.That(pose.MapId, Is.EqualTo("A").Or.EqualTo("B"), t.ToString());
                if (map != null && pose.MapId != map) changes++;
                map = pose.MapId;
            }
            Assert.That(map, Is.EqualTo("B"));
            Assert.That(changes, Is.EqualTo(1), "A에서 B로 한 번만 바뀐다");
        }

        [Test]
        public void 시각이_한꺼번에_뛰어도_맞는_자리다()
        {
            var wander = new NpcWanderStay(3f, 2f, 6f);
            NpcWorld stepped = OneNpc(wander);
            for (int m = 500; m <= 900; m++) stepped.PoseOf(0, m);
            NpcWorld jumped = OneNpc(wander);

            AssertSame(stepped.PoseOf(0, 900), jumped.PoseOf(0, 900), "900분");
            AssertSame(stepped.PoseOf(0, 901.37), jumped.PoseOf(0, 901.37), "901.37분");
        }

        [Test]
        public void 시각이_뒤로_가도_하루_밖이어도_예외가_없다()
        {
            NpcWorld world = OneNpc(new NpcWanderStay(3f, 2f, 6f));
            NpcPose start = OneNpc(new NpcWanderStay(3f, 2f, 6f)).PoseOf(0, 420);
            NpcPose end = OneNpc(new NpcWanderStay(3f, 2f, 6f)).PoseOf(0, 1860);

            world.PoseOf(0, 900);
            AssertAt(world.PoseOf(0, 430), Spot("A", 1, 1), false, "뒤로 간 시각");
            AssertSame(start, world.PoseOf(0, -5), "하루 앞은 처음으로");
            AssertSame(end, world.PoseOf(0, 5000), "하루 뒤는 끝으로");
            AssertSame(start, world.PoseOf(0, double.NaN), "NaN은 처음으로");
            AssertSame(end, world.PoseOf(0, double.PositiveInfinity), "무한대는 끝으로");
            Assert.That(end.Present && end.MapId == "B" && !end.Moving, Is.True, "B에서 A로 돌아가는 문이 없어 하루 끝에도 B에 서 있다");
        }

        [Test]
        public void 길이_없으면_출발_자리에_머문다()
        {
            NpcWorld world = OneNpc(maps: TwoMaps(door: false));
            AssertAt(world.PoseOf(0, 721), Spot("A", 1, 1), false, "B로 가는 길이 없다");
            AssertAt(world.PoseOf(0, 900), Spot("A", 1, 1), false, "오후 내내");
            NpcPose evening = world.PoseOf(0, 1021);
            Assert.That(evening.Moving, Is.True, "다음 경계에 다시 시도해 저녁 자리로 걷는다");
            Assert.That(evening.MapId, Is.EqualTo("A"));
            AssertAt(world.PoseOf(0, 1100), Spot("A", 10, 1), false, "저녁 자리");
        }

        [Test]
        public void 자리가_없는_시간대에는_어느_맵에도_없다()
        {
            Anchors anchors = DefaultAnchors();
            anchors.All.Remove(LocAfternoon);
            NpcWorld world = OneNpc(anchors: anchors);

            AssertAt(world.PoseOf(0, 719), Spot("A", 1, 1), false, "오전");
            Assert.That(world.PoseOf(0, 720).Present, Is.False, "경계에 사라진다");
            Assert.That(world.PoseOf(0, 800).Present, Is.False);
            AssertAt(world.PoseOf(0, 1020), Spot("A", 10, 1), false, "자리가 생기면 걸어오지 않고 나타난다");

            NpcWorld noSchedule = new NpcWorld(new[] { "npc.nobody" }, new NpcScheduleLookupCache(new NpcSchedule[0]), DefaultAnchors(), TwoMaps(), new NpcWalkSettings(Speed), Rules);
            noSchedule.Prepare(1);
            Assert.That(noSchedule.PoseOf(0, 500).Present, Is.False, "일정이 없으면 하루 종일 없다");
            Assert.That(noSchedule.PoseOf(0, 1500).Present, Is.False);
        }

        [Test]
        public void 같은_장소의_두_번째_NPC는_두_번째_자리다()
        {
            NpcWorld world = World(new[] { Schedule("npc.one"), Schedule("npc.two") });
            world.Prepare(1);
            Assert.That(world.NpcId(1), Is.EqualTo("npc.two"));
            AssertAt(world.PoseOf(0, 500), Spot("A", 1, 1), false, "첫 NPC");
            AssertAt(world.PoseOf(1, 500), Spot("A", 3, 1), false, "두 번째 NPC");
            AssertAt(world.PoseOf(1, 900), Spot("B", 3, 1), false, "오후에도 두 번째 자리");
        }

        [Test]
        public void 이동이_다음_경계까지_안_끝나면_그_자리에서_새_길을_잡는다()
        {
            // 초당 0.04칸: 오후 300분에 12칸 → 1020분에 A(13,1)(문까지 못 감). 저녁 자리 A(10,1)은 왼쪽 3칸 = 75분.
            NpcWorld world = OneNpc(speed: 0.04f);
            NpcPose before = world.PoseOf(0, 1019.9);
            NpcPose atBoundary = world.PoseOf(0, 1020);
            Assert.That(atBoundary.MapId, Is.EqualTo("A"));
            Assert.That(atBoundary.X, Is.EqualTo(Center(13, 1).X).Within(Cell / 2f), "1020분의 자리");
            Assert.That(Math.Abs(atBoundary.X - before.X), Is.LessThanOrEqualTo(Cell / 2f), "새 길은 그 자리에서 시작한다");

            NpcPose back = world.PoseOf(0, 1050);
            Assert.That(back.Moving, Is.True);
            Assert.That(back.Facing, Is.EqualTo(Facing.Left), "B 쪽이 아니라 저녁 자리로");
            AssertAt(world.PoseOf(0, 1100), Spot("A", 10, 1), false, "저녁 자리에 도착");
        }

        [Test]
        public void 자리가_같고_줄만_바뀌면_머무름이_이어진다()
        {
            var wander = new NpcWanderStay(3f, 2f, 6f);
            var anchors = new Anchors();
            anchors.All[LocMorning] = new[] { Spot("A", 10, 1) };
            NpcSchedule Make(INpcStayBehavior afternoon) => new NpcSchedule("Schedule", new NpcDefinition("npc.one", "n", "i", LocMorning, null, null, null, null, null), LocMorning, new[]
            {
                Row(LocMorning, LocationStateTimeSlots.Morning, wander),
                Row(LocMorning, LocationStateTimeSlots.Afternoon, afternoon),
            });
            var oneRow = new NpcSchedule("Schedule", new NpcDefinition("npc.one", "n", "i", LocMorning, null, null, null, null, null), LocMorning, new[]
            {
                new NpcScheduleEntry(LocMorning, null, null, null, null, 1, new INpcScheduleRule[] { new TimeSlotScheduleRule(new[] { LocationStateTimeSlots.Morning, LocationStateTimeSlots.Afternoon }) }, wander),
            });

            NpcWorld continued = World(new[] { Make(wander) }, anchors);
            NpcWorld reference = World(new[] { oneRow }, anchors);
            continued.Prepare(1);
            reference.Prepare(1);
            for (double t = 719; t <= 760; t += 0.5)
                AssertSame(reference.PoseOf(0, t), continued.PoseOf(0, t), "같은 행동이면 이어진다 " + t);

            NpcWorld restarted = World(new[] { Make(NpcStandStay.Instance) }, anchors);
            restarted.Prepare(1);
            AssertAt(restarted.PoseOf(0, 720), Spot("A", 10, 1), false, "다른 행동이면 경계에서 새 머무름");
            AssertAt(restarted.PoseOf(0, 800), Spot("A", 10, 1), false, "오후 내내 서 있기");
        }

        [Test]
        public void 날이_다르면_어슬렁_걸음이_다르다()
        {
            var wander = new NpcWanderStay(3f, 2f, 6f);
            NpcWorld day1 = OneNpc(wander, day: 1);
            NpcWorld day2 = OneNpc(wander, day: 2);
            NpcWorld day1Again = OneNpc(wander, day: 1);

            int differ = 0;
            for (int m = 730; m < 1020; m += 7)
            {
                NpcPose a = day1.PoseOf(0, m), b = day2.PoseOf(0, m);
                Assert.That(a.MapId, Is.EqualTo("B"));
                if (a.X != b.X || a.Y != b.Y) differ++;
                AssertSame(a, day1Again.PoseOf(0, m), "같은 날은 같다 " + m);
            }
            Assert.That(differ, Is.GreaterThan(0));
        }

        [Test]
        public void Prepare는_같은_날이면_다시_만들지_않는다()
        {
            Anchors anchors = DefaultAnchors();
            NpcWorld world = World(new[] { Schedule("npc.one") }, anchors);
            world.Prepare(1);
            NpcDayPlan plan = world.PlanOf(0);
            int calls = anchors.Calls;
            Assert.That(plan.NpcId, Is.EqualTo("npc.one"));
            Assert.That(plan.SegmentCount, Is.GreaterThan(1));

            world.Prepare(1);
            world.PoseOf(0, 800);
            Assert.That(world.PlanOf(0), Is.SameAs(plan));
            Assert.That(anchors.Calls, Is.EqualTo(calls), "같은 날이면 다시 묻지 않는다");

            world.Prepare(2);
            Assert.That(world.PlanOf(0), Is.Not.SameAs(plan), "날이 바뀌면 다시 만든다");
            NpcDayPlan day2 = world.PlanOf(0);

            world.ForgetMap("B");
            AssertAt(world.PoseOf(0, 900), Spot("B", 2, 1), false, "ForgetMap 뒤 PoseOf가 다시 만든다");
            Assert.That(world.PlanOf(0), Is.Not.SameAs(day2));
        }

        [Test]
        public void 걷는_속도는_0보다_커야_한다()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NpcWalkSettings(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NpcWalkSettings(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NpcWalkSettings(float.NaN));
            Assert.That(new NpcWalkSettings(2.5f).SpeedCellsPerSecond, Is.EqualTo(2.5f));
        }
    }
}
