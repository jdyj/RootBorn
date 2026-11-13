using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>덮어쓰기 폴더로 수치 하나를 바꾸면 균형 결과 숫자가 달라지고 보고서 맨 위에 적힌다. 자동 테스트(가정을 넣은 계산)다.</summary>
    [NonParallelizable] // TestWorld의 정의를 바꾼다
    public sealed class DataOverlayBalanceTests
    {
        private const string Work = "work.clinic-helper";

        private static long Earned()
        {
            var config = BalanceConfig.Read();
            config.Validate();
            var strategy = config.Strategies.First(s => s.Id == "one-part-time-work");
            var target = RunTargets.All("part-time-work", TestWorld.Definitions).First(t => RunTargets.IdOf(t) == Work);
            var run = BalanceRunner.Run(strategy, target, RunSetup.Baseline(config), config, 3);
            return run.Days.Sum(d => (long)d.Earned);
        }

        [Test]
        public void OverlayChangingTheReward_ChangesTheEarnedNumber_AndTheReportSaysSo()
        {
            long before = Earned();
            Assert.That(TestWorld.OverlayChanges, Is.Empty, "덮어쓰기 없이는 목록이 비어 있다");
            var temp = Path.Combine(Path.GetTempPath(), "rootborn-overlay-balance-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(temp, "overlay", "studentlife"));
                File.WriteAllText(Path.Combine(temp, "overlay", "studentlife", "PartTimeWorkDefinition.json"),
                    @"[{""type"":""PartTimeWorkDefinition"",""id"":""" + Work + @""",""rewardCounts"":""0a000000""}]");

                using (TestWorld.UseOverlay(Path.Combine(temp, "overlay")))
                {
                    long after = Earned();
                    Assert.That(after, Is.GreaterThan(before), "보상을 5에서 10으로 올렸다");
                    Assert.That(TestWorld.OverlayChanges.Single().Field, Is.EqualTo("rewardCounts"));

                    var config = BalanceConfig.Read();
                    BalanceReport.Write(temp, config, 3, BalanceRunner.RunAll(config, 3).Take(1).ToList());
                    var text = File.ReadAllText(Path.Combine(temp, BalanceReport.ReportFile));
                    Assert.That(text, Does.Contain(BalanceReport.OverlayLine));
                    Assert.That(text, Does.Contain("PartTimeWorkDefinition/" + Work + ".rewardCounts"));
                    Assert.That(text.IndexOf(BalanceReport.OverlayLine, StringComparison.Ordinal), Is.LessThan(text.IndexOf(BalanceReport.NotPlayLine, StringComparison.Ordinal)), "맨 위");
                }

                Assert.That(Earned(), Is.EqualTo(before), "덮어쓰기를 끝내면 원래 숫자로 돌아온다");
                Assert.That(TestWorld.OverlayChanges, Is.Empty);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch (IOException) { }
            }
        }
    }
}
