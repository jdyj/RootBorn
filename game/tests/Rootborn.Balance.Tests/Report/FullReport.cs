using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 손으로만 도는 전체 보고: 설정의 모든 판을 기본 날 수로 돌려 파일 셋을 쓴다.
    /// 날 수는 ROOTBORN_BALANCE_DAYS, 폴더는 ROOTBORN_BALANCE_OUT(없으면 &lt;저장소&gt;/output/balance). 표는 가정을 넣은 계산이다.
    /// </summary>
    public sealed class FullReport
    {
        [Test, Explicit("손으로만: 전체 판을 돌려 output/balance 에 표를 쓴다"), Category("BalanceReport")]
        public void WriteFullReport()
        {
            var watch = Stopwatch.StartNew();
            var config = BalanceConfig.Read();
            config.Validate();
            int days = BalanceReport.Days(config);
            var runs = BalanceRunner.RunAll(config, days).ToList();
            string directory = BalanceReport.OutputDirectory();
            BalanceReport.Write(directory, config, days, runs);
            watch.Stop();
            TestContext.Out.WriteLine("균형 보고: 판 " + runs.Count + "개 × " + days + "일, " + watch.Elapsed.TotalSeconds.ToString("0.0") + "초, 폴더 " + directory);
        }
    }
}
