using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class ManagersBootstrapConcurrencySourceTests
    {
        private const string ManagersPath = "Assets/Scripts/Game/Managers/Managers.cs";

        [Test]
        public void BootstrapAsync_SharesInFlightTaskAcrossConcurrentCallers()
        {
            string source = File.ReadAllText(ManagersPath);

            StringAssert.Contains("private static Task s_bootstrapTask", source);
            StringAssert.Contains("!s_bootstrapTask.IsCompleted", source);
            StringAssert.Contains("BootstrapInternalAsync", source);
        }
    }
}
