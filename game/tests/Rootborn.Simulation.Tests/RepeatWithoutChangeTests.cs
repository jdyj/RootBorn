using System.Linq;
using NUnit.Framework;
using Rootborn.Simulation.Headless;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트: 같은 행동을 연달아 했는데 보이는 것(관찰)이 그대로면 그 행동을 그날 정책의 후보에서 뺀다. 빼지 않으면(설정 0) 달리는 쪽이
    /// 그것을 막힘으로 잡는다. 2026-11-13 속도 보고: 학교 밖 정책이 4일째 장소 화면의 같은 줄을 4,947번 눌렀다(PolicyEscape 재현 판).
    /// </summary>
    public sealed class RepeatWithoutChangeTests
    {
        private static PlayView View(int day, string top, params string[] keys)
        {
            var v = new PlayView { InWorld = true, Day = day, Minutes = 600, MapId = "m", TopScreen = top };
            if (top != null) v.OpenScreens.Add(top);
            foreach (string k in keys) v.Actions.Add(new PlayAction { Key = k, Category = "row" });
            return v;
        }

        [Test]
        public void TheSameActionWithNoVisibleChange_IsBannedForTheDay_AfterThreeTimes()
        {
            var m = new PlayMemory { BanAfter = 3 };
            for (int i = 0; i < 3; i++)
            {
                PlayView v = View(1, "s", "row:a", "row:b");
                m.See(v);
                Assert.That(m.Banned("row:a"), Is.False, "아직 " + i + "번");
                m.Note(v, v.Find("row:a"));
            }
            m.See(View(1, "s", "row:a", "row:b"));
            Assert.That(m.RepeatsWithoutChange, Is.EqualTo(3));
            Assert.That(m.Banned("row:a"), Is.True);
            Assert.That(m.Banned("row:b"), Is.False);
            m.See(View(2, "s", "row:a", "row:b"));
            Assert.That(m.Banned("row:a"), Is.False, "다음 날에는 다시 해 본다");
        }

        [Test]
        public void AVisibleChange_ResetsTheCount()
        {
            var m = new PlayMemory { BanAfter = 3 };
            PlayView v = View(1, "s", "row:a");
            m.See(v); m.Note(v, v.Find("row:a"));
            m.See(v); m.Note(v, v.Find("row:a"));
            PlayView changed = View(1, "s", "row:a", "row:new");
            m.See(changed); m.Note(changed, changed.Find("row:a"));
            Assert.That(m.RepeatsWithoutChange, Is.EqualTo(0), "할 수 있는 것이 바뀌었다");
            m.See(changed);
            Assert.That(m.RepeatsWithoutChange, Is.EqualTo(1));
            Assert.That(m.Banned("row:a"), Is.False);
        }

        [Test]
        public void ADifferentActionInBetween_ResetsTheCount_AndZeroTurnsBanningOff()
        {
            var m = new PlayMemory { BanAfter = 0 };
            PlayView v = View(1, "s", "row:a", "row:b");
            for (int i = 0; i < 10; i++) { m.See(v); m.Note(v, v.Find("row:a")); }
            m.See(v);
            Assert.That(m.RepeatsWithoutChange, Is.EqualTo(10));
            Assert.That(m.Banned("row:a"), Is.False, "0이면 빼지 않는다");
            m.Note(v, v.Find("row:b"));
            m.See(v);
            Assert.That(m.RepeatsWithoutChange, Is.EqualTo(1), "다른 행동부터 다시 센다");
        }

        /// <summary>
        /// 둘이 번갈아 되돌리는 되풀이(걸어가 대화 열기 → 닫기 → 다시)도 같다: 같은 관찰에서 같은 행동이 지난번과 같은 관찰로 이어지기를 세 번 되풀이하면(처음 한 번 + 세 번) 그날 뺀다.
        /// 2026-11-13 이야기 유형 7일 판 4일째: NPC에게 걸어가기 2,466번과 대화 닫기 2,272번이 번갈았다(보이는 것이 둘 사이를 오갈 뿐).
        /// </summary>
        [Test]
        public void ATwoStepCycleThatChangesNothing_IsBannedToo()
        {
            var m = new PlayMemory { BanAfter = 3 };
            for (int i = 0; i < 4; i++)
            {
                PlayView world = View(1, null, "walk:npc/a", "door:x");
                m.See(world);
                Assert.That(m.Banned("walk:npc/a"), Is.False, "아직 " + i + "번");
                m.Note(world, world.Find("walk:npc/a"));
                PlayView talk = View(1, "dialogue", "button:dialogue/close");
                m.See(talk);
                m.Note(talk, talk.Find("button:dialogue/close"));
            }
            m.See(View(1, null, "walk:npc/a", "door:x"));
            Assert.That(m.Banned("walk:npc/a"), Is.True);
        }

        /// <summary>같은 일을 되풀이해도 보이는 것(시각·돈)이 달라지면 빼지 않는다(알바를 날마다 되풀이하는 것은 막힘이 아니다).</summary>
        [Test]
        public void RepeatingWorkThatMovesTheClock_IsNotBanned()
        {
            var m = new PlayMemory { BanAfter = 3 };
            for (int i = 0; i < 6; i++)
            {
                PlayView world = View(1, null, "walk:work/a");
                world.Minutes = 600 + i * 60;
                m.See(world);
                m.Note(world, world.Find("walk:work/a"));
            }
            Assert.That(m.Banned("walk:work/a"), Is.False);
        }

        /// <summary>
        /// 미니게임 화면에서 보이는 것(진행 줄, 움직이는 과녁·장애물)이 바뀌면 바뀐 것이다 — 시계가 멈춰 있어도(SIM-005: 대화가 열린 채 시작한
        /// 미니게임). 2026-11-13 이것저것 유형 7일 판이 4일째 기다리기 30번에서 막힘으로 잘못 잡혔다.
        /// </summary>
        [Test]
        public void AMovingMinigame_IsAVisibleChange_EvenWhenTheClockStands()
        {
            var m = new PlayMemory { BanAfter = 3 };
            for (int i = 0; i < 6; i++)
            {
                PlayView v = View(1, "minigame", "wait");
                v.Minigame = new MinigameSight { Status = "1/3" };
                v.Minigame.Elements.Add(new SightElement { Id = "obstacle", Visible = true, Rect = new Rootborn.Ui.UiRect(100 + i * 7, 50, 10, 10) });
                m.See(v);
                m.Note(v, new PlayAction { Key = "wait:10", Category = "wait" });
            }
            Assert.That(m.RepeatsWithoutChange, Is.EqualTo(0));
        }

        private static PlayResult OffSchoolToDayFour(int banAfter, string name)
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            using var body = new HeadlessBody(atlas);
            var settings = new PlaySettings { Days = 4, StepsPerDay = 5000, WindDownSteps = 120, MaxSteps = 1400, BanRepeatsWithoutChange = banAfter, SameActionStall = 30 };
            return new PlayRunner(body, PlayPolicyCatalog.Default().Create(PlayPolicyCatalog.OffSchool, atlas), 1, settings, atlas, name).Run();
        }

        /// <summary>빼지 않으면 달리는 쪽이 "같은 행동을 관찰 변화 없이" 막힘으로 잡는다(400걸음 판정은 활동 기록 수가 늘어 못 잡았다).</summary>
        [Test]
        public void WithoutBanning_TheRunnerCatchesTheRepeat()
        {
            PlayResult r = OffSchoolToDayFour(0, "repeat-no-ban");
            Assert.That(r.Failure, Is.Not.Null);
            Assert.That(r.Failure.What, Does.Contain("같은 행동"));
        }

        [Test]
        public void WithBanning_TheOffSchoolPolicyMovesOn()
        {
            PlayResult r = OffSchoolToDayFour(3, "repeat-ban");
            KnownDefects.AssertNoNewFailure(r);
            int longest = 0, run = 0;
            string last = null;
            foreach (PlayStep s in r.Steps.Where(x => !x.Key.StartsWith("wait", System.StringComparison.Ordinal))) { run = s.Key == last ? run + 1 : 1; last = s.Key; if (run > longest) longest = run; }
            Assert.That(longest, Is.LessThan(30), "연달아 같은 행동이 30번 미만(미니게임 기다리기 빼고 — 시계가 흘러 관찰이 바뀐다)");
            Assert.That(r.Steps.Last().Day, Is.GreaterThanOrEqualTo(4), "4일째까지 갔다");
        }
    }
}
