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
    }

    // ═══════════════════════════════════════════════════════════════════
    // 枚举
    // ═══════════════════════════════════════════════════════════════════

    public enum TamingState { Wild, Alert, Cautious, Bonding, Sedated, Tamed }
    public enum TamingMethod { Passive, Knockout, Challenge }
    public enum AITier { Active, Passive, Dormant }
    public enum FSMState { Wander, Alert, Hostile, Cautious, Follow, Guard, Harvest, Flee, Dead }
    public enum AIBehaviorOrder { Wander, Follow, Guard, Harvest, Stay }
}
