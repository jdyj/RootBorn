using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class DirectValidationAutoplaySourceTests
    {
        private const string AutoplayPath = "Assets/Scripts/Game/Common/DirectValidationAutoplayInstaller.cs";

        [Test]
        public void MULTI_DIRECT_PostIdentityAutoplayDrivesInputSystemAndDoesNotSetDomainStateDirectly()
        {
            Assert.IsTrue(File.Exists(AutoplayPath), "Direct validation autoplay installer must exist for post-identity executable activity/day-end capture.");
            string source = File.ReadAllText(AutoplayPath);

            StringAssert.Contains("-directValidationAutoplay", source);
            StringAssert.Contains("InputSystem.QueueStateEvent", source);
            StringAssert.Contains("KeyboardState", source);
            StringAssert.Contains("PlayerInteractionRouter", source);
            StringAssert.Contains("StudyBasicsActivity", source);
            StringAssert.Contains("StudentDayEndBoard", source);
            StringAssert.Contains("DirectValidationTrace.Log", source);

            Assert.IsFalse(source.Contains("CurrentDay ="), "Autoplay must not set world/student day directly.");
            Assert.IsFalse(source.Contains("TimeOfDay ="), "Autoplay must not set time of day directly.");
            Assert.IsFalse(source.Contains("TryEndDay("), "Autoplay must request day end through interact input, not call domain end-day directly.");
            Assert.IsFalse(source.Contains("AddRelationship("), "Autoplay must not mutate relationship state directly.");
            Assert.IsFalse(source.Contains("AddStatus("), "Autoplay must not mutate status state directly.");
            Assert.IsFalse(source.Contains("RecordActivityCompleted"), "Autoplay must not record activity completion directly.");
        }

        [Test]
        public void MULTI_TIME_004_AutoplayCanDelayStartUntilAllClientsAreConnected()
        {
            Assert.IsTrue(File.Exists(AutoplayPath), "Direct validation autoplay must support coordinated multi-client timing.");
            string source = File.ReadAllText(AutoplayPath);

            StringAssert.Contains("-directValidationAutoplayDelaySeconds", source);
            StringAssert.Contains("ReadDelaySeconds()", source);
            StringAssert.Contains("autoplay delaying seconds=", source);
            StringAssert.Contains("WaitForSecondsRealtime", source);
        }

        [Test]
        public void MULTI_TIME_004_AutoplayKeepsLocalPlayerAcrossDelayedStart()
        {
            Assert.IsTrue(File.Exists(AutoplayPath), "Delayed autoplay must not lose the already resolved local player after scene sync settles.");
            string source = File.ReadAllText(AutoplayPath);

            StringAssert.Contains("WaitForLocalInputPlayer", source);
            StringAssert.Contains("yield return WaitForLocalInputPlayer", source);
            StringAssert.Contains("if (identity == null)", source);
        }

        [Test]
        public void MULTI_DIRECT_RelationshipConditionAutoplayLogsReadOnlyProgressSnapshot()
        {
            Assert.IsTrue(File.Exists(AutoplayPath), "Direct validation autoplay must expose read-only relationship/condition evidence.");
            string source = File.ReadAllText(AutoplayPath);

            StringAssert.Contains("StudentProgressSnapshot", source);
            StringAssert.Contains("autoplay student progress snapshot player=", source);
            StringAssert.Contains("GetRelationshipIds()", source);
            StringAssert.Contains("GetStatusIds()", source);
            StringAssert.Contains("GetRelationshipValueById", source);
            StringAssert.Contains("GetStatusValueById", source);
        }
    }
}
