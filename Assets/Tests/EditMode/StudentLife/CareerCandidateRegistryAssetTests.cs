using NUnit.Framework;
using Rootborn.Game.Common;
using UnityEditor;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class CareerCandidateRegistryAssetTests
    {
        [Test]
        public void CAREER_CANDIDATE_EDIT_010_ProjectRegistryContainsFourCandidateAssetsAndRoutes()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.IsNotNull(registry, "CAREER-CANDIDATE-EDIT-010 failed: project registry asset is missing.");
            Assert.GreaterOrEqual(registry.CareerCandidates.Length, 4, "CAREER-CANDIDATE-EDIT-010 failed: registry needs at least four career candidates.");
            Assert.GreaterOrEqual(registry.CareerHints.Length, 4, "CAREER-CANDIDATE-EDIT-010 failed: registry needs at least four career hints.");
            Assert.GreaterOrEqual(registry.CareerCandidateRoutes.Length, 5, "CAREER-CANDIDATE-EDIT-010 failed: registry needs alternative route coverage.");
            bool hasAlternativeRouteCandidate = false;
            for (int i = 0; i < registry.CareerCandidates.Length; i++)
            {
                var candidate = registry.CareerCandidates[i];
                if (candidate != null && candidate.Routes.Count >= 2) hasAlternativeRouteCandidate = true;
            }
            Assert.IsTrue(hasAlternativeRouteCandidate, "CAREER-CANDIDATE-EDIT-010 failed: no candidate has two or more routes.");
        }
    }
}
