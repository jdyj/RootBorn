using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeTraitActivitySourceAuditTests
    {
        [Test]
        public void LIFE_TRAIT_ACTIVITY_008_RuntimeExecutionDoesNotBranchByEntityIds()
        {
            string core = File.ReadAllText("Assets/Scripts/Game/StudentLife/StudentLifeCore.cs");
            string scene = File.ReadAllText("Assets/Scripts/Game/StudentLife/StudentLifeSceneInteraction.cs");

            AssertNoEntityIdBranch(core);
            AssertNoEntityIdBranch(scene);
        }

        private static void AssertNoEntityIdBranch(string source)
        {
            StringAssert.DoesNotContain("traitId ==", source);
            StringAssert.DoesNotContain("activityId ==", source);
            StringAssert.DoesNotContain("choiceId ==", source);
            StringAssert.DoesNotContain("switch (traitId", source);
            StringAssert.DoesNotContain("switch (activityId", source);
            StringAssert.DoesNotContain("switch (choiceId", source);
        }
    }
}
