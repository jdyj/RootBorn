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
            // 고친 것: SIM-001 ScreenStack 매 프레임 할당(FoundDefectRegressionTests), SIM-002 PlayerDriver 긴 문(DriverDoorReproTests).
            new Defect
            {
                Id = "SIM-003",
                What = "Host WorldScreen.RefreshInteractions가 매 프레임 지역 함수 LocationOf가 잡는 객체(약 40바이트)를 새로 만든다(매 프레임 새 객체 금지 위반)",
                Seed = "코드를 읽어 찾음: 창 없는 몸 HeadlessWorld.Refresh의 같은 모양이 가만히 선 프레임마다 40바이트였고, 지역 함수를 바뀔 때만 도는 메서드로 옮기자 0이 됐다. "
                    + "Host 창 루프는 그리기도 할당해 이것만 따로 재는 재현 테스트는 없다",
                Cause = "game/Rootborn.Host/World/WorldScreen.cs RefreshInteractions: day·slot을 잡는 지역 함수가 맵·시간대가 같은 프레임에도 메서드에 들어올 때마다 객체를 만든다",
                Matches = _ => false,
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
