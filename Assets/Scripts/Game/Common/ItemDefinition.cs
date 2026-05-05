using Rootborn.Game.Crops;
using UnityEngine;

namespace Rootborn.Game.Common
{
    /// <summary>
    /// 자원·도구·기타 모든 인벤토리 표시 가능 아이템의 SO 정의.
    /// 도구는 ToolDefinition 과 동일 ID 를 공유 (인벤토리에서 동일 객체로 인식).
    /// 별도 .cs 파일로 분리한 이유: Unity 는 파일명과 같은 단일 클래스 MonoScript 만 생성하므로,
    /// .asset 가 m_Script 로 참조하려면 ItemDefinition 도 자신의 .cs 파일에 있어야 함.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_New", menuName = "Rootborn/Items/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayKey;
        [SerializeField] private Sprite _icon;
        [SerializeField] private int _maxStack = 99;
        [SerializeField] private ItemCategory _category = ItemCategory.Resource;
        [SerializeField, TextArea(3, 6)] private string _description;
        // Category==Seed 인 아이템이 어떤 작물의 씨앗인지. PlantSeedEffect 가 ID 분기 없이 작물 조회.
        [SerializeField] private CropDefinition _seedFor;

        public string Id => _id;
        public string DisplayKey => _displayKey;
        public Sprite Icon => _icon;
        public int MaxStack => Mathf.Max(1, _maxStack);
        public ItemCategory Category => _category;
        public string Description => _description;
        public CropDefinition SeedFor => _seedFor;
    }
}
