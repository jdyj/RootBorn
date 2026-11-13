using System;
using NUnit.Framework;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 시뮬레이션이 찾았지만 원인이 이 작업 범위 밖(게임 코드, 다른 세션이 고치는 중)이라 여기서 고치지 않은 결함.
    /// 각 결함은 가장 작은 재현 테스트가 건너뜀 없이 지금 동작을 고정한다. 고쳐지면 그 테스트가 실패하므로
    /// 그때 이 목록과 재현 테스트를 함께 지운다. 시뮬레이션 판은 이 목록의 실패를 "알려진 결함"으로 보고하고 통과시킨다 — 목록 밖의 실패는 실패다.
    /// </summary>
    public static class KnownDefects
    {
        public sealed class Defect
        {
            public string Id, What, Seed, Cause;
            public Func<PlayFailure, bool> Matches;
        }

        public static readonly Defect[] All =
        {
            // 판을 실패시키지 않는 결함(한계 값으로 받아 둔 것)도 보고를 위해 여기에 적는다. Matches가 false면 판 실패를 덮지 않는다.
            // 고친 것: SIM-001 ScreenStack 매 프레임 할당(FoundDefectRegressionTests), SIM-002 PlayerDriver 긴 문(DriverDoorReproTests),
            // SIM-003 Host WorldScreen 가만히 선 프레임의 클로저·열거자 할당(Host WorldFrameCostHostTests.WORLD_FRAME_001).
            new Defect
            {
                Id = "SIM-004",
                What = "비용 0·하루 한 번 제한 없는 장소 활동(시간 정책 free-interaction)을 시간·체력 없이 몇 번이고 다시 할 수 있고, 할 때마다 활동 기록이 늘고 결과(진로 진행 +1)가 다시 붙는다",
                Seed = "학교 밖 정책 씨앗 1, 4일째 장소 화면 첫 줄(2026-11-13 속도 보고에서 4,947번)",
                Cause = "데이터(LocationActivityDefinition의 비용 0 + ActivityTimePolicy oncePerDay 0) — 의도인지 기획 결정 대기. 재현 FreeRepeatableActivityReproTests.SIM004",
                Matches = _ => false, // 판을 실패시키지 않는다(정책은 같은 행동을 보이는 변화 없이 되풀이하면 그날 후보에서 뺀다)
            },
            new Defect
            {
                Id = "SIM-005",
                What = "대화가 떠 있는 채 걸어가 알바를 시작하면 미니게임이 대화 위에 뜨고, 그 미니게임 동안 혼자 하기의 시계가 서 있다(설계: 미니게임 중에는 흐른다)",
                Seed = "이것저것 유형 씨앗 1, 7일 판 4일째 964분(화면 쌓임 dialogue > minigame)",
                Cause = "대화는 이동을 막지 않는 모달이고 ScreenStack.PausesClock은 떠 있는 화면 중 하나라도 멈추면 멈춘다. 재현 MinigameUnderDialogueReproTests.SIM005",
                Matches = _ => false, // 판을 실패시키지 않는다(미니게임에서 보이는 것이 움직이면 변화로 본다)
            },
        };

        public static Defect Matches(PlayFailure failure)
        {
            if (failure == null) return null;
            foreach (Defect d in All) if (d.Matches(failure)) return d;
            return null;
        }

        public static void AssertNoNewFailure(PlayResult result)
        {
            if (result.Failure == null) return;
            Defect known = Matches(result.Failure);
            if (known != null)
            {
                TestContext.WriteLine("알려진 결함 " + known.Id + ": " + known.What + " (재현 씨앗 " + known.Seed + ", 원인 추정: " + known.Cause + ")");
                return;
            }
            Assert.Fail(result.Failure.What + "\n기록: " + result.OutputDir + "\n" + result.Failure.Report);
        }
    }
}
