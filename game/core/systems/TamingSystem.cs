using Godot;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Core.Systems
{
    /// <summary>
    /// 驯养系统 — 处理信任度增长、麻醉衰减、忠诚度衰减、驯养完成。
    /// </summary>
    public class TamingSystem : SystemBase
    {
        private const float LoyaltyDrainPerSec = 0.5f / 60f;   // ~0.5/分钟

        public override void Tick(float delta)
        {
            foreach (var entityId in World.Instance.Query<TamingComponent>())
            {
                var t = World.Instance.GetComponent<TamingComponent>(entityId)!;

                switch (t.State)
                {
                    case TamingState.Sedated:
                        t.SedationTimer -= delta;
                        if (t.SedationTimer <= 0f)
                        {
                            t.State = TamingState.Hostile;
                            t.TrustProgress = 0f;   // 麻醉耗尽 → 驯养失败
                        }
                        break;

                    case TamingState.Tamed:
                        var stats = World.Instance.GetComponent<CreatureStatsComponent>(entityId);
                        if (stats == null) break;
                        stats.Loyalty = Mathf.Max(0f, stats.Loyalty - LoyaltyDrainPerSec * delta);
                        if (stats.Loyalty <= 0f)
                            AbandonCreature(entityId, stats);
                        break;
                }
            }
        }

        // ── 玩家操作 ──────────────────────────────────────────────────────

        /// <summary>玩家尝试喂食（被动驯养）</summary>
        public TamingFeedResult TryFeedCreature(int playerId, int creatureId)
        {
            var taming = World.Instance.GetComponent<TamingComponent>(creatureId);
            var inv    = World.Instance.GetComponent<InventoryComponent>(playerId);
            if (taming == null || inv == null) return TamingFeedResult.InvalidTarget;
            if (taming.State == TamingState.Hostile) return TamingFeedResult.Hostile;
            if (taming.State == TamingState.Tamed)   return TamingFeedResult.AlreadyTamed;

            // 优先用偏好食物
            bool hasPreferred = inv.CountItem(taming.PreferredFood) > 0;
            string foodId = hasPreferred ? taming.PreferredFood : FindAnyFood(inv);
            if (foodId == "") return TamingFeedResult.NoFood;

            inv.RemoveItem(foodId, 1);

            float gain = hasPreferred ? 15f : 5f;
            taming.TrustProgress = Mathf.Min(100f, taming.TrustProgress + gain);
            taming.State = TamingState.Bonding;

            if (taming.TrustProgress >= 100f)
            {
                CompleteTaming(playerId, creatureId);
                return TamingFeedResult.TamingComplete;
            }
            return TamingFeedResult.Success;
        }

        /// <summary>麻醉击晕生物（击晕驯养第一步）</summary>
        public void Sedate(int creatureId, float sedationDuration)
        {
            var taming = World.Instance.GetComponent<TamingComponent>(creatureId);
            if (taming == null) return;
            taming.State = TamingState.Sedated;
            taming.SedationTimer += sedationDuration;
        }

        /// <summary>驯服后补充麻醉时间</summary>
        public void AddSedation(int creatureId, float amount)
        {
            var taming = World.Instance.GetComponent<TamingComponent>(creatureId);
            if (taming?.State == TamingState.Sedated)
                taming.SedationTimer = Mathf.Min(taming.SedationTimer + amount, 300f);
        }

        /// <summary>生物受伤时降低驯养效率</summary>
        public void OnCreatureHitDuringTaming(int creatureId, float damageTaken)
        {
            var taming = World.Instance.GetComponent<TamingComponent>(creatureId);
            if (taming == null || taming.State == TamingState.Tamed) return;
            taming.TamingEffectiveness = Mathf.Max(0f, taming.TamingEffectiveness - damageTaken * 0.5f);
        }

        // ── 私有方法 ─────────────────────────────────────────────────────

        private void CompleteTaming(int playerId, int creatureId)
        {
            var taming = World.Instance.GetComponent<TamingComponent>(creatureId)!;
            var stats  = World.Instance.GetComponent<CreatureStatsComponent>(creatureId)!;
            var health = World.Instance.GetComponent<HealthComponent>(creatureId)!;

            taming.State  = TamingState.Tamed;
            stats.OwnerId = playerId;
            stats.CurrentOrder = AIBehaviorOrder.Follow;

            // 驯养效率影响最终属性上限
            float eff = taming.TamingEffectiveness / 100f;
            health.MaxHp *= eff;
            health.CurrentHp = health.MaxHp;

            EventBus.Instance.Emit("creature_tamed", new TamedEventData(playerId, creatureId));
        }

        private void AbandonCreature(int entityId, CreatureStatsComponent stats)
        {
            var taming = World.Instance.GetComponent<TamingComponent>(entityId)!;
            taming.State   = TamingState.Wild;
            stats.OwnerId  = -1;
            stats.Loyalty  = 100f;
            stats.CurrentOrder = AIBehaviorOrder.Wander;
            EventBus.Instance.Emit("creature_abandoned", entityId);
        }

        private string FindAnyFood(InventoryComponent inv)
        {
            // 简单实现：找第一个 "food_" 前缀物品
            foreach (var item in inv.Items)
                if (item.ItemId.StartsWith("food_")) return item.ItemId;
            return "";
        }
    }

    public enum TamingFeedResult { Success, TamingComplete, NoFood, Hostile, AlreadyTamed, InvalidTarget }
    public record TamedEventData(int PlayerId, int CreatureId);
}
