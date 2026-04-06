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
        private const float HarvestRange = 2.5f;    // 米

        private readonly RandomNumberGenerator _rng = new();

        public HarvestResult TryHarvest(int playerId)
        {
            var playerPos = EcsWorld.Instance.GetComponent<PositionComponent>(playerId);
            var playerInv = EcsWorld.Instance.GetComponent<InventoryComponent>(playerId);
            if (playerPos == null || playerInv == null) return HarvestResult.NoTarget;

            // 找最近的未耗尽资源节点
            int bestId = -1;
            float bestDist = HarvestRange;
            foreach (var id in EcsWorld.Instance.Query<HarvestableComponent, PositionComponent>())
            {
                var h = EcsWorld.Instance.GetComponent<HarvestableComponent>(id)!;
                if (h.Depleted) continue;
                var pos = EcsWorld.Instance.GetComponent<PositionComponent>(id)!;
                float dist = playerPos.Position.DistanceTo(pos.Position);
                if (dist < bestDist) { bestDist = dist; bestId = id; }
            }

            if (bestId < 0) return HarvestResult.NoTarget;

            var harvestable = EcsWorld.Instance.GetComponent<HarvestableComponent>(bestId)!;
            int qty = _rng.RandiRange(harvestable.YieldMin, harvestable.YieldMax);
            playerInv.AddItem(harvestable.ResourceId, qty, 0.5f);

            harvestable.HitsRemaining--;
            if (harvestable.HitsRemaining <= 0)
                harvestable.Depleted = true;

            EventBus.Instance.Emit("resource_harvested",
                new HarvestEventData(playerId, bestId, harvestable.ResourceId, qty, harvestable.Depleted));

            GD.Print($"[Harvest] 采集 {harvestable.ResourceId} x{qty}，节点剩余次数={harvestable.HitsRemaining}");
            return HarvestResult.Success;
        }

        public override void Tick(float delta) { }
    }

    public enum HarvestResult { Success, NoTarget }
    public record HarvestEventData(int PlayerId, int NodeEntityId, string ItemId, int Quantity, bool Depleted);
}
