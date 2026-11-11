using System.Collections.Generic;
using System.IO;

namespace Rootborn.Simulation
{
    /// <summary>
    /// 기록 재생 도우미: output/simulation/&lt;이름&gt;/play.txt의 행동 이름을 차례로 다시 고른다(하루 마감 순서와 시작 순서도 기록에 든 그대로).
    /// 같은 몸 종류(창 없는 몸이면 창 없는 몸)에서 같은 플레이가 된다. 어긋나면 그 걸음과 그때 할 수 있던 것이 실패 보고에 나온다.
    /// </summary>
    public static class PlayReplay
    {
        public static PlayResult Run(IPlayBody body, string playFile, PlayAtlas atlas, string name)
        {
            if (!File.Exists(playFile)) throw new FileNotFoundException("기록이 없다", playFile);
            List<string> keys = PlayRecord.ReadKeys(playFile);
            (string policy, int seed) = PlayRecord.ReadHeader(playFile);
            var settings = new PlaySettings { Days = 1000, MaxSteps = keys.Count + 1, StepsPerDay = int.MaxValue };
            var runner = new PlayRunner(body, new ReplayPolicy(keys), seed, settings, atlas, name, pace: false);
            PlayResult result = runner.Run();
            result.Notes.Add("재생한 기록: " + playFile + " (정책 " + policy + ", 씨앗 " + seed + ", 행동 " + keys.Count + ")");
            return result;
        }
    }
}
