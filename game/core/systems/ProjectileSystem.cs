using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Core.Systems
{
    /// <summary>
    /// 弹射物系统 — 每帧推进弹射物位置，检测命中并交由 CombatSystem 处理伤害。
    /// 弹射物超出射程或命中目标后立即销毁，并通知视觉层（projectile_expired 事件）。
    /// </summary>
    public class ProjectileSystem : SystemBase
    {
        private const float HitRadius = 0.9f;   // 命中判定半径（米）

        private readonly List<int> _toDestroy = new();

        public override void Tick(float delta)
        {
            _toDestroy.Clear();

            foreach (var id in EcsWorld.Instance.Query<ProjectileComponent, PositionComponent>())
            {
                var proj = EcsWorld.Instance.GetComponent<ProjectileComponent>(id)!;
                var pos  = EcsWorld.Instance.GetComponent<PositionComponent>(id)!;

                float step = proj.Speed * delta;
                pos.Position         += proj.Direction * step;
                proj.TraveledDistance += step;

                // ── 碰撞检测 ────────────────────────────────────────────
                bool hit = false;
                foreach (var targetId in EcsWorld.Instance.Query<HealthComponent, PositionComponent, AIComponent>())
                {
                    if (targetId == proj.OwnerId) continue;

                    var ai = EcsWorld.Instance.GetComponent<AIComponent>(targetId)!;
                    if (ai.CurrentState == FSMState.Dead) continue;

                    var hp = EcsWorld.Instance.GetComponent<HealthComponent>(targetId)!;
                    if (!hp.IsAlive) continue;

                    var tpos = EcsWorld.Instance.GetComponent<PositionComponent>(targetId)!;
                    if (pos.Position.DistanceTo(tpos.Position) > HitRadius) continue;

                    // 命中：交由 CombatSystem 处理伤害与死亡逻辑
                    GameManager.Instance?.Combat?.DealDamageToCreature(targetId, proj.OwnerId, proj.Damage);
                    GD.Print($"[Projectile] 箭矢ID={id} 命中 生物ID={targetId}");
                    hit = true;
                    break;
                }

                if (hit || proj.TraveledDistance >= proj.MaxRange)
                    _toDestroy.Add(id);
            }

            // 统一销毁（避免迭代中修改集合）
            foreach (var id in _toDestroy)
            {
                EventBus.Instance.Emit("projectile_expired", id);
                if (EcsWorld.Instance.EntityExists(id))
                    EcsWorld.Instance.DestroyEntity(id);
            }
        }
    }
}
