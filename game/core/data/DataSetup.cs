using Godot.Collections;

namespace SurvivalGame.Core.Data
{
    /// <summary>
    /// M2 基础数据硬编码注册 — 在 GameManager._Ready() 中调用一次。
    /// 无需 .tres 资源文件，纯代码定义物品。
    /// </summary>
    public static class DataSetup
    {
        public static void Register()
        {
            RegisterItems();
        }

        private static void RegisterItems()
        {
            var ir = ItemRegistry.Instance;

            ir.Register(new ItemDefinition
            {
                Id = "wood", DisplayName = "木材",
                Category = ItemCategory.Material,
                Weight = 1f, MaxStackSize = 100
            });
            ir.Register(new ItemDefinition
            {
                Id = "grass", DisplayName = "茅草",
                Category = ItemCategory.Material,
                Weight = 0.2f, MaxStackSize = 100
            });
            ir.Register(new ItemDefinition
            {
                Id = "berry", DisplayName = "浆果",
                Category = ItemCategory.Food,
                Weight = 0.1f, MaxStackSize = 20,
                HungerRestore = 20f, ThirstRestore = 5f
            });
            ir.Register(new ItemDefinition
            {
                Id = "water_skin", DisplayName = "水袋",
                Category = ItemCategory.Food,
                Weight = 0.3f, MaxStackSize = 5,
                ThirstRestore = 30f
            });
        }
    }
}
