using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rootborn.Game;
using Rootborn.Screens;
using Rootborn.Simulation.Headless;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 실제 10분 하루(배속 1, 고정 프레임 1/60초)에서 자동 플레이어의 하루가 어떻게 흐르는지 기록한다: 몇 시에 무엇을 했고 체력·집중이 얼마였는지,
    /// 언제부터 할 것이 없어졌는지, 몇 시에 잤는지. 걸음 상한을 크게 두어 잘 시간·지침(HUD에 보이는 것)이 하루를 끝내게 한다.
    /// 기록은 output/simulation/day-flow-&lt;정책&gt;/day-flow.md. 수치를 고르지 않는다 — 균형은 사용자가 정한다. 자동 테스트(창 없음).
    /// </summary>
    [TestFixture, NonParallelizable]
    public sealed class DayFlowTests
    {
        [TestCase(PlayPolicyCatalog.OffSchool)]
        [TestCase(PlayPolicyCatalog.MainChain)]
        public void ARealTenMinuteDay_EndsWithSleepAtBedtimeOrWhenExhausted(string policy)
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            using var body = new HeadlessBody(atlas);
            var settings = new PlaySettings { Days = 1, StepsPerDay = 5000, MaxSteps = 5000, WindDownSteps = 120 };
            var runner = new PlayRunner(body, PlayPolicyCatalog.Default().Create(policy, atlas), 1, settings, atlas, "day-flow-" + policy);
            var rows = new List<string>();
            int lastActivities = 0, firstRefusedAt = -1;
            runner.AfterStep += (step, action) =>
            {
                GameSession s = body.Session;
                if (s == null || s.StudentDay != 1) return;
                string[] today = s.Student.GetTodayActivityIds();
                if (today.Length > lastActivities)
                {
                    for (int i = lastActivities; i < today.Length; i++)
                        rows.Add("| " + Clock(s.StudentTimeMinutes) + " | " + today[i] + " | " + s.Student.Energy + " | " + s.Student.Focus + " |");
                    lastActivities = today.Length;
                }
                else if (firstRefusedAt < 0 && (s.IsExhausted || s.Student.Focus == 0)) firstRefusedAt = s.StudentTimeMinutes;
            };
            PlayResult result = runner.Run();
            Assert.That(result.Failure, Is.Null, result.Failure?.What);
            PlayStep sleep = result.Steps.LastOrDefault(st => st.Day == 1 && st.By == "day-end");
            var md = new StringBuilder();
            md.AppendLine("# 실제 10분 하루 — 정책 " + policy + ", 씨앗 1");
            md.AppendLine();
            md.AppendLine("| 끝난 시각 | 오늘 기록(활동·방문) | 체력 | 집중 |");
            md.AppendLine("|---|---|---|---|");
            foreach (string r in rows) md.AppendLine(r);
            md.AppendLine();
            md.AppendLine("- 체력·집중이 바닥난 첫 시각: " + (firstRefusedAt < 0 ? "(없음)" : Clock(firstRefusedAt)));
            md.AppendLine("- 하루 마감 순서를 시작한 걸음의 시각(잠자리로 간다): " + (sleep.Key == null ? "(없음)" : Clock(sleep.Minutes)));
            md.AppendLine("- 걸음 " + result.Steps.Count + ", 프레임 " + result.Frames + ", 메모: " + string.Join(" / ", result.Notes));
            string dir = Path.Combine(atlas.RepoRoot, "output", "simulation", "day-flow-" + policy);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "day-flow.md"), md.ToString());
            TestContext.WriteLine(md.ToString());
            Assert.That(result.DaysCompleted, Is.GreaterThanOrEqualTo(1), "하루를 넘겼다(잤다)");
        }

        private static string Clock(int minutes)
        {
            int m = ((minutes % 1440) + 1440) % 1440;
            return (m / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (m % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
