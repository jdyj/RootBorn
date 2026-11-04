using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Characters.Growing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Profiling;

namespace Rootborn.Tests.PlayMode.Characters
{
    /// <summary>Measures the runtime costs the layered character adds. Numbers go to the test log for the report.</summary>
    public sealed class GrowingCharacterPerformanceTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GROW_PERF_001_MovingAllocatesNothingAndCharactersStayInRenderBudget()
        {
            var load = SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            GrowingCharacterVisualView view = null;
            float waited = 0f;
            while (view == null && waited < 25f)
            {
                var player = GameObject.Find("Player");
                view = player != null ? player.GetComponentInChildren<GrowingCharacterVisualView>(true) : null;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(view, Is.Not.Null);
            yield return new WaitForSecondsRealtime(1f);

            int changes = view.FrameChangeCount;
            long allocatedBefore = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 600; i++) view.Advance(Vector2.zero, Vector2.down, 0.016f);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.That(allocated, Is.Zero, "Standing still must not allocate.");
            Assert.That(view.FrameChangeCount, Is.EqualTo(changes), "Standing still must not reassign sprites.");
            allocatedBefore = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 600; i++) view.Advance(Vector2.up, Vector2.up, 0.016f);
            allocated = System.GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.That(allocated, Is.Zero, "Walking must not allocate.");
            view.Advance(Vector2.zero, Vector2.down, 0.016f);

            var texture = view.BodyRenderer.sprite.texture;
            long textureBytes = Profiler.GetRuntimeMemorySizeLong(texture);
            Debug.Log($"[GROW_PERF] body texture {texture.name} {texture.width}x{texture.height} {textureBytes} bytes");
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));

            // Nine characters drawn with the same layers. Batch counts come from the Editor's render statistics.
            var clones = new GameObject[8];
            for (int i = 0; i < clones.Length; i++)
            {
                clones[i] = Object.Instantiate(view.gameObject);
                // A tight grid around the player keeps all nine inside the camera view.
                clones[i].transform.position = view.transform.position + new Vector3(1.5f * ((i % 4) - 1.5f), 2f * (i / 4 + 1), 0f);
            }

            var camera = Camera.main;
#if UNITY_EDITOR
            yield return null;
            yield return null;
            // A budget means nothing for characters the camera does not draw.
            var frustum = GeometryUtility.CalculateFrustumPlanes(camera);
            foreach (var clone in clones)
            {
                var cloneView = clone.GetComponent<GrowingCharacterVisualView>();
                Assert.That(clone.activeInHierarchy, Is.True);
                foreach (var layer in new[] { cloneView.BodyRenderer, cloneView.OutfitRenderer, cloneView.HeadRenderer })
                {
                    Assert.That(layer.enabled && layer.sprite != null, Is.True, layer.name + " must draw.");
                    Assert.That(GeometryUtility.TestPlanesAABB(frustum, layer.bounds), Is.True, layer.name + " must be in view.");
                    Assert.That(camera.cullingMask & (1 << layer.gameObject.layer), Is.Not.Zero);
                }
            }

            int batchesNine = UnityEditor.UnityStats.batches;
            int drawCallsNine = UnityEditor.UnityStats.drawCalls;
            int setPassNine = UnityEditor.UnityStats.setPassCalls;
            foreach (var clone in clones) Object.Destroy(clone);
            yield return null;
            yield return null;
            int batchesOne = UnityEditor.UnityStats.batches;
            int drawCallsOne = UnityEditor.UnityStats.drawCalls;
            int setPassOne = UnityEditor.UnityStats.setPassCalls;
            Debug.Log($"[GROW_PERF] one character: batches {batchesOne} draws {drawCallsOne} setpass {setPassOne}; nine characters: batches {batchesNine} draws {drawCallsNine} setpass {setPassNine}");
            // Measured 2026-11-04: each character costs three draws and almost no SetPass; an atlas did not change this.
            Assert.That(batchesNine - batchesOne, Is.LessThanOrEqualTo(24), "Eight more characters may add at most three draws each.");
            Assert.That(setPassNine - setPassOne, Is.LessThanOrEqualTo(4), "Characters should share render state.");
#endif
            Assert.That(camera, Is.Not.Null);
        }
    }
}
