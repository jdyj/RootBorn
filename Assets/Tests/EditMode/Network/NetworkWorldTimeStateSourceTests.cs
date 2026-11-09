using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class NetworkWorldTimeStateSourceTests
    {
        // A QA run with -saveRoot must not touch the player's own world-time file.
        [NUnit.Framework.Test]
        public void SaveRoot_AlsoHoldsWorldTime()
        {
            try
            {
                Rootborn.Game.Save.SaveService.SetRootDirectoryForTests("qa-root");
                NUnit.Framework.Assert.That(Rootborn.Game.Save.SaveService.WorldTimeDirectory(), NUnit.Framework.Is.EqualTo(System.IO.Path.Combine("qa-root", "world-time")));
                Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(null);
                NUnit.Framework.Assert.That(Rootborn.Game.Save.SaveService.WorldTimeDirectory(), NUnit.Framework.Is.EqualTo(System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "world-time")));
                NUnit.Framework.Assert.That(System.IO.File.ReadAllText("Assets/Scripts/Network/Time/NetworkWorldTimeState.cs"), NUnit.Framework.Does.Contain("SaveService.WorldTimeDirectory()"));
            }
            finally { Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(null); }
        }

        [Test]
        public void MULTI_TIME_003_DayEndAndTimeAdvanceAreServerRpcRequestsOnly()
        {
            string source = File.ReadAllText("Assets/Scripts/Network/Time/NetworkWorldTimeState.cs");

            StringAssert.Contains("[ServerRpc(RequireOwnership = false)]", source);
            StringAssert.Contains("public void RequestDayEndReadyServerRpc", source);
            StringAssert.Contains("public void AdvanceTimeOfDayServerRpc", source);
            StringAssert.Contains("if (!IsAuthoritativeServerInstance())", source);
            StringAssert.Contains("private void AdvanceToNextDay()", source);
            StringAssert.Contains("return IsServer && Active == this;", source);
        }

        [Test]
        public void MULTI_TIME_004_DayEndPolicyWaitsForAllConnectedClients()
        {
            string source = File.ReadAllText("Assets/Scripts/Network/Time/NetworkWorldTimeState.cs");

            StringAssert.Contains("_dayEndReadyClients.Add(clientId)", source);
            StringAssert.Contains("ConnectedClientsIds.Count", source);
            StringAssert.Contains("if (_dayEndReadyClients.Count >= connectedClients)", source);
            StringAssert.Contains("AdvanceToNextDay();", source);
            StringAssert.Contains("_dayEndReadyClients.Clear();", source);
        }

        [Test]
        public void MULTI_TIME_005_TimeHudPrefersServerWorldTimeBeforeLocalClock()
        {
            string source = File.ReadAllText("Assets/Scripts/UI/HUD/TimeHud.cs");

            int worldTimeIndex = source.IndexOf("NetworkWorldTimeState.Active", System.StringComparison.Ordinal);
            int localClockIndex = source.IndexOf("_clock.Day", System.StringComparison.Ordinal);

            Assert.GreaterOrEqual(worldTimeIndex, 0);
            Assert.GreaterOrEqual(localClockIndex, 0);
            Assert.Less(worldTimeIndex, localClockIndex);
            StringAssert.Contains("worldTime.CurrentDay", source);
            StringAssert.Contains("worldTime.WeekdayIndex", source);
            StringAssert.Contains("worldTime.TimeOfDay", source);
            // Players see day, weekday and part of day through localized keys, never the raw sync names.
            StringAssert.Contains("\"daily.time.shared-hud\"", source);
            StringAssert.Contains("\"daily.weekday.\"", source);
            StringAssert.Contains("\"daily.time-of-day.\"", source);
            StringAssert.DoesNotContain("worldTime.WeekdayName", source);
            StringAssert.DoesNotContain("+ _worldTime.SchedulePhase", source);
        }

        [Test]
        public void MULTI_TIME_009_SharedWorldTimeHoldsTheLocalClockAtItsPartOfDay()
        {
            // Lighting reads the local GameClock; shared world time must set it, or clients light the town by their own clock.
            string source = File.ReadAllText("Assets/Scripts/Network/Time/NetworkWorldTimeState.cs");
            StringAssert.Contains("WorldTimeHours.Representative((int)TimeOfDay)", source);
            StringAssert.Contains("clock.TimeScale = 0f", source);
            int spawn = source.IndexOf("public override void OnNetworkSpawn", System.StringComparison.Ordinal);
            int despawn = source.IndexOf("public override void OnNetworkDespawn", System.StringComparison.Ordinal);
            Assert.Greater(source.IndexOf("SyncLocalClock();", spawn, System.StringComparison.Ordinal), spawn, "spawn must set the local clock");
            Assert.Greater(source.IndexOf("ReleaseLocalClock();", despawn, System.StringComparison.Ordinal), despawn, "despawn must give the clock back");
            int timeHandler = source.IndexOf("private void HandleTimeChanged", System.StringComparison.Ordinal);
            Assert.Greater(source.IndexOf("SyncLocalClock();", timeHandler, System.StringComparison.Ordinal), timeHandler, "a new part of day must move the clock");
        }

        [Test]
        public void MULTI_TIME_008_DayEndInteractorRequestsWorldTimeAfterPersonalDayResult()
        {
            string source = File.ReadAllText("Assets/Scripts/UI/StudentLife/StudentDayEndInteractor.cs");

            int personalDayResultIndex = source.IndexOf("progress.TryEndDay", System.StringComparison.Ordinal);
            int networkReadyIndex = source.IndexOf("NetworkWorldTimeState.TryRequestDayEndReady", System.StringComparison.Ordinal);
            int resultPanelIndex = source.IndexOf("_resultPanel.Show", System.StringComparison.Ordinal);

            Assert.GreaterOrEqual(personalDayResultIndex, 0);
            Assert.GreaterOrEqual(networkReadyIndex, 0);
            Assert.GreaterOrEqual(resultPanelIndex, 0);
            Assert.Less(personalDayResultIndex, networkReadyIndex);
            Assert.Less(networkReadyIndex, resultPanelIndex);
        }
    }
}
