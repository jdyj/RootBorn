using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HouseInteriorInteractablePlayModeTests
    {
        [UnityTest]
        public IEnumerator HouseScene_SpawnsComputerInteractorUsableByPlayerRouter()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var interactor = Object.FindFirstObjectByType<InteriorObjectInteractor>();
            Assert.IsNotNull(interactor, "Generated House computer should have a runtime interactor object.");
            Assert.AreEqual(InteriorObjectKind.Computer, interactor.ObjectKind);

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);

            var router = player.GetComponent<PlayerInteractionRouter>();
            Assert.IsNotNull(router);
            router.ConfigureForTests(2f);
            player.transform.position = interactor.transform.position + new Vector3(0.25f, 0f, 0f);
            yield return null;

            router.RefreshPromptNow();
            Assert.IsTrue(router.PromptVisible);
            Assert.AreEqual(interactor.InteractionPrompt, router.PromptText);

            Assert.IsTrue(router.TryInteractWithNearest());
            Assert.AreEqual(1, interactor.InteractionCount);
            Assert.AreSame(player, interactor.LastInteractingPlayer);
        }
    }
}
