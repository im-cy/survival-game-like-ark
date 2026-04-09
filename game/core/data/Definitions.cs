using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Core.Data
{
    // ═══════════════════════════════════════════════════════════════════
    // 物品定义（不可变模板，运行时只读）
    // ═══════════════════════════════════════════════════════════════════

    [GlobalClass]
    public partial class ItemDefinition : Resource
    {
        [Export] public string Id = "";
        [Export] public string DisplayName = "";
        [Export] public Texture2D? Icon;
        [Export] public ItemCategory Category = ItemCategory.Material;
        [Export] public int MaxStackSize = 50;
        [Export] public float Weight = 0.5f;
        [Export] public float DurabilityMax = -1f;      // -1 = 无耐久
        [Export] public float HungerRestore = 0f;
        [Export] public float ThirstRestore = 0f;
        [Export] public float SedationAmount = 0f;      // 麻醉量（麻醉箭等）
        [Export] public float AttackDamage      = 0f;     // 武器伤害（0=不是武器）
        [Export] public float ArmorValue       = 0f;     // 防御值，每次受击减免此值（0=不是防具）
        [Export] public bool  IsRanged         = false;  // 是否为远程武器
        [Export] public float ProjectileRange  = 20f;   // 远程最大射程（米）
        [Export] public string AmmoId          = "";    // 弹药物品 ID（""=无需弹药）
    }

    public enum ItemCategory { Material, Food, Tool, Weapon, Armor, Building, Medicine, Blueprint }

    // ═══════════════════════════════════════════════════════════════════
    // 配方定义
    // ═══════════════════════════════════════════════════════════════════

    [GlobalClass]
    public partial class RecipeDefinition : Resource
    {
        [Export] public string Id = "";
        [Export] public string ResultItemId = "";
        [Export] public int ResultQuantity = 1;
        [Export] public string RequiredStation = "";    // "" = 徒手
        [Export] public int RequiredLevel = 1;
        [Export] public string UnlockSource = "level"; // "level" | "boss:forest" | "blueprint"

        // 纯 C# List，避免 Godot.Collections.Array 对 Resource 对象的序列化问题
        public List<IngredientEntry> Ingredients { get; } = new();
    }

    // 普通 C# 类（不继承 Resource），彻底绕开 Godot Variant 序列化
    public class IngredientEntry
    {
        public string ItemId   = "";
        public int    Quantity = 1;

        public IngredientEntry() { }
        public IngredientEntry(string itemId, int quantity)
        {
            ItemId   = itemId;
            Quantity = quantity;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 生物定义
    // ═══════════════════════════════════════════════════════════════════

    [GlobalClass]
    public partial class CreatureDefinition : Resource
    {
        [Export] public string Id = "";
        [Export] public string DisplayName = "";
        [Export] public Texture2D? SpriteSheet;
        [Export] public CreatureTier Tier = CreatureTier.D;
        [Export] public TamingMethod TamingMethod = TamingMethod.Passive;
        [Export] public string PreferredFood = "";

        // 基础属性（出生/初始值）
        [Export] public float BaseHp     = 200f;
        [Export] public float BaseAttack = 15f;
        [Export] public float BaseSpeed  = 4f;
        [Export] public float BaseWeight = 50f;  // 最大负重

        [Export] public string[] PossibleTraits = System.Array.Empty<string>();
        [Export] public string[] SpawnBiomes    = System.Array.Empty<string>();

        // 驯服后能力
        [Export] public bool CanRide = false;
        [Export] public bool CanFly  = false;
        [Export] public string HarvestResourceType = "";  // "" = 不能自动采集

        // 非 Export — 代码注册时设置
        public string[] LootTable = System.Array.Empty<string>(); // 格式: "itemId:qty"
        public float ViewScale      = 1f;    // 占位体缩放倍率
        public float DetectionRange = 12f;   // 仇恨触发距离（m）
        public float AttackRange    = 2f;    // 近战攻击距离（m）

        // 进化链
        public string EvolvesTo   = "";  // 进化后的物种 ID（"" = 无）
        public int    EvolveLevel = 0;   // 达到此等级可进化（0 = 不可进化）

        // 占位体颜色（无贴图时显示的颜色）
        public Godot.Color ViewColor = new Godot.Color(0.72f, 0.45f, 0.18f);
    }

    public enum CreatureTier { F, D, C, B, A, S, Boss }

    // ═══════════════════════════════════════════════════════════════════
    // 特性词条定义（驯养完成时随机赋予）
    // ═══════════════════════════════════════════════════════════════════

    public class TraitDefinition
    {
        public string Id                 = "";
        public string DisplayName        = "";   // 中文显示名
        public float  HpMult             = 1f;   // 最大 HP 倍率
        public float  AttackMult         = 1f;   // 攻击力倍率
        public float  SpeedMult          = 1f;   // 移速倍率
        public float  LoyaltyDrainMult   = 1f;   // 忠诚衰减倍率（< 1 = 衰减更慢）
        public float  DetectionRangeMult = 1f;   // 感知范围倍率
    }

    // ═══════════════════════════════════════════════════════════════════
    // 群系定义
    // ═══════════════════════════════════════════════════════════════════

    [GlobalClass]
    public partial class BiomeDefinition : Resource
    {
        [Export] public string Id = "";
        [Export] public string DisplayName = "";
        [Export] public float BaseTemperature = 22f;
        [Export] public Color GroundTint = Colors.Green;
        [Export] public string[] ResourceSpawnTable = System.Array.Empty<string>();
        [Export] public string[] CreatureSpawnTable  = System.Array.Empty<string>();
    }
}
