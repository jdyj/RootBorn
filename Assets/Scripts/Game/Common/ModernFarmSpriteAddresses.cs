namespace Rootborn.Game.Common
{
    public static class ModernFarmSpriteAddresses
    {
        public const string Terrain16 = "sprites/farm/modern/16/terrain";
        public const string Fences16 = "sprites/farm/modern/16/fences";
        public const string Props16 = "sprites/farm/modern/16/props";
        public const string Crops16 = "sprites/farm/modern/16/crops";
        public const string FruitTrees16 = "sprites/farm/modern/16/fruit-trees";
        public const string Trees16 = "sprites/farm/modern/16/trees";
        public const string Pickups16 = "sprites/farm/modern/16/pickups";

        public static readonly (string sheetAddress, string[] subNames)[] AllSheets = new[]
        {
            (Terrain16, new[] { "ModernFarm_Terrain_r0_c0", "ModernFarm_Terrain_r22_c31" }),
            (Fences16, new[] { "ModernFarm_Fence_r0_c0", "ModernFarm_Fence_r16_c31" }),
            (Props16, new[] { "ModernFarm_Prop_r0_c0", "ModernFarm_Prop_r139_c31" }),
            (Crops16, new[] { "ModernFarm_Crop_r0_c0", "ModernFarm_Crop_r37_c19" }),
            (FruitTrees16, new[] { "ModernFarm_FruitTree_r0_c0", "ModernFarm_FruitTree_r46_c24" }),
            (Trees16, new[] { "ModernFarm_Tree_r0_c0", "ModernFarm_Tree_r193_c49" }),
            (Pickups16, new[] { "ModernFarm_Pickup_r0_c0", "ModernFarm_Pickup_r9_c13" }),
        };
    }
}
