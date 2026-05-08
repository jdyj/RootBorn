using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.TownConcept
{
    public sealed class TownSceneBootTests
    {
        [UnityTest]
        public IEnumerator TownScene_LoadsAsPlayableTownBaseline()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return null;

            Scene active = SceneManager.GetActiveScene();
            Assert.AreEqual("Town", active.name);
            Assert.IsTrue(active.rootCount > 0, "Town scene should have root objects.");
        }
    }
}