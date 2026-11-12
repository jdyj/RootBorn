using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 하루의 균형(docs/superpowers/specs/2026-11-12-time-balance.md): 실제 data/와 game/time.json으로 흔한 하루(수업 있는 평일, 학교 밖 위주,
    /// 알바 위주, 진로 연습 위주, 주말)를 욕심쟁이 플레이어로 돌려 하루 큰 활동 수가 목표 범위 안인지, 시간이 체력·집중보다 먼저(또는 비슷하게)
    /// 막는지 본다. 목표 범위와 가정(활동 사이 걷는 시간, 미니게임을 하는 실제 시간)은 이 폴더의 day-balance.json 한 곳이다(게임은 읽지 않고 배포·데이터 지문에도 들어가지 않는다).
    /// 활동 시간 값(data/의 timeCostMinutes)을 바꿔 범위를 벗어나면 이 검사가 깨진다. 자동 테스트다(화면 없음, GameSession 명령, 시계는 손으로 돌림).
    /// 큰 활동 = 시간이 드는 활동(0분짜리 소개·대화 선택지는 세지 않는다). 하루 이벤트는 그날 한 번 나오는 일이라 넣지 않는다.
    /// 성인 시설 일은 학생 하루가 아니라 넣지 않는다(체력·집중을 쓰지 않아 시간만 막는다 — 표는 설계 문서).
    /// </summary>
    public sealed class DayBalanceTests
    {
        private sealed class Balance
        {
            public int Min, Max, BodyMayStopFrom, TravelMinutes;
            public float MinigameShare;
        }

        private sealed class Option
        {
            public string Id;
            public Func<GameSession, string, bool> Try;
        }

        private sealed class DayLog
        {
            public int Actions, StoppedAt;
            public string Stop, Line;
            public bool TimeStopped;
        }

        private static readonly Lazy<Balance> Target = new Lazy<Balance>(ReadBalance);

        [Test]
        public void AWeekdayWithClass_HasFourToSixBigActivities_AndTimeLimitsTheDay()
            => Check(Play("수업 있는 평일", weekend: false, ClassFirst(), Outside(), Location(skipOncePerDay: false), Help()));

        [Test]
        public void AWeekdayOutsideSchool_HasFourToSixBigActivities_AndTimeLimitsTheDay()
            => Check(Play("학교 밖 위주 평일", weekend: false, Outside(), Help(), Location(skipOncePerDay: true)));

        [Test]
        public void AWeekdayOfPartTimeWork_HasFourToSixBigActivities_AndTimeLimitsTheDay()
            => Check(Play("알바 위주 평일", weekend: false, PartTimeWork(), Outside(), Help(), Location(skipOncePerDay: true)));

        [Test]
        public void AWeekdayOfCareerPractice_HasFourToSixBigActivities_AndTimeLimitsTheDay()
            => Check(Play("진로 연습 위주 평일", weekend: false, Practice(), Outside(), Help(), Location(skipOncePerDay: true)));

        [Test]
        public void AWeekend_HasFourToSixBigActivities_AndTimeLimitsTheDay()
            => Check(Play("주말", weekend: true, Outside(), Location(skipOncePerDay: false), Help(), PartTimeWork()));

        private static void Check(DayLog day)
        {
            var b = Target.Value;
            TestContext.Out.WriteLine(day.Line);
            Assert.That(day.Actions, Is.InRange(b.Min, b.Max), "하루 큰 활동 수(day-balance.json bigActivitiesPerDay): " + day.Line);
            Assert.That(day.TimeStopped || day.StoppedAt >= b.BodyMayStopFrom, Is.True,
                "체력·집중이 시간보다 너무 일찍 막았다(day-balance.json bodyMayStopFrom 전): " + day.Line);
        }

        // 하루: 앞 목록을 먼저 본다(위주로 하는 것). 목록 안에서는 돌아가며 고른다(같은 것만 되풀이하지 않는다). 걷는 시간을 먼저 보낸다. 취침 시각이 되면 멈춘다. 아무것도 안 되면 멈춘다.
        private static DayLog Play(string name, bool weekend, params List<Option>[] lists)
        {
            var b = Target.Value;
            var turn = new int[lists.Length];
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            while (game.Student.Calendar.IsWeekend(game.Student.CurrentDay) != weekend)
            {
                Assert.That(game.Sleep().Ended, Is.True);
                Assert.That(game.StartNextDay(), Is.True);
            }

            int day = game.Student.CurrentDay, start = game.Student.TimeMinutes, request = 0;
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var log = new DayLog();
            for (int guard = 0; ; guard++)
            {
                Assert.That(guard, Is.LessThan(200), "끝나지 않는 하루");
                if (game.Student.TimeMinutes >= game.TimeRules.BedtimeMinutes) { log.Stop = "취침 시각"; log.TimeStopped = true; break; }
                int before = game.Student.TimeMinutes;
                Walk(game, b.TravelMinutes);
                string done = null;
                for (int l = 0; l < lists.Length && done == null; l++)
                    for (int k = 0; k < lists[l].Count; k++)
                    {
                        var option = lists[l][(turn[l] + k) % lists[l].Count];
                        if (!option.Try(game, name + ":" + request++ + ":" + option.Id)) continue;
                        done = option.Id;
                        turn[l] = (turn[l] + k + 1) % lists[l].Count;
                        break;
                    }
                if (done == null)
                {
                    log.StoppedAt = before; // 걷기만 한 시간은 하루 끝으로 치지 않는다
                    log.Stop = Reason(game);
                    break;
                }

                log.Actions++;
                counts[done] = counts.TryGetValue(done, out int c) ? c + 1 : 1;
                Assert.That(game.Student.CurrentDay, Is.EqualTo(day), "활동이 07:00을 넘겼다");
            }

            if (log.TimeStopped) log.StoppedAt = game.Student.TimeMinutes;
            log.Line = string.Format("{0} | {1}일째 | 시작 {2} | 큰 활동 {3}개 | 멈춘 때 {4}({5}) 남은 체력 {6} 집중 {7} | {8}",
                name, day, Clock(start), log.Actions, Clock(log.StoppedAt), log.Stop, game.Student.Energy, game.Student.Focus,
                string.Join(", ", counts.Select(p => p.Key + "×" + p.Value)));
            return log;
        }

        private static void Walk(GameSession game, int minutes)
        {
            if (minutes <= 0) return;
            game.AdvanceTime(minutes * game.TimeRules.RealSecondsPerGameDay / (24 * 60) + 0.001f, false);
        }

        // 미니게임을 하는 동안 시계가 흐른다(혼자 하기, 미니게임 화면은 시계를 멈추지 않는다). 제한 시간 합의 일정 몫만큼 실제 초를 보낸다.
        private static void PlayMinigame(GameSession game, Minigame minigame)
        {
            float limit = 0f;
            foreach (var challenge in minigame.Challenges)
                if (challenge is ChallengeBase c) limit += c.TimeLimitSeconds;
            game.AdvanceTime(limit * Target.Value.MinigameShare, false);
        }

        private static List<Option> ClassFirst()
            => TestWorld.Definitions.All<LocationActivity>()
                .Where(a => a.TimeCostMinutes > 0 && a.TimePolicy != null && a.TimePolicy.OncePerDay)
                .Select(a => new Option { Id = a.Id, Try = (g, r) => g.PerformLocationActivity(a.Id, r).Kind == LifeActivityResultKind.Applied })
                .ToList();

        private static List<Option> Location(bool skipOncePerDay)
            => TestWorld.Definitions.All<LocationActivity>()
                .Where(a => a.TimeCostMinutes > 0 && !(skipOncePerDay && a.TimePolicy != null && a.TimePolicy.OncePerDay))
                .Select(a => new Option { Id = a.Id, Try = (g, r) => g.PerformLocationActivity(a.Id, r).Kind == LifeActivityResultKind.Applied })
                .ToList();

        private static List<Option> Outside()
            => TestWorld.Definitions.All<OutsideSchoolActivity>().Where(a => a.TimeCostMinutes > 0)
                .Select(a => new Option { Id = a.Id, Try = (g, r) => g.PerformOutsideSchoolActivity(a.Id, r).Kind == LifeActivityResultKind.Applied })
                .ToList();

        private static List<Option> Help()
            => TestWorld.Definitions.All<TownHelpAction>().Where(a => a.TimeCostMinutes > 0)
                .Select(a => new Option { Id = a.Id, Try = (g, r) => g.PerformTownHelp(a.Id, r).Kind == LifeActivityResultKind.Applied })
                .ToList();

        private static List<Option> PartTimeWork()
            => TestWorld.Definitions.All<IMinigamePartTimeWork>().Where(w => w.TimeCostMinutes > 0 && w.Minigame != null)
                .Select(w => new Option { Id = w.Id, Try = (g, r) => WithMinigame(g, g.BeginPartTimeWork(w.Id, r, 1), r, w.Minigame) })
                .ToList();

        private static List<Option> Practice()
            => TestWorld.Definitions.All<CareerPractice>().Where(p => p.Minigame != null && p.Activity != null && p.Activity.TimeCostMinutes > 0)
                .Select(p => new Option { Id = p.Id, Try = (g, r) => WithMinigame(g, g.BeginPractice(p.Id, r, 1), r, p.Minigame) })
                .ToList();

        private static bool WithMinigame(GameSession game, Rootborn.Core.Careers.AdultCareerResult begun, string request, Minigame minigame)
        {
            if (!begun.Applied) return false;
            PlayMinigame(game, minigame);
            Assert.That(game.SettleMinigame(request, succeeded: true).Applied, Is.True, "미니게임 정산: " + request);
            return true;
        }

        private static string Reason(GameSession game)
        {
            if (game.IsExhausted) return "지침(체력 0)";
            if (game.Student.Energy <= 1) return "체력";
            if (game.Student.Focus <= 1) return "집중";
            return "조건(할 수 있는 활동 없음)";
        }

        private static Balance ReadBalance()
        {
            var path = Path.Combine(TestWorld.RepoRoot, "game", "tests", "Rootborn.Game.Tests", "day-balance.json");
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var b = document.RootElement;
            var range = b.GetProperty("bigActivitiesPerDay");
            var stop = b.GetProperty("bodyMayStopFrom").GetString().Split(':');
            return new Balance
            {
                Min = range.GetProperty("min").GetInt32(),
                Max = range.GetProperty("max").GetInt32(),
                BodyMayStopFrom = int.Parse(stop[0]) * 60 + int.Parse(stop[1]),
                TravelMinutes = b.GetProperty("travelMinutesPerActivity").GetInt32(),
                MinigameShare = b.GetProperty("minigamePlayShareOfTimeLimit").GetSingle(),
            };
        }

        private static string Clock(int minutes)
        {
            int day = minutes / (24 * 60), clock = minutes % (24 * 60);
            return (day > 0 ? "+" + day + "일 " : "") + (clock / 60).ToString("00") + ":" + (clock % 60).ToString("00");
        }
    }
}
