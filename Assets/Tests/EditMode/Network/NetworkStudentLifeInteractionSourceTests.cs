using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class NetworkStudentLifeInteractionSourceTests
    {
        [Test]
        public void MULTI_TIME_002_ActivityInteractorLogsAppliedResultForDirectPlayEvidence()
        {
            const string sourcePath = "Assets/Scripts/Game/StudentLife/StudentLifeSceneInteraction.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Direct multiplayer validation needs observable activity-interaction evidence without forcing domain state in tests.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("Student life activity interact", source);
            StringAssert.Contains("result.Kind", source);
            StringAssert.Contains("result.ActivityId", source);
            StringAssert.Contains("result.PlayerId", source);
        }

        [Test]
        public void MULTI_TIME_008_DayEndInteractorLogsPersonalSummaryBesideSharedTimeRequest()
        {
            const string sourcePath = "Assets/Scripts/UI/StudentLife/StudentDayEndInteractor.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Direct multiplayer validation needs personal day-result evidence separate from shared world-time transition.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("Student day result", source);
            StringAssert.Contains("summary.CompletedActivityIds", source);
            StringAssert.Contains("progress.PlayerId", source);
            StringAssert.Contains("NetworkWorldTimeState.TryRequestDayEndReady", source);
        }
    }
}
