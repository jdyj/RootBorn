using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Save;
using Rootborn.Game;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// SIM-004 재현(고치지 않음 — 데이터·기획 결정): 비용이 0이고 하루 한 번 제한이 없는 장소 활동(시간 정책 free-interaction)은
    /// 시간·체력·집중을 쓰지 않고 몇 번이고 다시 할 수 있고, 할 때마다 활동 기록이 늘고 결과(진로 진행 +1)가 다시 붙는다.
    /// 자동 플레이어가 이 줄을 4,947번 눌렀다(2026-11-13 속도 보고, 학교 밖 정책 4일째). 이 테스트는 지금 동작을 고정한다 —
    /// 횟수 제한이 생기면 실패하므로 그때 이 테스트와 KnownDefects의 SIM-004를 함께 지운다.
    /// 실제 데이터의 id를 쓴다(테스트는 데이터 id로 확인해도 된다).
    /// </summary>
    public sealed class FreeRepeatableActivityReproTests
    {
        public const string Activity = "opening.introduce.restaurant";

        [Test]
        public void SIM004_AFreeActivity_RepeatsWithoutTimeOrCost_AndKeepsAddingToTheLog()
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            string root = Path.Combine(Path.GetTempPath(), "rootborn-sim004-" + Guid.NewGuid().ToString("N"));
            try
            {
                GameSession s = GameSession.Start(atlas.Definitions, GameStartSettings.FromDefinitions(atlas.Definitions), new FileSaveStore(root), "slot-0", null);
                int minutes = s.StudentTimeMinutes, energy = s.Student.Energy, log = s.Student.GetActivityLogIds().Length;
                for (int i = 0; i < 5; i++)
                    Assert.That(s.PerformLocationActivity(Activity, "sim004-" + i).Kind, Is.EqualTo(LifeActivityResultKind.Applied), "다시 해도 받아들인다(" + i + ")");
                Assert.That(s.StudentTimeMinutes, Is.EqualTo(minutes), "시간이 들지 않는다");
                Assert.That(s.Student.Energy, Is.EqualTo(energy), "체력이 들지 않는다");
                Assert.That(s.Student.GetActivityLogIds().Length, Is.EqualTo(log + 5), "할 때마다 활동 기록이 는다(세이브도 커진다)");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
