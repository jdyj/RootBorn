using System;
using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class DedicatedResourceRecipeActivityAutoplayContractTests
    {
        private const string SourcePath = "Assets/Scripts/Game/Common/DirectValidationResourceRecipeActivityAutoplayInstaller.cs";
        private const string SmokeScriptPath = "Scripts/qa/run-dedicated-resource-recipe-activity-smoke.ps1";
        private const string LinkXmlPath = "Assets/Linker/link.xml";

        [Test]
        public void DEDICATED_RESOURCE_RECIPE_EDIT_001_AutoplayInstallerDefinesActionObserveScreenshotAndEvidenceContracts()
        {
            Assert.IsTrue(File.Exists(SourcePath), "Dedicated resource/recipe activity autoplay installer must exist.");
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains("-directValidationAutoplayResourceRecipeActivity", source);
            StringAssert.Contains("-directValidationAutoplayResourceRecipeActivityObserveOnly", source);
            StringAssert.Contains("-directValidationScreenshot", source);
            StringAssert.Contains("autoplay completed resource-recipe-activity", source);
            StringAssert.Contains("autoplay completed resource-recipe-activity-observe", source);
            StringAssert.Contains("autoplay failed", source);
            StringAssert.Contains("recipe=recipe.town.field-notes revealed:true", source, "Smoke must log recipe reveal evidence.");
            StringAssert.Contains("resource=Rock seen:true", source, "Smoke must log resource sighting evidence.");
            StringAssert.Contains("journal=resource-recipe duplicateRows:0", source, "Smoke must log Objective Journal duplicate evidence.");
            StringAssert.Contains("hud=tracked duplicates:0", source, "Smoke must log TrackedObjectiveHud duplicate evidence.");
            StringAssert.Contains("save=resource-recipe exists:true", source, "Smoke must log saved-state evidence.");
            StringAssert.Contains("ScreenCapture.CaptureScreenshot", source);
            Assert.IsFalse(source.Contains("TryRevealRecipe("), "Dedicated autoplay must not reveal the recipe by direct progress mutation.");
            Assert.IsFalse(source.Contains("TryReveal("), "Dedicated autoplay must not reveal the recipe by direct progress mutation.");
            Assert.IsFalse(source.Contains("ResourceRecipeProgressPersistence.Save"), "Dedicated autoplay must not write resource/recipe save state directly.");
        }

        [Test]
        public void DEDICATED_RESOURCE_RECIPE_EDIT_002_SmokeScriptRunsActionAndObservePassesWithScreenshotsAndCleanup()
        {
            Assert.IsTrue(File.Exists(SmokeScriptPath), "Dedicated resource/recipe activity smoke must have a repository-level runner script.");
            string source = File.ReadAllText(SmokeScriptPath);

            StringAssert.Contains("-directValidationAutoplayResourceRecipeActivity", source);
            StringAssert.Contains("-directValidationAutoplayResourceRecipeActivityObserveOnly", source);
            StringAssert.Contains("network-dedicated-resource-recipe-activity-e2e.png", source);
            StringAssert.Contains("network-dedicated-resource-recipe-activity-e2e-reload.png", source);
            StringAssert.Contains("autoplay completed resource-recipe-activity", source);
            StringAssert.Contains("autoplay completed resource-recipe-activity-observe", source);
            StringAssert.Contains("Assert-Screenshot", source);
            StringAssert.Contains("Stop-TrackedProcesses", source);
            StringAssert.Contains("Assert-NoFatalLogs", source);
            StringAssert.Contains("recipe=recipe.town.field-notes revealed:true", source);
            StringAssert.Contains("resource=Rock seen:true", source);
            StringAssert.Contains("journal=resource-recipe duplicateRows:0", source);
            StringAssert.Contains("hud=tracked duplicates:0", source);
            StringAssert.Contains("save=resource-recipe exists:true", source);
            StringAssert.DoesNotContain("-directValidationAutoplay\"", source, "Dedicated resource/recipe smoke must not launch the generic autoplay runner.");
        }

        [Test]
        public void DEDICATED_RESOURCE_RECIPE_EDIT_003_SmokeScriptFailsBeforeLaunchOnStaleArtifacts()
        {
            Assert.IsTrue(File.Exists(SmokeScriptPath), "Dedicated resource/recipe activity smoke must have a repository-level runner script.");
            string source = File.ReadAllText(SmokeScriptPath);

            StringAssert.Contains("Assert-FreshBuildArtifacts", source, "Dedicated resource/recipe smoke must fail before process launch if build artifacts are stale or incomplete.");
            StringAssert.Contains("Assert-RequiredArtifact", source, "Dedicated resource/recipe smoke must validate required executable, managed assembly, and Addressables runtime files.");
            StringAssert.Contains("rootborn-server_Data\\Managed\\Rootborn.Game.dll", source);
            StringAssert.Contains("rootborn_Data\\Managed\\Rootborn.Game.dll", source);
            StringAssert.Contains("rootborn-server_Data\\StreamingAssets\\aa\\settings.json", source);
            StringAssert.Contains("rootborn_Data\\StreamingAssets\\aa\\settings.json", source);
            StringAssert.Contains("DirectValidationResourceRecipeActivityAutoplayInstaller.cs", source, "Freshness guard must compare artifacts against the autoplay source required by this runner.");
            StringAssert.Contains("run-dedicated-resource-recipe-activity-smoke.ps1", source, "Freshness guard failure output must name the runner script.");
            StringAssert.DoesNotContain("run-dedicated-resource-recipe-activity-smoke.ps1", ExtractRequiredSourcesBlock(source), "Freshness guard must not compare managed DLL artifacts against the runner script itself.");
            StringAssert.Contains("BuildServerWindows64", source, "Freshness failure output must name the server build remediation command.");
            StringAssert.Contains("BuildClientWindows64", source, "Freshness failure output must name the client build remediation command.");
        }

        [Test]
        public void DEDICATED_RESOURCE_RECIPE_EDIT_004_LinkXmlPreservesDedicatedAutoplayInstaller()
        {
            Assert.IsTrue(File.Exists(LinkXmlPath), "Addressables link.xml must exist.");
            string source = File.ReadAllText(LinkXmlPath);
            StringAssert.Contains("Rootborn.Game.Common.DirectValidationResourceRecipeActivityAutoplayInstaller", source);
        }

        private static string ExtractRequiredSourcesBlock(string source)
        {
            const string Marker = "$requiredSources = @(";
            int start = source.IndexOf(Marker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Freshness guard must define required source files.");
            int end = source.IndexOf(")", start, StringComparison.Ordinal);
            Assert.Greater(end, start, "Freshness guard required source block must be closed.");
            return source.Substring(start, end - start);
        }
    }
}
