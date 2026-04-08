using Godot;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Core.Systems
{
    /// <summary>
    /// 营火系统 — 消耗燃料，燃尽后熄灭并广播事件。
    /// </summary>
    public class CampfireSystem : SystemBase
    {
        public override void Tick(float delta)
        {
            foreach (var id in EcsWorld.Instance.Query<CampfireComponent>())
            {
                var cf = EcsWorld.Instance.GetComponent<CampfireComponent>(id)!;
                if (!cf.IsLit) continue;

                cf.FuelRemaining -= delta;
                if (cf.FuelRemaining <= 0f)
                {
                    cf.IsLit = false;
                    cf.FuelRemaining = 0f;
                    EventBus.Instance.Emit("campfire_extinguished", id);
                    GD.Print($"[CampfireSystem] 营火 EntityId={id} 已熄灭");
                }
            }
        }
    }
}
