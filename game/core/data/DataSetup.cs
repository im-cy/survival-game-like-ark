using Godot;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Core.Data
{
    /// <summary>
    /// M2–M6 基础数据硬编码注册 — 在 GameManager._Ready() 中调用一次。
    /// 无需 .tres 资源文件，纯代码定义物品、配方与生物。
    /// </summary>
    public static class DataSetup
    {
        public static void Register()
        {
            RegisterItems();
            RegisterRecipes();
            RegisterCreatures();
        }

        // ── 物品 ──────────────────────────────────────────────────────────

        private static void RegisterItems()
        {
            var ir = ItemRegistry.Instance;

            // 基础材料
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
                Id = "stone", DisplayName = "石头",
                Category = ItemCategory.Material,
                Weight = 1.5f, MaxStackSize = 100
            });
            ir.Register(new ItemDefinition
            {
                Id = "fiber", DisplayName = "纤维",
                Category = ItemCategory.Material,
                Weight = 0.1f, MaxStackSize = 100
            });
            ir.Register(new ItemDefinition
            {
                Id = "hide", DisplayName = "兽皮",
                Category = ItemCategory.Material,
                Weight = 0.5f, MaxStackSize = 50
            });
            ir.Register(new ItemDefinition
            {
                Id = "boss_crystal", DisplayName = "Boss水晶",
                Category = ItemCategory.Material,
                Weight = 0.2f, MaxStackSize = 10
            });

            // 食物
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

            // 工具/武器（一阶）
            ir.Register(new ItemDefinition
            {
                Id = "stone_axe", DisplayName = "石斧",
                Category = ItemCategory.Tool,
                Weight = 2f, MaxStackSize = 1, DurabilityMax = 50f,
                AttackDamage = 35f
            });
            ir.Register(new ItemDefinition
            {
                Id = "wood_spear", DisplayName = "木矛",
                Category = ItemCategory.Weapon,
                Weight = 1.5f, MaxStackSize = 1, DurabilityMax = 40f,
                AttackDamage = 45f
            });

            // 装备/武器（二阶）
            ir.Register(new ItemDefinition
            {
                Id = "leather_chestplate", DisplayName = "皮革胸甲",
                Category = ItemCategory.Armor,
                Weight = 3f, MaxStackSize = 1, DurabilityMax = 120f,
                ArmorValue = 15f
            });
            ir.Register(new ItemDefinition
            {
                Id = "wood_bow", DisplayName = "木弓",
                Category = ItemCategory.Weapon,
                Weight = 1f, MaxStackSize = 1, DurabilityMax = 60f,
                AttackDamage = 30f, IsRanged = true, ProjectileRange = 22f, AmmoId = "arrow"
            });
            ir.Register(new ItemDefinition
            {
                Id = "arrow", DisplayName = "箭矢",
                Category = ItemCategory.Weapon,
                Weight = 0.1f, MaxStackSize = 50
            });
        }

        // ── 配方 ──────────────────────────────────────────────────────────

        private static void RegisterRecipes()
        {
            var rr = RecipeRegistry.Instance;

            // ── 一阶：石器时代（游戏开始即解锁）────────────────────────

            var stoneAxe = new RecipeDefinition
            {
                Id = "stone_axe", ResultItemId = "stone_axe", ResultQuantity = 1,
                UnlockSource = "tier1", RequiredStation = "",
            };
            stoneAxe.Ingredients.Add(new IngredientEntry { ItemId = "stone", Quantity = 5 });
            stoneAxe.Ingredients.Add(new IngredientEntry { ItemId = "wood",  Quantity = 2 });
            rr.Register(stoneAxe);

            var woodSpear = new RecipeDefinition
            {
                Id = "wood_spear", ResultItemId = "wood_spear", ResultQuantity = 1,
                UnlockSource = "tier1", RequiredStation = "",
            };
            woodSpear.Ingredients.Add(new IngredientEntry { ItemId = "wood",  Quantity = 4 });
            woodSpear.Ingredients.Add(new IngredientEntry { ItemId = "fiber", Quantity = 3 });
            rr.Register(woodSpear);

            // ── 二阶：革质时代（击败 Boss 后解锁）───────────────────────

            var leatherChest = new RecipeDefinition
            {
                Id = "leather_chestplate", ResultItemId = "leather_chestplate", ResultQuantity = 1,
                UnlockSource = "boss:forest_guardian", RequiredStation = "",
            };
            leatherChest.Ingredients.Add(new IngredientEntry { ItemId = "hide",  Quantity = 8 });
            leatherChest.Ingredients.Add(new IngredientEntry { ItemId = "fiber", Quantity = 5 });
            rr.Register(leatherChest);

            var woodBow = new RecipeDefinition
            {
                Id = "wood_bow", ResultItemId = "wood_bow", ResultQuantity = 1,
                UnlockSource = "boss:forest_guardian", RequiredStation = "",
            };
            woodBow.Ingredients.Add(new IngredientEntry { ItemId = "wood",  Quantity = 5 });
            woodBow.Ingredients.Add(new IngredientEntry { ItemId = "fiber", Quantity = 4 });
            rr.Register(woodBow);

            var arrow = new RecipeDefinition
            {
                Id = "arrow", ResultItemId = "arrow", ResultQuantity = 5,
                UnlockSource = "boss:forest_guardian", RequiredStation = "",
            };
            arrow.Ingredients.Add(new IngredientEntry { ItemId = "wood",  Quantity = 2 });
            arrow.Ingredients.Add(new IngredientEntry { ItemId = "fiber", Quantity = 1 });
            rr.Register(arrow);
        }

        // ── 生物 ──────────────────────────────────────────────────────────

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
                SpawnBiomes = new[] { "forest", "grassland" },
                LootTable = new[] { "meat:2", "hide:1" },
                ViewScale = 1f,
                DetectionRange = 12f,
                AttackRange    = 2f,
            });

            // 森林守卫者 — 区域 Boss，高攻高血，掉落 Boss 水晶
            cr.Register(new CreatureDefinition
            {
                Id = "forest_guardian", DisplayName = "森林守卫者",
                Tier = CreatureTier.Boss,
                TamingMethod = TamingMethod.Knockout,   // 不可驯养
                PreferredFood = "",
                BaseHp = 1500f, BaseAttack = 35f, BaseSpeed = 3f, BaseWeight = 200f,
                HarvestResourceType = "",
                CanRide = false, CanFly = false,
                SpawnBiomes = new[] { "forest" },
                LootTable = new[] { "boss_crystal:1", "hide:5", "meat:3" },
                ViewScale = 2.5f,
                DetectionRange = 7f,    // 调试用：约 1/4 屏幕视野
                AttackRange    = 3.5f,
            });
        }
    }
}
