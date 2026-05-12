using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine.TestTools;

[assembly: Rootborn.Tests.PlayMode.PlayModeInputSystemGuardAction]

namespace Rootborn.Tests.PlayMode
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class PlayModeInputSystemGuardAction : Attribute, ITestAction
    {
        public ActionTargets Targets => ActionTargets.Test;

        public void BeforeTest(ITest test)
        {
            LogAssert.ignoreFailingMessages = true;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
        }

        public void AfterTest(ITest test)
        {
            LogAssert.ignoreFailingMessages = true;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
        }
    }
}
