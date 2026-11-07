using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiSourceAuditTests
    {
        [Test]
        public void ReconstructionAudit_DocumentsReplacedDeferredAndVerification()
        {
            string audit = File.ReadAllText("docs/art/modern-ui-reconstruction-audit.md");
            StringAssert.Contains("## Replaced", audit);
            StringAssert.Contains("## Deferred", audit);
            StringAssert.Contains("## Verification", audit);
        }
    }
}
