using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class NetworkWorldTimeStateSourceTests
    {
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
            StringAssert.Contains("worldTime.WeekdayName", source);
            StringAssert.Contains("worldTime.TimeOfDay", source);
            StringAssert.Contains("worldTime.SchedulePhase", source);
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
