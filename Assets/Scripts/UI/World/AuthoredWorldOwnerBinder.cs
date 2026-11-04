using System.Collections.Generic;
using System.Threading.Tasks;
using Rootborn.Game.Careers;
using Rootborn.UI.Careers;
using Rootborn.UI.HUD;
using Rootborn.UI.StudentLife;
using Rootborn.UI.Quests;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.World
{
    public static class AuthoredWorldOwnerBinder
    {
        public static async Task<bool> Bind(Scene scene, AdultCareerRuntimeComponent owner)
        {
            if (!scene.IsValid() || !scene.isLoaded || owner == null || owner.gameObject.scene != scene || !owner.IsBound || owner.RecoveryRequired
                || owner.Progress == null || !AdultCareerPersistenceRouter.IsOwner(owner.Progress.SaveSlot, owner.Progress.PlayerId, owner)) return false;
            TownTimeHudRuntimeInstaller.BindOwner(scene, owner.StudentComponent);
            DailyPlanSessionBinding.Ensure(owner.gameObject, owner.Registry);
            LocationNpcRuntimeInstaller.BindOwner(scene, owner.StudentComponent);
            if (!NpcDialoguePresenter.BindSceneOwner(scene, owner)) return false;
            if(!Rootborn.UI.Interiors.AuthoredHousePlacementBinding.BindOwner(scene,owner))return false;
            var pending = new List<Task<bool>>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var presenter in root.GetComponentsInChildren<FacilityWorldPresenter>(true))
                {
                    var binding = presenter.BindOwner(owner);
                    if (presenter.isActiveAndEnabled) pending.Add(binding);
                }
            bool result = true;
            foreach (var task in pending) result &= await task;
            return result && owner != null && owner.Progress != null
                && AdultCareerPersistenceRouter.IsOwner(owner.Progress.SaveSlot, owner.Progress.PlayerId, owner);
        }
    }
}
