using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class TownConceptSourceAuditTests
    {
        [Test]
        public void ModernSocietyActivityKinds_DoNotExposeFarmHelp()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/ModernSociety/ModernActivityKind.cs");
            StringAssert.DoesNotContain("FarmHelp", source);
        }

        [Test]
        public void TownConceptDocs_RecordFarmResidueAudit()
        {
            Assert.IsTrue(File.Exists("docs/art/town-concept-audit.md"));
            string audit = File.ReadAllText("docs/art/town-concept-audit.md");
            StringAssert.Contains("Farm Residue Classes", audit);
            StringAssert.Contains("Style1 To Style2 UI Decision", audit);
        }

        [Test]
        public void ConstitutionAndTestingDiscipline_RequirePlayerOperatedScenarioTests()
        {
            string agents = File.ReadAllText("AGENTS.md");
            string constitution = File.ReadAllText(".claude/constitution.md");
            string testingDiscipline = File.ReadAllText(".claude/rules/testing-discipline.md");

            StringAssert.Contains("플레이어 대리 테스트", agents);
            StringAssert.Contains("플레이어 대리 테스트", constitution);
            StringAssert.Contains("플레이어 대리 테스트", testingDiscipline);
            StringAssert.Contains("마우스/키보드/게임패드 입력", testingDiscipline);
            StringAssert.Contains("완료 판정은 실제 플레이 경로 테스트", testingDiscipline);
        }
    }
}
