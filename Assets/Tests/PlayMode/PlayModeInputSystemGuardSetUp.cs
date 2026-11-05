using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine.TestTools;

[assembly: Rootborn.Tests.PlayMode.PlayModeInputSystemGuardAction]

namespace Rootborn.Tests.PlayMode
{
    // New Game now opens the character creator. Scenario tests that are not about it press its real confirm button after a
    // short wait, so tests that confirm by themselves still go first. Tests about the creator opt out by name.
    internal sealed class PlayModeCreatorAutoConfirm : UnityEngine.MonoBehaviour
    {
        private const float WaitSeconds = 3f, CheckSeconds = 0.25f;
        private static PlayModeCreatorAutoConfirm s_instance;
        private float _nextCheck, _openSince = -1f;

        public static void ConfigureFor(string testName)
        {
            if (s_instance == null)
            {
                var go = new UnityEngine.GameObject("[PlayModeCreatorAutoConfirm]");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<PlayModeCreatorAutoConfirm>();
            }
            s_instance._openSince = -1f;
        }

        private static bool CurrentTestIsAboutCreator()
        {
            string name = TestContext.CurrentContext?.Test?.FullName ?? string.Empty;
            return name.Contains("Spum") || name.Contains("CharacterPart") || name.Contains("Creator");
        }

        private void Update()
        {
            float now = UnityEngine.Time.unscaledTime;
            if (now < _nextCheck) return;
            _nextCheck = now + CheckSeconds;
            if (CurrentTestIsAboutCreator()) return;
            var panel = UnityEngine.GameObject.Find("SpumCharacterCreatorPanel");
            if (panel == null) { _openSince = -1f; return; }
            if (_openSince < 0f) _openSince = now;
            if (now - _openSince < WaitSeconds) return;
            foreach (var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                if (button.name != "ConfirmButton" || !button.IsInteractable()) continue;
                _openSince = -1f;
                button.onClick.Invoke();
                return;
            }
        }
    }

    // Applies to every test under this namespace, with or without InputTestFixture.
    [SetUpFixture]
    public sealed class PlayModeScenarioStart
    {
        [OneTimeSetUp]
        public void Apply()
        {
            // Scenario tests start from Town. The House start is covered by NewGame_StartsInTheHouse and real-input play.
            Rootborn.Game.World.WorldEntryService.SetNewGameEntryForTests("Town", "town-home-entry");
            PlayModeCreatorAutoConfirm.ConfigureFor(string.Empty);
        }

        // A player cannot press confirm in the first frames after the menu loads, while game data is still loading. Tests can.
        public static System.Collections.IEnumerator WaitForGameData()
        {
            float deadline = UnityEngine.Time.unscaledTime + 15f;
            while (!Rootborn.Game.Common.GameDataRegistryProvider.TryGet(out _) && UnityEngine.Time.unscaledTime < deadline) yield return null;
            yield return null;
        }

        [OneTimeTearDown]
        public void Clear() => Rootborn.Game.World.WorldEntryService.SetNewGameEntryForTests(null, null);
    }

    public abstract class PlayModeInputFixture : UnityEngine.InputSystem.InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            PlayModeInputSystemGuardAction.ForgetResolvedControls();
            PlayModeCreatorAutoConfirm.ConfigureFor(TestContext.CurrentContext.Test.FullName);
            // Scenario tests start from Town. The House start is covered by NewGame_StartsInTheHouse and real-input play.
            Rootborn.Game.World.WorldEntryService.SetNewGameEntryForTests("Town", "town-home-entry");
        }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class PlayModeInputSystemGuardAction : Attribute, ITestAction
    {
        public ActionTargets Targets => ActionTargets.Test;

        // InputTestFixture swaps the Input System per test. Shared action assets keep controls resolved against the swapped
        // system, and enabling them afterwards throws IndexOutOfRange inside the Input System, failing unrelated tests.
        internal static void ForgetResolvedControls()
        {
            foreach (var asset in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.InputSystem.InputActionAsset>())
            {
                try
                {
                    bool wasEnabled = asset.enabled;
                    asset.Disable();
                    asset.bindingMask = new UnityEngine.InputSystem.InputBinding { groups = "__test-reset__" };
                    asset.bindingMask = null;
                    if (wasEnabled) asset.Enable();
                }
                catch (Exception error)
                {
                    UnityEngine.Debug.LogWarning("Input action reset failed for " + asset.name + ": " + error.Message);
                }
            }
        }

        public void BeforeTest(ITest test)
        {
            // Scenario tests here start from Town. The House start is covered by NewGame_StartsInTheHouse and real-input play.
            Rootborn.Game.World.WorldEntryService.SetNewGameEntryForTests("Town", "town-home-entry");
            LogAssert.ignoreFailingMessages = true;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
        }

        public void AfterTest(ITest test)
        {
            LogAssert.ignoreFailingMessages = true;
            ForgetResolvedControls();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
        }
    }
}
