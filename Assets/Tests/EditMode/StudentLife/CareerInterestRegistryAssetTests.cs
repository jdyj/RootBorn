using NUnit.Framework;
using Rootborn.Game.Common;
using UnityEditor;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class CareerInterestRegistryAssetTests
    {
        [Test]
        public void CAREER_INTEREST_EDIT_001_ProjectRegistryContainsDataDrivenInterestAssets()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.IsNotNull(registry, "CAREER-INTEREST-EDIT-001 failed: project registry asset is missing.");
            Assert.GreaterOrEqual(registry.CareerInterests.Length, 4, "CAREER-INTEREST-EDIT-001 failed: registry needs at least four career interests.");
            bool hasOutsideSchoolRoute = false;
            for (int i = 0; i < registry.CareerInterests.Length; i++)
            {
                var interest = registry.CareerInterests[i];
                Assert.IsNotNull(interest, "CAREER-INTEREST-EDIT-001 failed: registry contains a null career interest.");
                Assert.IsNotNull(interest.Candidate, interest.Id + " must point at a career candidate.");
                Assert.GreaterOrEqual(interest.UnlockRequirements.Count, 1, interest.Id + " must define unlock requirements as SO strategies.");
                Assert.GreaterOrEqual(interest.SelectionRules.Count, 1, interest.Id + " must define selection rules as SO strategies.");
                Assert.GreaterOrEqual(interest.Recommendations.Count, 1, interest.Id + " must define recommendations as SO strategies.");
                if (interest.RelatedLocations.Count > 0 && interest.RelatedActivities.Count > 0) hasOutsideSchoolRoute = true;
            }
            Assert.IsTrue(hasOutsideSchoolRoute, "CAREER-INTEREST-EDIT-001 failed: no career interest points at related location/activity data.");
        }
    }
}
