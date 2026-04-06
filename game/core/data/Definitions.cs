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
        [Export] public Godot.Collections.Array<IngredientEntry> Ingredients = new();
        [Export] public string RequiredStation = "";    // "" = 徒手
        [Export] public int RequiredLevel = 1;
        [Export] public string UnlockSource = "level"; // "level" | "boss:forest" | "blueprint"
    }

    [GlobalClass]
    public partial class IngredientEntry : Resource
    {
        [Export] public string ItemId = "";
        [Export] public int Quantity = 1;
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
    }

    public enum CreatureTier { F, D, C, B, A, S, Boss }

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
