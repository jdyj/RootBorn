using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 손으로만 도는 모든 경우 훑기: money-balance.json "axes"의 곱을 axes.days일 병렬로 돌려 sweep.csv·sweep-report.md를 쓴다.
    /// 폴더는 ROOTBORN_BALANCE_OUT(없으면 &lt;저장소&gt;/output/balance) 아래 sweep. 표는 가정을 넣은 계산이다.
    /// 끝까지 돌지 못한 판이 있으면 파일을 다 쓴 뒤 실패한다(보고서 맨 위와 failed 칸에 까닭).
    /// </summary>
    public sealed class FullSweep
    {
        private const int ProgressSteps = 50;   // 진행 알림 횟수(원격 로그에 2%마다 한 줄)

        [Test, Explicit("손으로만: 모든 조합을 돌려 output/balance/sweep 에 표를 쓴다"), Category("BalanceReport")]
        public void WriteSweepReport()
        {
            var config = BalanceConfig.Read();
            config.Validate();
            int runs = SweepRunner.Plan(config).Count;
            int parallel = Environment.ProcessorCount;
            TestContext.Out.WriteLine("훑기: 판 " + runs + "개 × " + config.Axes.Days + "일, 동시 " + parallel);

            var watch = Stopwatch.StartNew();
            int step = Math.Max(1, runs / ProgressSteps);
            var rows = SweepRunner.RunAll(config, parallel, (done, total) =>
            {
                if (done % step == 0 || done == total)
                    TestContext.Progress.WriteLine("훑기 진행: 판 " + done + "/" + total + ", " + watch.Elapsed.TotalSeconds.ToString("0") + "초");
            });
            var ran = watch.Elapsed;
            string directory = SweepReport.OutputDirectory();
            SweepReport.Write(directory, config, rows);
            watch.Stop();

            var csv = new FileInfo(Path.Combine(directory, SweepReport.CsvFile));
            int failed = rows.Where(r => r.Failure != null).Select(r => r.StrategyId + "\n" + r.TargetId + "\n" + r.Setup.Id).Distinct().Count();
            TestContext.Out.WriteLine("판 " + runs + "개, 줄 " + rows.Count + "개, 돌기 " + ran.TotalSeconds.ToString("0.0") + "초, 쓰기까지 " + watch.Elapsed.TotalSeconds.ToString("0.0")
                + "초, 판 하나 평균 " + (ran.TotalSeconds * parallel / Math.Max(1, runs)).ToString("0.000") + "초(코어 하나 기준), sweep.csv " + (csv.Length / 1024.0 / 1024.0).ToString("0.0")
                + "MB, 실패한 판 " + failed + "개, 폴더 " + directory);
            Assert.That(failed, Is.Zero, "끝까지 돌지 못한 판이 있다 — " + Path.Combine(directory, SweepReport.ReportFile) + " 맨 위");
        }
    }
}
