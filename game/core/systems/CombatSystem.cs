using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;

namespace SurvivalGame.Core.Systems
{
    /// <summary>
    /// 战斗系统 — 处理玩家攻击（鼠标左键）和生物近战攻击。
    /// 生物死亡后尸体保留 CorpseLifetime 秒再消失；
    /// 击杀瞬间将掉落物直接放入击杀者背包。
    /// </summary>
    public class CombatSystem : SystemBase
    {
        public static CombatSystem? Instance { get; private set; }

        private const float PlayerAttackRange   = 4f;
        private const float PlayerAttackDamage  = 25f;
        private const float PlayerAttackCooldown = 0.6f;
        private const float CorpseLifetime       = 20f;   // 尸体保留秒数

        private float _playerAttackTimer = 0f;

        // entityId → 已死亡秒数
        private readonly Dictionary<int, float> _corpseTimers = new();

        public override void Initialize()
        {
            Instance = this;
            EventBus.Instance.Subscribe("creature_attack",    OnCreatureAttack);
            EventBus.Instance.Subscribe("resource_harvested", OnResourceHarvested);
        }

        public override void Tick(float delta)
        {
            if (_playerAttackTimer > 0f)
                _playerAttackTimer -= delta;

            // ── 尸体计时器 ──────────────────────────────────────────────
            var keys = new List<int>(_corpseTimers.Keys);
            foreach (var id in keys)
            {
                _corpseTimers[id] += delta;
                if (_corpseTimers[id] < CorpseLifetime) continue;

                _corpseTimers.Remove(id);
                if (!EcsWorld.Instance.EntityExists(id)) continue;

                EventBus.Instance.Emit("creature_died", id);
                EcsWorld.Instance.DestroyEntity(id);
            }
        }

        // ── 生物攻击玩家 ──────────────────────────────────────────────────

        private void OnCreatureAttack(object? payload)
        {
            if (payload is not AttackEventData data) return;

            foreach (var playerId in EcsWorld.Instance.Query<SurvivalComponent>())
            {
                var hp = EcsWorld.Instance.GetComponent<HealthComponent>(playerId);
                if (hp == null || !hp.IsAlive) continue;

                var attacker = EcsWorld.Instance.GetComponent<AIComponent>(data.AttackerId);
                float dmg = attacker?.AttackPower ?? 10f;

                // 装备胸甲时减免伤害
                var equip = EcsWorld.Instance.GetComponent<EquipmentComponent>(playerId);
                if (equip?.ChestId != null)
                {
                    var armorDef = ItemRegistry.Instance.Get(equip.ChestId);
                    if (armorDef != null)
                        dmg = Mathf.Max(1f, dmg - armorDef.ArmorValue);
                }

                hp.TakeDamage(dmg);
                EventBus.Instance.Emit("player_damaged", dmg);
                GD.Print($"[Combat] 生物ID={data.AttackerId} 攻击玩家  伤害={dmg}  玩家HP={hp.CurrentHp:F0}/{hp.MaxHp:F0}");
                break;
            }
        }

        // ── 玩家攻击（近战 / 远程统一入口）────────────────────────────────

        private const float RangedAttackCooldown = 1.0f;   // 弓箭射击冷却

        public bool TryPlayerAttack(int playerId, Vector3 aimPos)
        {
            if (_playerAttackTimer > 0f) return false;

            var equip     = EcsWorld.Instance.GetComponent<EquipmentComponent>(playerId);
            var weaponDef = equip?.WeaponId != null ? ItemRegistry.Instance.Get(equip.WeaponId) : null;

            // 远程武器走弹射物流程
            if (weaponDef?.IsRanged == true)
                return TryRangedAttack(playerId, aimPos, weaponDef);

            // ── 近战攻击 ────────────────────────────────────────────────
            var playerPos = EcsWorld.Instance.GetComponent<PositionComponent>(playerId);
            if (playerPos == null) return false;

            int targetId   = -1;
            float bestDist = PlayerAttackRange;
            foreach (var id in EcsWorld.Instance.Query<HealthComponent, PositionComponent, AIComponent>())
            {
                var hp = EcsWorld.Instance.GetComponent<HealthComponent>(id)!;
                if (!hp.IsAlive) continue;
                var ai = EcsWorld.Instance.GetComponent<AIComponent>(id)!;
                if (ai.CurrentState == FSMState.Dead) continue;
                var pos = EcsWorld.Instance.GetComponent<PositionComponent>(id)!;
                if (playerPos.Position.DistanceTo(pos.Position) > PlayerAttackRange) continue;
                float d = aimPos.DistanceTo(pos.Position);
                if (d < bestDist) { bestDist = d; targetId = id; }
            }
            if (targetId < 0) return false;

            float damage = weaponDef?.AttackDamage > 0f ? weaponDef.AttackDamage : PlayerAttackDamage;
            _playerAttackTimer = PlayerAttackCooldown;
            DealDamageToCreature(targetId, playerId, damage);
            return true;
        }

        // ── 远程攻击：消耗箭矢，创建弹射物实体 ──────────────────────────

        private bool TryRangedAttack(int playerId, Vector3 aimPos, ItemDefinition weaponDef)
        {
            var inv = EcsWorld.Instance.GetComponent<InventoryComponent>(playerId);
            if (inv == null) return false;

            // 检查弹药
            if (!string.IsNullOrEmpty(weaponDef.AmmoId) && inv.CountItem(weaponDef.AmmoId) <= 0)
            {
                GD.Print("[Combat] 弓箭：没有箭矢");
                return false;
            }

            var playerPos = EcsWorld.Instance.GetComponent<PositionComponent>(playerId);
            if (playerPos == null) return false;

            // 消耗 1 枚箭矢
            if (!string.IsNullOrEmpty(weaponDef.AmmoId))
                inv.RemoveItem(weaponDef.AmmoId, 1);

            // 计算飞行方向（投影到 XZ 平面）
            var dir = new Vector3(aimPos.X - playerPos.Position.X, 0f,
                                  aimPos.Z - playerPos.Position.Z);
            if (dir.LengthSquared() < 0.001f) dir = -Vector3.Forward;
            dir = dir.Normalized();

            // 创建弹射物 ECS 实体（略微偏移，避免立即命中自己）
            int projId = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(projId, new PositionComponent
                { Position = playerPos.Position + dir * 0.7f });
            EcsWorld.Instance.AddComponent(projId, new ProjectileComponent
            {
                OwnerId  = playerId,
                Direction = dir,
                Speed    = 22f,
                Damage   = weaponDef.AttackDamage > 0f ? weaponDef.AttackDamage : 30f,
                MaxRange = weaponDef.ProjectileRange,
            });

            _playerAttackTimer = RangedAttackCooldown;
            EventBus.Instance.Emit("projectile_fired", projId);
            GD.Print($"[Combat] 射出箭矢ID={projId}  方向=({dir.X:F2},{dir.Z:F2})  剩余箭矢={inv.CountItem(weaponDef.AmmoId)}");
            return true;
        }

        // ── 通用伤害接口（近战 & 弹射物共用）────────────────────────────

        public void DealDamageToCreature(int targetId, int attackerId, float damage)
        {
            var hp = EcsWorld.Instance.GetComponent<HealthComponent>(targetId);
            if (hp == null || !hp.IsAlive) return;
            var ai = EcsWorld.Instance.GetComponent<AIComponent>(targetId);
            if (ai?.CurrentState == FSMState.Dead) return;

            hp.TakeDamage(damage);
            GD.Print($"[Combat] ID={targetId}  受到伤害={damage}  HP={hp.CurrentHp:F0}/{hp.MaxHp:F0}");
            EventBus.Instance.Emit("creature_damaged", new DamageEventData(targetId, damage));

            if (!hp.IsAlive)
                HandleCreatureKilled(targetId, attackerId);
        }

        // ── 生物死亡处理 ──────────────────────────────────────────────────

        private void HandleCreatureKilled(int entityId, int killerId)
        {
            var stats = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(entityId);
            string speciesId = stats?.SpeciesId ?? "";
            var def = CreatureRegistry.Instance.Get(speciesId);

            // Boss 专属事件（击败后解锁二阶配方）
            if (def?.Tier == CreatureTier.Boss)
            {
                GD.Print($"[Combat] Boss [{speciesId}] 已被击败！");
                EventBus.Instance.Emit("boss_defeated", speciesId);
            }

            // 将战利品存入尸体，供玩家用 E 键采集（不再自动掉落）
            if (def != null && def.LootTable.Length > 0)
            {
                var corpse = new CorpseComponent();
                corpse.RemainingLoot.AddRange(def.LootTable);
                EcsWorld.Instance.AddComponent(entityId, corpse);
                EcsWorld.Instance.AddComponent(entityId, new HarvestableComponent
                {
                    ResourceId    = "corpse",
                    HitsRemaining = def.LootTable.Length,
                    YieldMin      = 0,
                    YieldMax      = 0,
                    Depleted      = false,
                });
                GD.Print($"[Combat] 生物ID={entityId} 已死亡，可采集 {def.LootTable.Length} 种素材");
            }
            else
            {
                GD.Print($"[Combat] 生物ID={entityId} 已死亡，无战利品");
            }

            // 标记 FSM 为死亡，启动尸体计时器（超时自动销毁）
            var ai = EcsWorld.Instance.GetComponent<AIComponent>(entityId);
            if (ai != null) ai.CurrentState = FSMState.Dead;

            _corpseTimers[entityId] = 0f;
        }

        // 尸体被完全采集后立即销毁（无需等计时器）
        private void OnResourceHarvested(object? payload)
        {
            if (payload is not HarvestEventData data) return;
            if (!data.Depleted) return;
            if (!_corpseTimers.ContainsKey(data.NodeEntityId)) return;

            _corpseTimers.Remove(data.NodeEntityId);
            if (!EcsWorld.Instance.EntityExists(data.NodeEntityId)) return;
            EventBus.Instance.Emit("creature_died", data.NodeEntityId);
            EcsWorld.Instance.DestroyEntity(data.NodeEntityId);
            GD.Print($"[Combat] 尸体ID={data.NodeEntityId} 已采集完毕，立即消失");
        }
    }

    public record DamageEventData(int EntityId, float Amount);
}
