using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;
using SurvivalGame.Entities.Creatures;

namespace SurvivalGame.Core.Systems
{
    /// <summary>
    /// 繁殖系统 — 同种族、同主人的两只已驯服生物靠近后自动配对，
    /// 孕育 90 秒后产下后代，后代继承双亲各一个特性词条（可能突变）。
    /// </summary>
    public class BreedingSystem : SystemBase
    {
        private const float BreedRange          = 5f;    // 触发配对的距离（米）
        private const float DefaultCooldown     = 120f;  // 生育后冷却（秒）
        private const float MutationChance      = 0.10f; // 后代特性突变概率

        // 已完成孕育的实体，统一在 Tick 末尾处理（避免迭代中修改）
        private readonly List<int> _birthQueue = new();

        public override void Tick(float delta)
        {
            _birthQueue.Clear();

            foreach (var id in EcsWorld.Instance.Query<BreedingComponent, CreatureStatsComponent, PositionComponent>())
            {
                var bc    = EcsWorld.Instance.GetComponent<BreedingComponent>(id)!;
                var stats = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(id)!;
                var pos   = EcsWorld.Instance.GetComponent<PositionComponent>(id)!;
                var ai    = EcsWorld.Instance.GetComponent<AIComponent>(id);

                // 死亡不处理
                if (ai?.CurrentState == FSMState.Dead) continue;

                // ── 孕育进度 ────────────────────────────────────────────
                if (bc.IsPregnant)
                {
                    bc.PregnancyTimer += delta;
                    if (bc.PregnancyTimer >= bc.PregnancyDuration)
                        _birthQueue.Add(id);
                    continue;
                }

                // ── 冷却倒计时 ───────────────────────────────────────────
                if (bc.BreedCooldown > 0f)
                {
                    bc.BreedCooldown -= delta;
                    continue;
                }

                // ── 只有驯服生物才能繁殖 ─────────────────────────────────
                if (stats.OwnerId < 0) continue;

                // ── 寻找配对对象 ─────────────────────────────────────────
                foreach (var other in EcsWorld.Instance.Query<BreedingComponent, CreatureStatsComponent, PositionComponent>())
                {
                    if (other == id) continue;

                    var obc    = EcsWorld.Instance.GetComponent<BreedingComponent>(other)!;
                    var ostats = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(other)!;
                    var opos   = EcsWorld.Instance.GetComponent<PositionComponent>(other)!;
                    var oai    = EcsWorld.Instance.GetComponent<AIComponent>(other);

                    if (oai?.CurrentState == FSMState.Dead)    continue;
                    if (ostats.SpeciesId  != stats.SpeciesId)  continue;  // 同种
                    if (ostats.OwnerId    != stats.OwnerId)    continue;  // 同主人
                    if (obc.IsPregnant || obc.BreedCooldown > 0f) continue;

                    if (pos.Position.DistanceTo(opos.Position) > BreedRange) continue;

                    // 配对成功：本体进入孕育，伙伴进入冷却
                    bc.IsPregnant      = true;
                    bc.PregnancyTimer  = 0f;
                    bc.PartnerId       = other;
                    obc.BreedCooldown  = DefaultCooldown;

                    GD.Print($"[Breeding] 生物ID={id}({stats.SpeciesId}) 与 ID={other} 配对，孕育开始");
                    EventBus.Instance.Emit("breeding_started", id);
                    break;
                }
            }

            // ── 统一处理出生 ─────────────────────────────────────────────
            foreach (var parentId in _birthQueue)
                GiveBirth(parentId);
        }

        // ── 出生逻辑 ─────────────────────────────────────────────────────

        private void GiveBirth(int parentId)
        {
            var bc     = EcsWorld.Instance.GetComponent<BreedingComponent>(parentId);
            var stats  = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(parentId);
            var pos    = EcsWorld.Instance.GetComponent<PositionComponent>(parentId);
            if (bc == null || stats == null || pos == null) return;

            var spawner = CreatureSpawner.Instance;
            if (spawner == null) return;

            // 在亲代附近随机偏移处出生
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            float angle  = rng.RandfRange(0f, Mathf.Tau);
            float dist   = rng.RandfRange(1.5f, 3f);
            var   offset = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

            int offspringId = spawner.SpawnCreature(stats.SpeciesId, pos.Position + offset);
            if (offspringId < 0) goto Reset;

            // 后代归同一主人，立即设为已驯服并跟随
            var offTaming = EcsWorld.Instance.GetComponent<TamingComponent>(offspringId);
            var offStats  = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(offspringId);
            var offHealth = EcsWorld.Instance.GetComponent<HealthComponent>(offspringId);
            var offAI     = EcsWorld.Instance.GetComponent<AIComponent>(offspringId);

            if (offTaming != null) offTaming.State = TamingState.Tamed;
            if (offStats  != null)
            {
                offStats.OwnerId       = stats.OwnerId;
                offStats.CurrentOrder  = AIBehaviorOrder.Follow;
                InheritTraits(parentId, offspringId, offStats, offHealth, offAI, rng);
            }
            if (offAI != null)
            {
                offAI.CurrentState = FSMState.Follow;
                offAI.StateTimer   = 0f;
            }

            GD.Print($"[Breeding] 生物ID={parentId} 产下后代ID={offspringId}  词条：[{string.Join(", ", offStats?.Traits ?? new List<string>())}]");
            EventBus.Instance.Emit("creature_born", offspringId);

            Reset:
            bc.IsPregnant     = false;
            bc.PregnancyTimer = 0f;
            bc.BreedCooldown  = DefaultCooldown;
            bc.PartnerId      = -1;
        }

        // ── 特性继承 ─────────────────────────────────────────────────────

        private static void InheritTraits(int parentId, int offspringId,
            CreatureStatsComponent offStats, HealthComponent? offHealth,
            AIComponent? offAI, RandomNumberGenerator rng)
        {
            var parentStats  = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(parentId);
            int partnerId    = EcsWorld.Instance.GetComponent<BreedingComponent>(parentId)?.PartnerId ?? -1;
            var partnerStats = partnerId >= 0
                ? EcsWorld.Instance.GetComponent<CreatureStatsComponent>(partnerId)
                : null;

            // 从亲代各取一个词条（若亲代没有词条则跳过）
            var candidates = new List<string>();
            if (parentStats?.Traits.Count > 0)
                candidates.Add(parentStats.Traits[rng.RandiRange(0, parentStats.Traits.Count - 1)]);
            if (partnerStats?.Traits.Count > 0)
                candidates.Add(partnerStats.Traits[rng.RandiRange(0, partnerStats.Traits.Count - 1)]);

            // 突变：有几率从物种可能词条中额外随机一个
            var def = CreatureRegistry.Instance.Get(offStats.SpeciesId);
            if (def != null && def.PossibleTraits.Length > 0 && rng.Randf() < MutationChance)
            {
                string mutTrait = def.PossibleTraits[rng.RandiRange(0, def.PossibleTraits.Length - 1)];
                if (!candidates.Contains(mutTrait))
                {
                    candidates.Add(mutTrait);
                    GD.Print($"[Breeding] 后代ID={offspringId} 发生特性突变：{mutTrait}");
                }
            }

            // 去重后应用
            var applied = new HashSet<string>();
            foreach (var tid in candidates)
            {
                if (!applied.Add(tid)) continue;
                var trait = TraitRegistry.Instance.Get(tid);
                if (trait == null) continue;

                offStats.Traits.Add(tid);
                if (offHealth != null)
                {
                    offHealth.MaxHp    *= trait.HpMult;
                    offHealth.CurrentHp = offHealth.MaxHp;
                }
                if (offAI != null)
                {
                    offAI.AttackPower    *= trait.AttackMult;
                    offAI.MoveSpeed      *= trait.SpeedMult;
                    offAI.DetectionRange *= trait.DetectionRangeMult;
                }
            }
        }
    }
}
