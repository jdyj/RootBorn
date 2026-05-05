using UnityEngine;

namespace Rootborn.Game.Common
{
    /// <summary>
    /// 자원 노드 충돌 전역 토글. false 로 두면 _isWalkable=false 자원도 통과 (디버그·맵 디자인용).
    /// FarmAutoFiller.SpawnNode 가 spawn 시 1회 평가. 런타임 토글은 자원 재스폰 후에야 반영.
    /// </summary>
    public static class ResourceCollisionToggle
    {
        public static bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Addressables 주소와 sub-sprite 이름 매핑. Editor 에서 자동 와이어링.
    /// 런타임은 이 SO만 보고 어떤 sheet의 어떤 sub-sprite를 로드할지 결정.
    /// SlimeMaster 의 Define.cs 상수 + AddressableKeys 패턴.
    /// </summary>
    [CreateAssetMenu(fileName = "AddressableManifest", menuName = "Rootborn/Common/Addressable Manifest")]
    public sealed class AddressableManifest : ScriptableObject
    {
        public const string AddrRegistry = "data/registry";
        public const string AddrSheetTile = "sheet/Tile";
        public const string AddrSheetIdleDown = "sheet/Down";
        public const string AddrPlayerPrefab = "prefabs/player";
        public const string LabelPreLoad = "PreLoad";

        [Header("Sheet Address → Sub-sprite Name")]
        [SerializeField] private string _groundSheetAddress = AddrSheetTile;
        [SerializeField] private string _groundSubSpriteName = "Tile_r2_c4";
        [SerializeField] private string _playerIdleSheetAddress = AddrSheetIdleDown;
        [SerializeField] private string _playerIdleSubSpriteName = "Idle_Down_1";

        public string GroundSheetAddress => _groundSheetAddress;
        public string GroundSubSpriteName => _groundSubSpriteName;
        public string PlayerIdleSheetAddress => _playerIdleSheetAddress;
        public string PlayerIdleSubSpriteName => _playerIdleSubSpriteName;
    }
}
