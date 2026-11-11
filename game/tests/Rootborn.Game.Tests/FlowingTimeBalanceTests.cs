using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 지켜야 할 것(설계 3.4절, 테스트로 고정): 밤을 새는 것이 자는 것보다 효율이 좋으면 안 된다. 실제 data/와 game/time.json으로
    /// 이틀 동안 할 수 있는 활동 수와 얻는 성장(특성·기술 값의 합)을 셌을 때 밤을 샌 쪽이 제때 잔 쪽보다 많지 않다.
    /// 수치(활동 비용, 회복, 밤 체력 감소)를 바꿔도 이 검사가 깨지면 알 수 있다. 자동 테스트다(화면 없음, GameSession 명령).
    /// 플레이어는 욕심쟁이다: 할 수 있는 활동(시간이 드는 학교 밖 활동·장소 활동·마을 돕기, 데이터 순서)을 할 수 있는 동안 계속 한다.
    /// </summary>
    public sealed class FlowingTimeBalanceTests
    {
        private const float GameHour = 25f;

        private sealed class Tally
        {
            public int Actions;
            public int Growth;
            public readonly List<string> Days = new List<string>();
        }

        [Test]
        public void StayingUpAllNight_IsNeverBetterThanSleepingOnTime_OverTwoDays()
        {
            var onTime = Play("제때 잠", nights: new[] { false, false });
            var allNight = Play("이틀 밤샘", nights: new[] { true, true });
            var firstNight = Play("첫날 밤샘", nights: new[] { true, false });

            foreach (var tally in new[] { onTime, allNight, firstNight })
                foreach (var line in tally.Days) TestContext.Out.WriteLine(line);

            Assert.That(onTime.Actions, Is.GreaterThan(0), "준비: 실제 데이터로 활동을 했다");
            Assert.That(allNight.Actions, Is.LessThanOrEqualTo(onTime.Actions), "이틀 밤샘의 활동 수");
            Assert.That(allNight.Growth, Is.LessThanOrEqualTo(onTime.Growth), "이틀 밤샘의 성장");
            Assert.That(firstNight.Actions, Is.LessThanOrEqualTo(onTime.Actions), "첫날 밤샘의 활동 수");
            Assert.That(firstNight.Growth, Is.LessThanOrEqualTo(onTime.Growth), "첫날 밤샘의 성장");
        }

        // nights[i]: i번째 날 밤을 새는가. 제때 자는 날은 할 것이 없거나 취침 시각이 되면 잔다. 밤을 새는 날은 할 것이 없으면 1시간씩 기다린다.
        private static Tally Play(string name, bool[] nights)
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var tally = new Tally();
            var activities = Candidates();
            int request = 0;
            for (int d = 0; d < nights.Length; d++)
            {
                int day = game.Student.CurrentDay, actions = 0;
                int energyStart = game.Student.Energy, focusStart = game.Student.Focus, startMinutes = game.Student.TimeMinutes;
                var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
                string stop;
                for (int guard = 0; ; guard++)
                {
                    Assert.That(guard, Is.LessThan(2000), "끝나지 않는 하루");
                    if (!nights[d] && game.Student.TimeMinutes >= game.TimeRules.BedtimeMinutes) { stop = "취침 시각"; break; }
                    string done = TryAny(game, activities, name + ":" + request++);
                    if (done != null)
                    {
                        actions++;
                        counts[done] = counts.TryGetValue(done, out int c) ? c + 1 : 1;
                        if (game.Student.CurrentDay != day) { stop = "07:00 넘김"; break; }
                        continue;
                    }

                    if (!nights[d]) { stop = Reason(game); break; }
                    game.AdvanceTime(GameHour, false);
                    if (game.Student.CurrentDay != day) { stop = "07:00 넘김(" + Reason(game) + ")"; break; }
                }

                int stoppedAt = game.Student.TimeMinutes;
                int energyLeft = game.Student.Energy, focusLeft = game.Student.Focus;
                if (game.Student.CurrentDay == day)
                {
                    Assert.That(game.Sleep().Ended, Is.True);
                    Assert.That(game.StartNextDay(), Is.True);
                }

                tally.Actions += actions;
                tally.Days.Add(string.Format("{0} | {1}일째 | 시작 {2} 체력 {3} 집중 {4} | 활동 {5}개 | 멈춘 때 {6}({7}) 남은 체력 {8} 집중 {9} | {10}",
                    name, day, Clock(startMinutes), energyStart, focusStart, actions, Clock(stoppedAt), stop, energyLeft, focusLeft,
                    string.Join(", ", counts.Select(p => p.Key + "×" + p.Value))));
            }

            tally.Growth = Growth(game.Student);
            tally.Days.Add(string.Format("{0} | 합계 활동 {1}개, 성장(특성·기술 합) {2}", name, tally.Actions, tally.Growth));
            return tally;
        }

        // 시간이 드는 활동만(0분짜리는 시계를 쓰지 않는 소개·대화라 횟수 비교에서 뺀다). 미니게임이 있는 알바·연습은 뺀다.
        private static List<(string Kind, string Id)> Candidates()
        {
            var list = new List<(string, string)>();
            foreach (var a in TestWorld.Definitions.All<OutsideSchoolActivity>()) if (a.TimeCostMinutes > 0) list.Add(("outside", a.Id));
            foreach (var a in TestWorld.Definitions.All<LocationActivity>()) if (a.TimeCostMinutes > 0) list.Add(("location", a.Id));
            foreach (var a in TestWorld.Definitions.All<TownHelpAction>()) if (a.TimeCostMinutes > 0) list.Add(("help", a.Id));
            return list;
        }

        private static string TryAny(GameSession game, List<(string Kind, string Id)> activities, string request)
        {
            foreach (var (kind, id) in activities)
            {
                bool applied;
                switch (kind)
                {
                    case "outside": applied = game.PerformOutsideSchoolActivity(id, request + ":" + id).Kind == LifeActivityResultKind.Applied; break;
                    case "location": applied = game.PerformLocationActivity(id, request + ":" + id).Kind == LifeActivityResultKind.Applied; break;
                    default: applied = game.PerformTownHelp(id, request + ":" + id).Kind == LifeActivityResultKind.Applied; break;
                }

                if (applied) return id;
            }

            return null;
        }

        private static string Reason(GameSession game)
        {
            if (game.IsExhausted) return "지침(체력 0)";
            if (game.Student.Energy == 0) return "체력";
            if (game.Student.Focus <= 1) return "집중";
            return "조건(할 수 있는 활동 없음)";
        }

        private static int Growth(StudentLifeProgress student)
            => student.GetTraitIds().Sum(student.GetTraitValue) + student.GetSkillIds().Sum(student.GetSkillValue);

        private static string Clock(int minutes)
        {
            int day = minutes / (24 * 60), clock = minutes % (24 * 60);
            return (day > 0 ? "+" + day + "일 " : "") + (clock / 60).ToString("00") + ":" + (clock % 60).ToString("00");
        }
    }
}
