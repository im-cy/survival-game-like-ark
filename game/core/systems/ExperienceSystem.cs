using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;

namespace SurvivalGame.Core.Systems
{
    /// <summary>
    /// 经验与进化系统。
    ///
    /// EXP 来源：
    ///   • 生物完成一次自动采集 → 80 EXP（×10 测试倍率）
    ///   • 附近（15m）有生物死亡 → 250 EXP（×10 测试倍率）
    ///
    /// 升级：每级所需 EXP = Level × 100（Lv1→2: 100, Lv4→5: 400 …）
    ///   每次升级：MaxHp × 1.05、AttackPower × 1.03
    ///
    /// 进化：达到 CreatureDefinition.EvolveLevel 时，CanEvolve = true，
    ///   玩家在范围内按 V 键调用 TryEvolve(entityId) 触发进化。
    /// </summary>
    public class ExperienceSystem : SystemBase
    {
        private const float HarvestExp  = 80f;   // 采集一次获得的 EXP（×10 测试倍率）
        private const float KillExp     = 250f;  // 附近死亡事件获得的 EXP（×10 测试倍率）
        private const float KillExpRange = 15f;  // 击杀 EXP 感知半径（米）

        public override void Initialize()
        {
            EventBus.Instance.Subscribe("creature_harvested", OnCreatureHarvested);
            EventBus.Instance.Subscribe("creature_died",      OnCreatureDied);
        }

        public override void Tick(float delta) { }  // EXP 完全由事件驱动

        // ── 事件处理 ──────────────────────────────────────────────────────

        private void OnCreatureHarvested(object? payload)
        {
            if (payload is int creatureId)
                GainExp(creatureId, HarvestExp);
        }

        private void OnCreatureDied(object? payload)
        {
            if (payload is not int deadId) return;
            var deadPos = EcsWorld.Instance.GetComponent<PositionComponent>(deadId)?.Position
                          ?? Vector3.Zero;

            foreach (var id in EcsWorld.Instance.Query<ExperienceComponent, CreatureStatsComponent, PositionComponent>())
            {
                var stats = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(id)!;
                if (stats.OwnerId < 0) continue;   // 只有驯服生物获得经验

                var pos = EcsWorld.Instance.GetComponent<PositionComponent>(id)!;
                if (pos.Position.DistanceTo(deadPos) > KillExpRange) continue;

                GainExp(id, KillExp);
            }
        }

        // ── 经验增加 + 升级 ────────────────────────────────────────────────

        private void GainExp(int entityId, float amount)
        {
            var exp   = EcsWorld.Instance.GetComponent<ExperienceComponent>(entityId);
            var stats = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(entityId);
            if (exp == null || stats == null || stats.OwnerId < 0) return;

            exp.CurrentExp += amount;

            // 连续升级（极少情况下可能一次性跨多级）
            while (exp.CurrentExp >= exp.ExpToNextLevel)
            {
                exp.CurrentExp -= exp.ExpToNextLevel;
                exp.Level++;
                LevelUp(entityId, exp, stats);
            }
        }

        private void LevelUp(int entityId, ExperienceComponent exp, CreatureStatsComponent stats)
        {
            var health = EcsWorld.Instance.GetComponent<HealthComponent>(entityId);
            var ai     = EcsWorld.Instance.GetComponent<AIComponent>(entityId);

            if (health != null)
            {
                health.MaxHp     *= 1.05f;
                health.CurrentHp  = Mathf.Min(health.CurrentHp + health.MaxHp * 0.10f, health.MaxHp);
            }
            if (ai != null)
                ai.AttackPower *= 1.03f;

            GD.Print($"[Exp] 生物ID={entityId}({stats.SpeciesId}) → Lv.{exp.Level}  " +
                     $"HP={health?.MaxHp:F0}  ATK={ai?.AttackPower:F1}");
            EventBus.Instance.Emit("creature_leveled_up", entityId);

            // 进化判定
            var def = CreatureRegistry.Instance.Get(stats.SpeciesId);
            if (def != null && def.EvolveLevel > 0 && exp.Level >= def.EvolveLevel && !exp.CanEvolve)
            {
                exp.CanEvolve = true;
                GD.Print($"[Exp] 生物ID={entityId} 可以进化了！按 V 键触发。");
                EventBus.Instance.Emit("creature_can_evolve", entityId);
            }
        }

        // ── 玩家触发进化（Player.cs 调用） ───────────────────────────────

        public void TryEvolve(int entityId)
        {
            var exp   = EcsWorld.Instance.GetComponent<ExperienceComponent>(entityId);
            var stats = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(entityId);
            if (exp == null || !exp.CanEvolve || stats == null) return;

            var def = CreatureRegistry.Instance.Get(stats.SpeciesId);
            if (def == null || string.IsNullOrEmpty(def.EvolvesTo)) return;

            var newDef = CreatureRegistry.Instance.Get(def.EvolvesTo);
            if (newDef == null)
            {
                GD.PrintErr($"[Exp] 进化目标 '{def.EvolvesTo}' 未注册！");
                return;
            }

            var health = EcsWorld.Instance.GetComponent<HealthComponent>(entityId);
            var ai     = EcsWorld.Instance.GetComponent<AIComponent>(entityId);

            float hpPct      = health != null ? health.CurrentHp / health.MaxHp : 1f;
            float levelBonus = 1f + exp.Level * 0.03f;   // 等级越高，进化后基础更强

            string oldId = stats.SpeciesId;

            // 更新物种属性
            stats.SpeciesId             = def.EvolvesTo;
            stats.CanRide               = newDef.CanRide;
            stats.CanFly                = newDef.CanFly;
            stats.HarvestResourceType   = newDef.HarvestResourceType;

            if (health != null)
            {
                health.MaxHp     = newDef.BaseHp * levelBonus;
                health.CurrentHp = health.MaxHp * hpPct;
            }
            if (ai != null)
            {
                ai.AttackPower    = newDef.BaseAttack  * levelBonus;
                ai.MoveSpeed      = newDef.BaseSpeed;
                ai.DetectionRange = newDef.DetectionRange;
                ai.AttackRange    = newDef.AttackRange;
            }

            exp.CanEvolve = false;

            // 进化后立即检查新形态是否也满足进化条件
            if (newDef.EvolveLevel > 0 && exp.Level >= newDef.EvolveLevel)
                exp.CanEvolve = true;

            GD.Print($"[Exp] 生物ID={entityId} 进化成功：{oldId} → {def.EvolvesTo}（Lv.{exp.Level}）");
            EventBus.Instance.Emit("creature_evolved", new EvolutionEventData(entityId, def.EvolvesTo));
        }
    }

    public record EvolutionEventData(int EntityId, string NewSpeciesId);
}
