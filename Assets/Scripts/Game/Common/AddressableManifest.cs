using UnityEngine;

namespace Rootborn.Game.Common
{
    /// <summary>
    /// Resource node collision toggle. False lets non-walkable resource definitions pass through for debugging.
    /// </summary>
    public static class ResourceCollisionToggle
    {
        public static bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Optional addressable manifest for data-driven sprite sheet lookups.
    /// Current defaults point at Modern Farm sheets; core runtime visual fallbacks are wired through GameDataRegistry.
    /// </summary>
    [CreateAssetMenu(fileName = "AddressableManifest", menuName = "Rootborn/Common/Addressable Manifest")]
    public sealed class AddressableManifest : ScriptableObject
    {
        public const string AddrRegistry = "data/registry";
        public const string AddrSheetTerrain = "sprites/modernfarm/terrain-16";
        public const string AddrSheetPlayerIdle = "sprites/modernfarm/player-idle";
        public const string AddrPlayerPrefab = "prefabs/player";
        public const string LabelPreLoad = "PreLoad";

        [Header("Sheet Address / Sub-sprite Name")]
        [SerializeField] private string _groundSheetAddress = AddrSheetTerrain;
        [SerializeField] private string _groundSubSpriteName = "ModernFarm_Terrain_r0_c0";
        [SerializeField] private string _playerIdleSheetAddress = AddrSheetPlayerIdle;
        [SerializeField] private string _playerIdleSubSpriteName = "ModernFarmer_IdleDown_16x16";

        public string GroundSheetAddress => _groundSheetAddress;
        public string GroundSubSpriteName => _groundSubSpriteName;
        public string PlayerIdleSheetAddress => _playerIdleSheetAddress;
        public string PlayerIdleSubSpriteName => _playerIdleSubSpriteName;
    }
}
