using System;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.Minigames;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Careers;
using UnityEngine;

namespace Rootborn.UI.Minigames
{
    public static class MinigamePresentationBridge
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            Uninstall();
            StudentMinigameRequestBridge.PracticeRequested += Practice;
            StudentMinigameRequestBridge.PartTimeWorkRequested += PartTime;
            FacilityManagementPanel.WorkRequested += Work;
        }

        public static void Uninstall()
        {
            StudentMinigameRequestBridge.PracticeRequested -= Practice;
            StudentMinigameRequestBridge.PartTimeWorkRequested -= PartTime;
            FacilityManagementPanel.WorkRequested -= Work;
        }

        private static bool Practice(GameObject player, CareerPracticeDefinition practice, string request)
        {
            if (!Prepare(player, out var owner, out var panel)) return false;
            return Begin(owner, panel, () => owner.TryBeginPractice(practice, request, NewSeed()));
        }

        private static bool PartTime(GameObject player, PartTimeWorkDefinition work, string request)
        {
            if (!Prepare(player, out var owner, out var panel)) return false;
            // Scene references and an Addressables registry can hold distinct instances.
            // Use only the owner's unique registered definition, never scene-supplied costs.
            var canonical = ResolvePartTimeWork(owner.Registry, work);
            if (canonical == null) return Refuse("work " + (work != null ? work.Id : "-") + " is not registered exactly once");
            return Begin(owner, panel, () => owner.TryBeginPartTimeWork(canonical, request, NewSeed()));
        }

        private static PartTimeWorkDefinition ResolvePartTimeWork(GameDataRegistry registry, PartTimeWorkDefinition work)
        {
            if (work == null || string.IsNullOrWhiteSpace(work.Id) || registry == null || registry.PartTimeWorks == null) return null;
            PartTimeWorkDefinition canonical = null;
            foreach (var candidate in registry.PartTimeWorks)
            {
                if (candidate == null || !string.Equals(candidate.Id, work.Id, StringComparison.Ordinal)) continue;
                if (canonical != null) return null;
                canonical = candidate;
            }
            return canonical;
        }

        private static void Work(FacilityWorkDefinition work, AdultCareerRuntimeComponent owner)
        {
            if (owner == null || !Prepare(owner.gameObject, out var current, out var panel) || current != owner) return;
            Begin(owner, panel, () => owner.TryBeginWork(work, Guid.NewGuid().ToString("N"), NewSeed()));
        }

        private static int NewSeed() => Guid.NewGuid().GetHashCode();

        private static bool Prepare(GameObject player, out AdultCareerRuntimeComponent owner, out MinigamePanel panel)
        {
            owner = null; panel = null;
            if (player == null) return false;
            Canvas canvas = null;
            // Resolve only when an interaction is requested, never once per frame.
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (candidate.isRootCanvas && candidate.isActiveAndEnabled && candidate.gameObject.scene == player.scene)
                { canvas = candidate; break; }
            }
            if (canvas == null) return Refuse("no active root canvas in " + player.scene.name);
            panel = MinigamePanel.EnsureInScene(canvas);
            if (panel == null) return Refuse("no minigame panel");
            if (panel.IsOpen) return Refuse("a minigame is already open");
            owner = player.GetComponent<AdultCareerRuntimeComponent>();
            if (owner == null || !owner.IsBound)
            {
                if (!GameDataRegistryProvider.TryGet(out var registry)) return false;
                owner = AdultCareerRuntimeComponent.Ensure(player, registry);
            }
            if (owner == null) return Refuse("no career owner on the player");
            if (!owner.CanMutate) return Refuse("career owner cannot change state (bound=" + owner.IsBound + " recovery=" + owner.RecoveryRequired + ")");
            if (owner.ActiveWorkSession != null) return Refuse("a work session is already running");
            return true;
        }

        // E at a work point that silently does nothing is what a player sees when a request is refused; say why in the log.
        private static bool Refuse(string reason)
        {
            Debug.LogWarning("[ROOTBORN] Minigame request refused: " + reason);
            return false;
        }

        private static bool Begin(AdultCareerRuntimeComponent owner, MinigamePanel panel, Func<AdultCareerResult> reserve)
        {
            var reserved = reserve();
            if (!reserved.Applied) return Refuse("reservation " + reserved.Kind);
            var session = owner.ActiveWorkSession;
            if (panel.Show(session, completed => Settle(owner, panel, completed))) return true;
            // A presentation failure cannot leave a live playable attempt behind.
            if (session != null) { session.Cancel(); owner.TrySettlePendingWork(session); }
            return false;
        }

        private static void Settle(AdultCareerRuntimeComponent owner, MinigamePanel panel, MinigameSession session)
        {
            if (owner != null && owner.TrySettlePendingWork(session).Applied) return;
            panel.SetSettlementFailure(GameText.Get("minigame.save-failed"),
                () => owner != null && owner.TrySettlePendingWork(session).Applied);
        }
    }
}
