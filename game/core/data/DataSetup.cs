using SurvivalGame.Core.ECS;

namespace SurvivalGame.Core.Data
{
    /// <summary>
    /// M2/M3 基础数据硬编码注册 — 在 GameManager._Ready() 中调用一次。
    /// 无需 .tres 资源文件，纯代码定义物品与生物。
    /// </summary>
    public static class DataSetup
    {
        public static void Register()
        {
            RegisterItems();
            RegisterCreatures();
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
            ir.Register(new ItemDefinition
            {
                Id = "meat", DisplayName = "生肉",
                Category = ItemCategory.Food,
                Weight = 0.5f, MaxStackSize = 20,
                HungerRestore = 40f
            });
        }

        private static void RegisterCreatures()
        {
            var cr = CreatureRegistry.Instance;

            // 野猪 — 被动驯养，用浆果喂食，驯服后可自动采集木材
            cr.Register(new CreatureDefinition
            {
                Id = "boar", DisplayName = "野猪",
                Tier = CreatureTier.D,
                TamingMethod = TamingMethod.Passive,
                PreferredFood = "berry",
                BaseHp = 150f, BaseAttack = 10f, BaseSpeed = 3.5f, BaseWeight = 40f,
                HarvestResourceType = "wood",
                CanRide = false, CanFly = false,
                SpawnBiomes = new[] { "forest", "grassland" }
            });
        }
    }
}
