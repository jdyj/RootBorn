using System.Threading.Tasks;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Common;
using Rootborn.Game.Careers;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.World;
using Rootborn.Network.Session;
using Rootborn.UI.Careers;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.World
{
    public static class PlayerWorldTravelAdapter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            WorldTravelRuntime.ConfigureDefaultOwnerHandlers(Prepare, Complete, Cancel);
            WorldEntryService.ConfigureLegacyPlaceholderPolicy(CanRemoveLegacyPlaceholder);
            WorldTravelRuntime.RecoveryFailed -= ShowRecoveryFailure;
            WorldTravelRuntime.RecoveryFailed += ShowRecoveryFailure;
        }

        private static void ShowRecoveryFailure()
        {
            Rootborn.UI.MainMenu.SaveSlotSelectPanel.EnsureInScene().ShowEntryFailure();
        }

        public static bool CanRemoveLegacyPlaceholder(GameObject traveller, GameObject candidate)
        {
            if (!IsOfflineAuthority() || traveller == null || candidate == null || traveller == candidate
                || traveller.scene != candidate.scene || candidate.transform.parent != null || candidate.name != "Player"
                || candidate.GetComponent<PlayerController>() == null || candidate.GetComponent<PlayerIdentity>() != null
                || candidate.GetComponent<NetworkObject>() != null) return false;
            var owner = traveller.GetComponent<AdultCareerRuntimeComponent>();
            var identity = traveller.GetComponent<PlayerIdentity>();
            if (owner == null || identity == null || !owner.IsBound || owner.RecoveryRequired || owner.Progress == null
                || owner.StudentComponent == null || owner.StudentComponent.Progress == null) return false;
            var student = owner.StudentComponent.Progress;
            return owner.Progress.PlayerId == identity.PlayerId && owner.Progress.SaveSlot == ActiveSaveContext.SlotId
                && student.SaveSlot == owner.Progress.SaveSlot && student.PlayerId == identity.PlayerId
                && AdultCareerPersistenceRouter.IsOwner(owner.Progress.SaveSlot, identity.PlayerId, owner)
                && (owner.SceneTransferPending || owner.CanMutate);
        }

        public static bool RequestEntry(SaveSlotMetadata metadata, string sceneName, string spawnId)
        {
            return IsOfflineAuthority() && WorldTravelRuntime.TryRequestEntry(metadata, sceneName, spawnId);
        }

        public static bool Prepare(GameObject actor)
        {
            if (actor == null) return false;
            var owner = actor.GetComponent<AdultCareerRuntimeComponent>();
            return owner != null && owner.TryBeginSceneTransfer();
        }
        public static async Task<bool> Complete(Scene scene, GameObject actor)
        {
            if (!IsOfflineAuthority() || !scene.IsValid() || !scene.isLoaded || actor == null || actor.scene != scene
                || actor.GetComponent<PlayerIdentity>() == null || !GameDataRegistryProvider.TryGet(out var registry) || registry.StudentLifeCalendar == null) return false;
            // Only the explicit initial-entry actor can reach this path without an owner.
            // Portal preparation requires an existing owner and the driver rejects replacement actors.
            if (actor.GetComponent<StudentLifeProgressComponent>() == null) actor.AddComponent<StudentLifeProgressComponent>();
            if (actor.GetComponent<PlayerInventory>() == null) actor.AddComponent<PlayerInventory>();
            var owner = AdultCareerRuntimeComponent.Ensure(actor, registry);
            if (owner == null || !owner.IsBound || owner.RecoveryRequired) return false;
            if (owner.SceneTransferPending && !owner.TryCompleteSceneTransfer()) return false;
            if (!owner.CanMutate || !owner.SaveCurrent()) return false;
            return await AuthoredWorldOwnerBinder.Bind(scene, owner);
        }
        public static void Cancel(GameObject actor)
        {
            if (actor != null) actor.GetComponent<AdultCareerRuntimeComponent>()?.CancelSceneTransfer();
        }
        private static bool IsOfflineAuthority()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return false;
            var session = NetworkBootstrap.ActiveSession;
            if (session != null && session.Mode != SessionMode.Single && session.Mode != SessionMode.None) return false;
            var mode = GameBootstrap.Config != null ? GameBootstrap.Config.Mode : SessionMode.None;
            return mode == SessionMode.Single || mode == SessionMode.None;
        }
    }
}
