using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Resources;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode
{
    public sealed class SeededFarmGenerationFlowTests
    {
        [TearDown]
        public void TearDown()
        {
            ActiveSaveContext.Clear();
        }

        [UnityTest]
        public IEnumerator FarmScene_UsesActiveSaveSeeds_ForResourceLayout()
        {
            ActiveSaveContext.Set(new SaveSlotMetadata
            {
                SlotId = "slot-0",
                DisplayName = "slot-0",
                WorldSeed = 111,
                TileSeed = 222,
                Character = new CharacterCustomization(),
            });
            yield return LoadFarmAndFill();
            string first = ResourceLayoutSignature();

            ActiveSaveContext.Set(new SaveSlotMetadata
            {
                SlotId = "slot-0",
                DisplayName = "slot-0",
                WorldSeed = 333,
                TileSeed = 444,
                Character = new CharacterCustomization(),
            });
            yield return LoadFarmAndFill();
            string second = ResourceLayoutSignature();

            Assert.AreNotEqual(first, second);
        }

        private static IEnumerator LoadFarmAndFill()
        {
            var asyncLoad = SceneManager.LoadSceneAsync("Farm");
            while (!asyncLoad.isDone) yield return null;

            var bootstrapTask = Managers.BootstrapAsync();
            float elapsed = 0f;
            while (!bootstrapTask.IsCompleted && elapsed < 10f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(bootstrapTask.IsCompleted);

            var existing = GameObject.Find("[FarmAutoFiller]");
            var filler = existing != null ? existing.GetComponent<FarmAutoFiller>() : null;
            if (filler == null)
            {
                existing = new GameObject("[FarmAutoFiller]");
                filler = existing.AddComponent<FarmAutoFiller>();
            }

            filler.FillIfEmpty();
            yield return null;
        }

        private static string ResourceLayoutSignature()
        {
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            var parts = new List<string>(nodes.Length);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Definition == null)
                {
                    continue;
                }

                var position = nodes[i].transform.position;
                parts.Add($"{nodes[i].Definition.name}:{position.x:0.00}:{position.y:0.00}");
            }

            parts.Sort(System.StringComparer.Ordinal);
            return string.Join("|", parts);
        }
    }
}
