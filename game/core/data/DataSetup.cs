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
            RegisterTraits();
        }

        // ── 特性词条 ───────────────────────────────────────────────────────

        private static void RegisterTraits()
        {
            var tr = TraitRegistry.Instance;

            tr.Register(new TraitDefinition { Id = "sturdy",    DisplayName = "强壮",
                HpMult = 1.30f });
            tr.Register(new TraitDefinition { Id = "fierce",    DisplayName = "凶猛",
                AttackMult = 1.30f, SpeedMult = 0.90f });
            tr.Register(new TraitDefinition { Id = "swift",     DisplayName = "敏捷",
                SpeedMult = 1.35f });
            tr.Register(new TraitDefinition { Id = "hardy",     DisplayName = "耐久",
                LoyaltyDrainMult = 0.40f });
            tr.Register(new TraitDefinition { Id = "stocky",    DisplayName = "粗壮",
                HpMult = 1.15f, SpeedMult = 0.85f });
            tr.Register(new TraitDefinition { Id = "keen",      DisplayName = "敏锐",
                DetectionRangeMult = 1.35f });
            tr.Register(new TraitDefinition { Id = "predator",  DisplayName = "掠食者",
                AttackMult = 1.20f, DetectionRangeMult = 1.20f });
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

            // ── 森林 / 草原 ──────────────────────────────────────────────

            // 野猪 Lv5→野猪王 Lv12→远古野猪
            cr.Register(new CreatureDefinition
            {
                Id = "boar", DisplayName = "野猪",
                Tier = CreatureTier.D, TamingMethod = TamingMethod.Passive, PreferredFood = "berry",
                BaseHp = 150f, BaseAttack = 10f, BaseSpeed = 3.5f, BaseWeight = 40f,
                HarvestResourceType = "wood", CanRide = false, CanFly = false,
                SpawnBiomes = new[] { "forest", "grassland" },
                PossibleTraits = new[] { "sturdy", "swift", "hardy", "stocky" },
                LootTable = new[] { "meat:2", "hide:1" },
                ViewScale = 1.0f, DetectionRange = 12f, AttackRange = 2f,
                EvolveLevel = 5, EvolvesTo = "boar_king",
                ViewColor = new Godot.Color(0.72f, 0.45f, 0.18f),
            });
            cr.Register(new CreatureDefinition
            {
                Id = "boar_king", DisplayName = "野猪王",
                Tier = CreatureTier.C, TamingMethod = TamingMethod.Passive, PreferredFood = "berry",
                BaseHp = 300f, BaseAttack = 22f, BaseSpeed = 4.0f, BaseWeight = 80f,
                HarvestResourceType = "wood", CanRide = false, CanFly = false,
                SpawnBiomes = new[] { "forest" },
                PossibleTraits = new[] { "sturdy", "fierce", "stocky" },
                LootTable = new[] { "meat:3", "hide:2" },
                ViewScale = 1.4f, DetectionRange = 13f, AttackRange = 2.5f,
                EvolveLevel = 12, EvolvesTo = "ancient_boar",
                ViewColor = new Godot.Color(0.48f, 0.22f, 0.08f),
            });
            cr.Register(new CreatureDefinition
            {
                Id = "ancient_boar", DisplayName = "远古野猪",
                Tier = CreatureTier.B, TamingMethod = TamingMethod.Passive, PreferredFood = "berry",
                BaseHp = 550f, BaseAttack = 40f, BaseSpeed = 4.5f, BaseWeight = 150f,
                HarvestResourceType = "wood", CanRide = true, CanFly = false,
                SpawnBiomes = new[] { "forest" },
                PossibleTraits = new[] { "sturdy", "fierce", "stocky" },
                LootTable = new[] { "meat:5", "hide:4", "boss_crystal:1" },
                ViewScale = 1.9f, DetectionRange = 14f, AttackRange = 3f,
                EvolveLevel = 0, EvolvesTo = "",
                ViewColor = new Godot.Color(0.25f, 0.12f, 0.06f),
            });

            // 森林守卫者 — Boss，不进化
            cr.Register(new CreatureDefinition
            {
                Id = "forest_guardian", DisplayName = "森林守卫者",
                Tier = CreatureTier.Boss, TamingMethod = TamingMethod.Knockout, PreferredFood = "",
                BaseHp = 1500f, BaseAttack = 35f, BaseSpeed = 3f, BaseWeight = 200f,
                HarvestResourceType = "", CanRide = false, CanFly = false,
                SpawnBiomes = new[] { "forest" },
                LootTable = new[] { "boss_crystal:1", "hide:5", "meat:3" },
                ViewScale = 2.5f, DetectionRange = 7f, AttackRange = 3.5f,
                EvolveLevel = 0,
                ViewColor = new Godot.Color(0.28f, 0.50f, 0.18f),
            });

            // ── 沙漠 ────────────────────────────────────────────────────

            // 沙狐 Lv5→沙漠狐灵
            cr.Register(new CreatureDefinition
            {
                Id = "sand_fox", DisplayName = "沙狐",
                Tier = CreatureTier.D, TamingMethod = TamingMethod.Passive, PreferredFood = "berry",
                BaseHp = 100f, BaseAttack = 8f, BaseSpeed = 5f, BaseWeight = 25f,
                HarvestResourceType = "", CanRide = false, CanFly = false,
                SpawnBiomes = new[] { "desert" },
                PossibleTraits = new[] { "swift", "keen", "hardy" },
                LootTable = new[] { "hide:1", "meat:1" },
                ViewScale = 0.8f, DetectionRange = 14f, AttackRange = 1.8f,
                EvolveLevel = 5, EvolvesTo = "desert_fox_spirit",
                ViewColor = new Godot.Color(0.88f, 0.68f, 0.28f),
            });
            cr.Register(new CreatureDefinition
            {
                Id = "desert_fox_spirit", DisplayName = "沙漠狐灵",
                Tier = CreatureTier.C, TamingMethod = TamingMethod.Passive, PreferredFood = "berry",
                BaseHp = 220f, BaseAttack = 18f, BaseSpeed = 7.5f, BaseWeight = 40f,
                HarvestResourceType = "", CanRide = false, CanFly = false,
                SpawnBiomes = new[] { "desert" },
                PossibleTraits = new[] { "swift", "keen", "predator" },
                LootTable = new[] { "hide:2", "meat:1" },
                ViewScale = 1.1f, DetectionRange = 18f, AttackRange = 2f,
                EvolveLevel = 0,
                ViewColor = new Godot.Color(0.95f, 0.82f, 0.15f),
            });

            // ── 雪原 ────────────────────────────────────────────────────

            // 雪狼 Lv5→霜狼 Lv12→冰霜狼王
            cr.Register(new CreatureDefinition
            {
                Id = "snow_wolf", DisplayName = "雪狼",
                Tier = CreatureTier.C, TamingMethod = TamingMethod.Passive, PreferredFood = "meat",
                BaseHp = 200f, BaseAttack = 22f, BaseSpeed = 5.5f, BaseWeight = 60f,
                HarvestResourceType = "", CanRide = false, CanFly = false,
                SpawnBiomes = new[] { "snow" },
                PossibleTraits = new[] { "fierce", "swift", "predator", "sturdy" },
                LootTable = new[] { "hide:2", "meat:2" },
                ViewScale = 1.1f, DetectionRange = 15f, AttackRange = 2.2f,
                EvolveLevel = 5, EvolvesTo = "frost_wolf",
                ViewColor = new Godot.Color(0.78f, 0.84f, 0.92f),
            });
            cr.Register(new CreatureDefinition
            {
                Id = "frost_wolf", DisplayName = "霜狼",
                Tier = CreatureTier.B, TamingMethod = TamingMethod.Passive, PreferredFood = "meat",
                BaseHp = 380f, BaseAttack = 42f, BaseSpeed = 6.8f, BaseWeight = 100f,
                HarvestResourceType = "", CanRide = false, CanFly = false,
                SpawnBiomes = new[] { "snow" },
                PossibleTraits = new[] { "fierce", "swift", "predator" },
                LootTable = new[] { "hide:3", "meat:3" },
                ViewScale = 1.5f, DetectionRange = 18f, AttackRange = 2.5f,
                EvolveLevel = 12, EvolvesTo = "blizzard_wolf",
                ViewColor = new Godot.Color(0.42f, 0.68f, 0.95f),
            });
            cr.Register(new CreatureDefinition
            {
                Id = "blizzard_wolf", DisplayName = "冰霜狼王",
                Tier = CreatureTier.A, TamingMethod = TamingMethod.Passive, PreferredFood = "meat",
                BaseHp = 700f, BaseAttack = 72f, BaseSpeed = 8.5f, BaseWeight = 180f,
                HarvestResourceType = "", CanRide = true, CanFly = false,
                SpawnBiomes = new[] { "snow" },
                PossibleTraits = new[] { "fierce", "swift", "predator" },
                LootTable = new[] { "hide:5", "meat:5", "boss_crystal:1" },
                ViewScale = 2.0f, DetectionRange = 20f, AttackRange = 3f,
                EvolveLevel = 0,
                ViewColor = new Godot.Color(0.85f, 0.92f, 1.00f),
            });

            // ── 沼泽 ────────────────────────────────────────────────────

            // 沼泽蟾蜍 Lv5→毒蟾蜍
            cr.Register(new CreatureDefinition
            {
                Id = "swamp_toad", DisplayName = "沼泽蟾蜍",
                Tier = CreatureTier.D, TamingMethod = TamingMethod.Passive, PreferredFood = "berry",
                BaseHp = 250f, BaseAttack = 12f, BaseSpeed = 2.5f, BaseWeight = 80f,
                HarvestResourceType = "", CanRide = false, CanFly = false,
                SpawnBiomes = new[] { "swamp" },
                PossibleTraits = new[] { "sturdy", "hardy", "stocky" },
                LootTable = new[] { "hide:2", "meat:3" },
                ViewScale = 1.3f, DetectionRange = 10f, AttackRange = 2.0f,
                EvolveLevel = 5, EvolvesTo = "poison_toad",
                ViewColor = new Godot.Color(0.35f, 0.50f, 0.22f),
            });
            cr.Register(new CreatureDefinition
            {
                Id = "poison_toad", DisplayName = "毒蟾蜍",
                Tier = CreatureTier.B, TamingMethod = TamingMethod.Passive, PreferredFood = "berry",
                BaseHp = 520f, BaseAttack = 28f, BaseSpeed = 3.2f, BaseWeight = 150f,
                HarvestResourceType = "", CanRide = true, CanFly = false,
                SpawnBiomes = new[] { "swamp" },
                PossibleTraits = new[] { "sturdy", "stocky", "hardy" },
                LootTable = new[] { "hide:4", "meat:4" },
                ViewScale = 1.8f, DetectionRange = 12f, AttackRange = 2.5f,
                EvolveLevel = 0,
                ViewColor = new Godot.Color(0.52f, 0.28f, 0.62f),
            });

            // ── 火山 ────────────────────────────────────────────────────

            // 火蜥蜴 Lv5→熔岩巨蜥 Lv12→火龙（可骑）
            cr.Register(new CreatureDefinition
            {
                Id = "fire_lizard", DisplayName = "火蜥蜴",
                Tier = CreatureTier.C, TamingMethod = TamingMethod.Passive, PreferredFood = "meat",
                BaseHp = 180f, BaseAttack = 18f, BaseSpeed = 3.8f, BaseWeight = 50f,
                HarvestResourceType = "", CanRide = false, CanFly = false,
                SpawnBiomes = new[] { "volcano" },
                PossibleTraits = new[] { "fierce", "sturdy", "predator", "keen" },
                LootTable = new[] { "hide:3", "meat:2" },
                ViewScale = 1.0f, DetectionRange = 11f, AttackRange = 2.0f,
                EvolveLevel = 5, EvolvesTo = "lava_lizard",
                ViewColor = new Godot.Color(0.80f, 0.32f, 0.10f),
            });
            cr.Register(new CreatureDefinition
            {
                Id = "lava_lizard", DisplayName = "熔岩巨蜥",
                Tier = CreatureTier.B, TamingMethod = TamingMethod.Passive, PreferredFood = "meat",
                BaseHp = 380f, BaseAttack = 38f, BaseSpeed = 5.0f, BaseWeight = 100f,
                HarvestResourceType = "", CanRide = false, CanFly = false,
                SpawnBiomes = new[] { "volcano" },
                PossibleTraits = new[] { "fierce", "predator", "sturdy" },
                LootTable = new[] { "hide:4", "meat:3" },
                ViewScale = 1.5f, DetectionRange = 14f, AttackRange = 2.8f,
                EvolveLevel = 12, EvolvesTo = "fire_dragon",
                ViewColor = new Godot.Color(0.65f, 0.14f, 0.05f),
            });
            cr.Register(new CreatureDefinition
            {
                Id = "fire_dragon", DisplayName = "火龙",
                Tier = CreatureTier.A, TamingMethod = TamingMethod.Passive, PreferredFood = "meat",
                BaseHp = 800f, BaseAttack = 80f, BaseSpeed = 6.5f, BaseWeight = 250f,
                HarvestResourceType = "", CanRide = true, CanFly = false,
                SpawnBiomes = new[] { "volcano" },
                PossibleTraits = new[] { "fierce", "predator", "sturdy" },
                LootTable = new[] { "hide:6", "meat:5", "boss_crystal:2" },
                ViewScale = 2.5f, DetectionRange = 18f, AttackRange = 4f,
                EvolveLevel = 0,
                ViewColor = new Godot.Color(0.92f, 0.10f, 0.05f),
            });
        }
    }
}
