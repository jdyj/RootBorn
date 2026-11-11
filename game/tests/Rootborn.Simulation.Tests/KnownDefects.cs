using System;
using NUnit.Framework;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 시뮬레이션이 찾았지만 원인이 이 작업 범위 밖(게임 코드, 다른 세션이 고치는 중)이라 여기서 고치지 않은 결함.
    /// 각 결함은 가장 작은 재현 테스트(KnownDefectReproTests)가 건너뜀 없이 지금 동작을 고정한다. 고쳐지면 그 테스트가 실패하므로
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
            new Defect
            {
                Id = "SIM-001",
                What = "ScreenStack.Tick이 매 프레임 약 64바이트, 가구 방에서 ScreenStack.EnterMap이 약 32바이트를 새로 잡는다(매 프레임 새 객체 금지 위반)",
                Seed = "아무 판의 가만히 선 프레임(KnownDefectReproTests.SIM001_ScreenStackTickAndEnterMap_AllocateEveryFrame)",
                Cause = "game/Rootborn.Screens/ScreenStack.cs Tick의 Snapshot().ToArray(), SameFeatures의 IReadOnlyCollection foreach",
                Matches = _ => false,
            },
            new Defect
            {
                Id = "SIM-002",
                What = "테스트 도우미 PlayerDriver.WalkThroughDoor가 맵 가장자리를 따라 긴 문(마을 → 해변, 1408x45px)의 끝 쪽에서 문 가운데 쪽(옆)으로 키를 눌러 문에 닿지 못한다",
                Seed = "Host.Tests DriverDoorReproTests.SIM002_PlayerDriverWalkThroughDoor_MissesTheWideBeachDoorFromItsEnd(마을 부엌 앞에서 해변 문 왼쪽 끝으로)",
                Cause = "game/tests/Rootborn.Host.Tests/PlayerDriver.cs WalkThroughDoor의 방향(문 가운데 기준). 시뮬레이션은 DoorDirection(가장 가까운 가장자리)을 쓴다",
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
