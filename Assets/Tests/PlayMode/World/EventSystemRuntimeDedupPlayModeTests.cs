using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.World
{
    public sealed class EventSystemRuntimeDedupPlayModeTests
    {
        [UnityTest]
        public IEnumerator TOWN_UI_PM_002_LoadingTownRemovesDuplicateEventSystems()
        {
            var extra = new GameObject("EventSystem_TestDuplicate", typeof(EventSystem));
            extra.SetActive(false);
            Object.DontDestroyOnLoad(extra);

            var op = SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            Assert.IsNotNull(op);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            var systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.LessOrEqual(systems.Length, 1, "Playable scene loads must remove inactive or active duplicate EventSystems.");
        }
    }
}
