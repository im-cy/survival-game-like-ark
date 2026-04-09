using System.Collections.Generic;
using Godot;

namespace SurvivalGame.Core.ECS
{
    // ═══════════════════════════════════════════════════════════════════
    // 通用组件
    // ═══════════════════════════════════════════════════════════════════

    public class PositionComponent
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float FacingAngle;   // 朝向（弧度）
    }

    public class HealthComponent
    {
        public float MaxHp = 100f;
        public float CurrentHp = 100f;
        public bool IsAlive => CurrentHp > 0f;

        public void TakeDamage(float amount) =>
            CurrentHp = Mathf.Max(0f, CurrentHp - amount);

        public void Heal(float amount) =>
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
    }

    public class InventoryComponent
    {
        public List<ItemInstance> Items = new();
        public float MaxWeight = 50f;

        public float CurrentWeight
        {
            get
            {
                float w = 0f;
                foreach (var item in Items) w += item.Weight;
                return w;
            }
        }

        public int CountItem(string itemId)
        {
            int total = 0;
            foreach (var item in Items)
                if (item.ItemId == itemId) total += item.Quantity;
            return total;
        }

        public ItemInstance? FindItem(string itemId)
        {
            foreach (var item in Items)
                if (item.ItemId == itemId) return item;
            return null;
        }

        public bool RemoveItem(string itemId, int quantity)
        {
            int remaining = quantity;
            for (int i = Items.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (Items[i].ItemId != itemId) continue;
                int take = Mathf.Min(Items[i].Quantity, remaining);
                Items[i].Quantity -= take;
                remaining -= take;
                if (Items[i].Quantity <= 0) Items.RemoveAt(i);
            }
            return remaining == 0;
        }

        public void AddItem(string itemId, int quantity, float weightPerUnit)
        {
            var existing = FindItem(itemId);
            if (existing != null)
                existing.Quantity += quantity;
            else
                Items.Add(new ItemInstance(itemId, quantity, weightPerUnit));
        }
    }

    public class ItemInstance
    {
        public string ItemId;
        public int Quantity;
        public float WeightPerUnit;
        public float Weight => WeightPerUnit * Quantity;

        public ItemInstance(string itemId, int quantity, float weightPerUnit)
        {
            ItemId = itemId;
            Quantity = quantity;
            WeightPerUnit = weightPerUnit;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 玩家专属组件
    // ═══════════════════════════════════════════════════════════════════

    public class SurvivalComponent
    {
        public float Hunger = 100f;         // 归零 → 扣血
        public float Thirst = 100f;         // 归零 → 扣血更快
        public float Temperature = 37f;     // 正常范围 35–39
        public float Stamina = 100f;        // 耗尽 → 移速/战斗惩罚
        public bool IsSprinting = false;
        public int RidingEntityId = -1;             // -1 = 未骑乘；≥0 = 正在骑乘该实体

        public float HungerDrainRate = 1f / 60f;    // per second
        public float ThirstDrainRate = 1.5f / 60f;
        public float StaminaDrainRate = 20f;         // per second (sprinting)
        public float StaminaRegenRate = 10f;
    }

    public class PlayerStatsComponent
    {
        public int Level = 1;
        public int EngramPoints = 0;        // 解锁配方的点数
        public HashSet<string> UnlockedRecipes = new();
        public HashSet<string> UnlockedBlueprints = new();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 生物组件
    // ═══════════════════════════════════════════════════════════════════

    public class TamingComponent
    {
        public TamingState State = TamingState.Wild;
        public float TrustProgress = 0f;            // 0–100
        public string PreferredFood = "";
        public TamingMethod Method = TamingMethod.Passive;
        public float SedationTimer = 0f;
        public float TamingEffectiveness = 100f;    // 受伤后降低，影响最终属性
    }

    public class CreatureStatsComponent
    {
        public string SpeciesId = "";
        public int Level = 1;
        public int OwnerId = -1;                    // -1 = 野生
        public float Loyalty = 100f;                // 归零 → 离队
        public AIBehaviorOrder CurrentOrder = AIBehaviorOrder.Wander;
        public List<string> Traits = new();         // 特性词条
        public bool CanRide = false;
        public bool CanFly = false;
        public string? HarvestResourceType = null;
    }

    public class AIComponent
    {
        public AITier Tier = AITier.Dormant;
        public FSMState CurrentState = FSMState.Wander;
        public int TargetEntityId = -1;
        public Vector3 Destination;
        public float StateTimer = 0f;
        public float DetectionRange = 10f;
        public float AttackRange = 2f;
        public float AttackPower = 10f;      // 每次攻击伤害
        public float AttackCooldown = 1.5f;  // 攻击冷却（秒）
        public float AttackTimer = 0f;       // 距下次攻击还需等待的时间
        public float AggroIgnoreDistTimer = 0f; // 远程命中后无视距离限制的持续时间
        public float MoveSpeed = 4f;            // 基础移速（米/秒），特性词条可修改
    }

    // ═══════════════════════════════════════════════════════════════════
    // 弹射物组件（箭矢等飞行弹射物）
    // ═══════════════════════════════════════════════════════════════════

    public class ProjectileComponent
    {
        public int     OwnerId          = -1;
        public Vector3 Direction;                // 归一化飞行方向
        public float   Speed            = 22f;   // 飞行速度（米/秒）
        public float   Damage           = 30f;   // 命中伤害
        public float   MaxRange         = 22f;   // 最大射程（米）
        public float   TraveledDistance = 0f;    // 已飞行距离
    }

    // ═══════════════════════════════════════════════════════════════════
    // 经验/等级/进化组件（驯服生物通过采集与战斗获取经验）
    // ═══════════════════════════════════════════════════════════════════

    public class ExperienceComponent
    {
        public int   Level      = 1;
        public float CurrentExp = 0f;
        public bool  CanEvolve  = false;   // 已满足进化条件，等待玩家触发（V 键）

        /// <summary>升级所需经验值：随等级线性递增</summary>
        public float ExpToNextLevel => Level * 100f;   // Lv1→2: 100, Lv2→3: 200, Lv4→5: 400 …
    }

    // ═══════════════════════════════════════════════════════════════════
    // 繁殖组件（驯服生物可进行繁殖，产下后代）
    // ═══════════════════════════════════════════════════════════════════

    public class BreedingComponent
    {
        public float BreedCooldown     = 0f;    // 下次可繁殖的冷却倒计时（秒）
        public bool  IsPregnant        = false;
        public float PregnancyTimer    = 0f;    // 孕育进度（秒）
        public float PregnancyDuration = 90f;   // 孕育总时长（秒）
        public int   PartnerId         = -1;    // 配对伙伴的实体 ID
    }

    // ═══════════════════════════════════════════════════════════════════
    // 尸体战利品组件（死亡生物携带，可用 E 键逐项采集）
    // ═══════════════════════════════════════════════════════════════════

    public class CorpseComponent
    {
        public List<string> RemainingLoot = new();   // "itemId:qty" 格式，逐项采集
    }

    // ═══════════════════════════════════════════════════════════════════
    // 装备组件
    // ═══════════════════════════════════════════════════════════════════

    public class EquipmentComponent
    {
        public string? WeaponId = null;   // 当前装备的武器/工具 ID（null = 空手）
        public string? ChestId  = null;   // 当前装备的胸甲 ID（null = 无甲）
    }

    // ═══════════════════════════════════════════════════════════════════
    // 资源 / 采集组件
    // ═══════════════════════════════════════════════════════════════════

    public class HarvestableComponent
    {
        public string ResourceId = "wood";   // 掉落物品 ID
        public int YieldMin = 1;
        public int YieldMax = 3;
        public int HitsRemaining = 3;        // 还能被采几次
        public bool Depleted = false;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 营火组件
    // ═══════════════════════════════════════════════════════════════════

    public class CampfireComponent
    {
        public bool IsLit = true;
        public float FuelRemaining = 120f;  // 秒
        public float WarmthRadius = 5f;     // 米
        public float WarmthBonus = 20f;     // 加温度（°C）
    }

    // ═══════════════════════════════════════════════════════════════════
    // 建造件组件
    // ═══════════════════════════════════════════════════════════════════

    public class BuildingPieceComponent
    {
        public string PieceId = "";
        public float HpMax = 500f;
        public float HpCurrent = 500f;
        public BuildingTier Tier = BuildingTier.Thatch;
        public BuildingPieceType PieceType = BuildingPieceType.House;
        public bool IsStable = true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 枚举
    // ═══════════════════════════════════════════════════════════════════

    public enum TamingState { Wild, Alert, Cautious, Bonding, Sedated, Tamed }
    public enum TamingMethod { Passive, Knockout, Challenge }
    public enum AITier { Active, Passive, Dormant }
    public enum FSMState { Wander, Alert, Hostile, Cautious, Follow, Guard, Harvest, Flee, Dead, Mounted }
    public enum AIBehaviorOrder { Wander, Follow, Guard, Harvest, Stay }
    public enum BuildingTier { Thatch, Wood }
    public enum BuildingPieceType { House, Special }
}
