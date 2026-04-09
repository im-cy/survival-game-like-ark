using Godot;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Core.Systems
{
    /// <summary>
    /// 采集系统 — 玩家按交互键时，寻找最近的可采集资源并给予物品。
    /// M1 实现：即时采集，无动画等待。
    /// </summary>
    public class HarvestSystem : SystemBase
    {
        private const float PlayerReachRange = 5f;   // 玩家脚下允许的最大距离
        private const float CursorPickRange  = 3f;   // 鼠标指向优先选取半径

        private readonly RandomNumberGenerator _rng = new();

        /// <summary>
        /// 玩家主动采集。
        /// mouseWorldPos：鼠标投影到 Y=0 平面的世界坐标，用于优先选取鼠标指向的资源。
        /// 若未提供则退化为"最近玩家"模式。
        /// </summary>
        public HarvestResult TryHarvest(int playerId, Vector3? mouseWorldPos = null)
        {
            var playerPos = EcsWorld.Instance.GetComponent<PositionComponent>(playerId);
            var playerInv = EcsWorld.Instance.GetComponent<InventoryComponent>(playerId);
            if (playerPos == null || playerInv == null) return HarvestResult.NoTarget;

            int bestId   = -1;
            float bestDist = float.MaxValue;

            foreach (var id in EcsWorld.Instance.Query<HarvestableComponent, PositionComponent>())
            {
                var h = EcsWorld.Instance.GetComponent<HarvestableComponent>(id)!;
                if (h.Depleted) continue;
                var pos = EcsWorld.Instance.GetComponent<PositionComponent>(id)!;

                // 使用 XZ 水平距离，避免地形高度差导致距离超标
                if (DistXZ(playerPos.Position, pos.Position) > PlayerReachRange) continue;

                float score = mouseWorldPos.HasValue
                    ? DistXZ(mouseWorldPos.Value, pos.Position)
                    : DistXZ(playerPos.Position, pos.Position);

                if (score < bestDist) { bestDist = score; bestId = id; }
            }

            if (bestId >= 0 && mouseWorldPos.HasValue && bestDist > CursorPickRange)
                bestId = -1;

            if (bestId < 0) return HarvestResult.NoTarget;

            return DoHarvest(bestId, playerInv, playerId);
        }

        /// <summary>
        /// 生物代理采集：直接对指定资源节点采集，产出送入指定背包（通常是主人）。
        /// </summary>
        public HarvestResult TryHarvestAt(int nodeEntityId, InventoryComponent? targetInv)
        {
            var harvestable = EcsWorld.Instance.GetComponent<HarvestableComponent>(nodeEntityId);
            if (harvestable == null || harvestable.Depleted) return HarvestResult.NoTarget;
            if (targetInv == null) return HarvestResult.NoTarget;

            return DoHarvest(nodeEntityId, targetInv, -1);
        }

        private HarvestResult DoHarvest(int nodeId, InventoryComponent inv, int harvesterId)
        {
            var harvestable = EcsWorld.Instance.GetComponent<HarvestableComponent>(nodeId)!;

            // 尸体采集：逐项给出战利品
            var corpse = EcsWorld.Instance.GetComponent<CorpseComponent>(nodeId);
            if (corpse != null)
            {
                if (corpse.RemainingLoot.Count == 0)
                {
                    harvestable.Depleted = true;
                    return HarvestResult.NoTarget;
                }

                string entry = corpse.RemainingLoot[0];
                corpse.RemainingLoot.RemoveAt(0);

                string itemId = entry;
                int qty = 1;
                var parts = entry.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int parsed))
                {
                    itemId = parts[0];
                    qty    = parsed;
                }

                var itemDef = Core.Data.ItemRegistry.Instance.Get(itemId);
                inv.AddItem(itemId, qty, itemDef?.Weight ?? 0.5f);

                harvestable.HitsRemaining = corpse.RemainingLoot.Count;
                if (harvestable.HitsRemaining <= 0)
                    harvestable.Depleted = true;

                EventBus.Instance.Emit("resource_harvested",
                    new HarvestEventData(harvesterId, nodeId, itemId, qty, harvestable.Depleted));

                GD.Print($"[Harvest] 采集尸体: {itemId} ×{qty}，剩余={harvestable.HitsRemaining}");
                return HarvestResult.Success;
            }

            // 普通资源采集
            int amount = _rng.RandiRange(harvestable.YieldMin, harvestable.YieldMax);
            var def = Core.Data.ItemRegistry.Instance.Get(harvestable.ResourceId);
            float weight = def?.Weight ?? 0.5f;
            inv.AddItem(harvestable.ResourceId, amount, weight);

            harvestable.HitsRemaining--;
            if (harvestable.HitsRemaining <= 0)
                harvestable.Depleted = true;

            EventBus.Instance.Emit("resource_harvested",
                new HarvestEventData(harvesterId, nodeId, harvestable.ResourceId, amount, harvestable.Depleted));

            GD.Print($"[Harvest] 采集 {harvestable.ResourceId} x{amount}，节点剩余次数={harvestable.HitsRemaining}");
            return HarvestResult.Success;
        }

        public override void Tick(float delta) { }

        private static float DistXZ(Vector3 a, Vector3 b)
            => new Vector2(a.X - b.X, a.Z - b.Z).Length();
    }

    public enum HarvestResult { Success, NoTarget }
    public record HarvestEventData(int PlayerId, int NodeEntityId, string ItemId, int Quantity, bool Depleted);
}
