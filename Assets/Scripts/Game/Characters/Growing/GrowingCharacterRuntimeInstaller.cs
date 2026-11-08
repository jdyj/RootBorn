using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.Characters.Growing
{
    /// <summary>Puts the layered character on a player when the registry provides one. Colliders and movement stay on the root.</summary>
    public static class GrowingCharacterRuntimeInstaller
    {
        public const string VisualRootName = "GrowingCharacterVisual";
        public const string PrefabPath = "Assets/Prefabs/Characters/GrowingCharacterVisual.prefab";

        // Sprites stand on their feet; the player's transform sits at the middle of its one-unit collider.
        public const float FeetOffsetY = -0.5f;

        public static event System.Action<GameObject, GrowingCharacterVisualView> Installed;

        public static GrowingCharacterAppearanceDefinition FindDefault(GameDataRegistry registry)
        {
            var appearances = registry != null ? registry.CharacterAppearances : null;
            if (appearances == null) return null;
            for (int i = 0; i < appearances.Length; i++)
            {
                if (appearances[i] is GrowingCharacterAppearanceDefinition growing && growing.Body != null && growing.Body.IsValid) return growing;
            }

            return null;
        }

        public static bool TryInstall(GameObject player, GameDataRegistry registry)
        {
            if (player == null) return false;
            var appearance = FindDefault(registry);
            if (appearance == null) return false;
            var view = player.GetComponentInChildren<GrowingCharacterVisualView>(true);
            if (view == null)
            {
                view = appearance.VisualPrefab != null ? Object.Instantiate(appearance.VisualPrefab, player.transform, false) : new GameObject(VisualRootName).AddComponent<GrowingCharacterVisualView>();
                view.name = VisualRootName;
                view.transform.SetParent(player.transform, false);
                view.transform.localPosition = new Vector3(0f, FeetOffsetY, 0f);
            }

            var rootRenderer = player.GetComponent<SpriteRenderer>();
            // A reinstall keeps whatever look is already drawn (legacy appearance or wearable layers).
            if (!view.HasLook) view.Apply(appearance);
            view.MatchSorting(rootRenderer);
            HideOtherVisuals(player, view);
            var adapter = player.GetComponent<PlayerCharacterVisualAdapter>();
            if (adapter == null) adapter = player.AddComponent<PlayerCharacterVisualAdapter>();
            adapter.ConfigureForTests(view);
            view.SetMotion(Vector2.zero, Vector2.down);
            Installed?.Invoke(player, view);
            return true;
        }

        private static void HideOtherVisuals(GameObject player, GrowingCharacterVisualView view)
        {
            var renderers = player.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var candidate = renderers[i];
                if (candidate == null || candidate.transform.IsChildOf(view.transform)) continue;
                candidate.enabled = false;
            }
        }
    }
}
