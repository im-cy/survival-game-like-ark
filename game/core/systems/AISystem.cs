using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.World;

namespace SurvivalGame.Core.Systems
{
    /// <summary>
    /// 生物 AI 系统 — 三层活跃度 + HFSM 状态机。
    /// Active(视野内)：每帧全量更新
    /// Passive(已加载但视野外)：低频 tick
    /// Dormant(未加载 Chunk)：仅 Chunk 加载时补偿
    /// </summary>
    public class AISystem : SystemBase
    {
        private const float ActiveRange   = 30f;
        private const float PassiveRange  = 80f;
        private const float PassiveTick   = 1.5f;   // seconds

        private float _passiveAccum = 0f;
        private Vector3 _playerPos;

        public override void Initialize()
        {
            EventBus.Instance.Subscribe("creature_damaged", OnCreatureDamaged);
        }

        // 生物被远程攻击时：被动生物逃跑，攻击型生物忽略距离追击
        private void OnCreatureDamaged(object? payload)
        {
            if (payload is not DamageEventData data) return;
            var ai = EcsWorld.Instance.GetComponent<AIComponent>(data.EntityId);
            if (ai == null || ai.CurrentState == FSMState.Dead) return;

            var taming = EcsWorld.Instance.GetComponent<TamingComponent>(data.EntityId);
            bool isPassive = taming?.Method == TamingMethod.Passive;

            if (isPassive)
            {
                // 被动生物：逃跑 6 秒
                ai.CurrentState = FSMState.Flee;
                ai.StateTimer   = 0f;
            }
            else
            {
                // 攻击型生物：立即仇恨，忽略距离 10 秒
                ai.CurrentState          = FSMState.Hostile;
                ai.AggroIgnoreDistTimer  = 10f;
                ai.StateTimer            = 0f;
            }
        }

        public override void Tick(float delta)
        {
            _playerPos   = GetPlayerPosition();
            _passiveAccum += delta;
            bool doPassive = _passiveAccum >= PassiveTick;

            foreach (var entityId in EcsWorld.Instance.Query<AIComponent, PositionComponent>())
            {
                var ai  = EcsWorld.Instance.GetComponent<AIComponent>(entityId)!;
                var pos = EcsWorld.Instance.GetComponent<PositionComponent>(entityId)!;

                float dist = pos.Position.DistanceTo(_playerPos);
                ai.Tier = dist < ActiveRange  ? AITier.Active
                        : dist < PassiveRange ? AITier.Passive
                        : AITier.Dormant;

                switch (ai.Tier)
                {
                    case AITier.Active:
                        TickFSM(entityId, ai, pos, delta);
                        break;
                    case AITier.Passive when doPassive:
                        TickPassive(entityId, ai, pos, PassiveTick);
                        break;
                }
            }

            if (doPassive) _passiveAccum = 0f;
        }

        // ── FSM (Active) ──────────────────────────────────────────────

        private void TickFSM(int id, AIComponent ai, PositionComponent pos, float delta)
        {
            var taming = EcsWorld.Instance.GetComponent<TamingComponent>(id);
            var stats  = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(id);
            ai.StateTimer += delta;
            if (ai.AggroIgnoreDistTimer > 0f) ai.AggroIgnoreDistTimer -= delta;

            // 死亡实体不再处理任何 AI 逻辑
            if (ai.CurrentState == FSMState.Dead)
            {
                pos.Velocity = Vector3.Zero;
                return;
            }

            switch (ai.CurrentState)
            {
                case FSMState.Wander:
                    Wander(id, ai, pos, delta);
                    if (IsPlayerNear(pos, ai.DetectionRange))
                    {
                        if (taming?.State == TamingState.Tamed)
                        {
                            // 已驯服 → 执行指令
                            ai.CurrentState = stats?.CurrentOrder == AIBehaviorOrder.Harvest
                                ? FSMState.Harvest : FSMState.Follow;
                        }
                        else
                        {
                            ai.CurrentState = FSMState.Alert;
                        }
                        ai.StateTimer = 0f;
                    }
                    break;

                case FSMState.Alert:
                    // 玩家已经离开了感知范围 → 取消警觉
                    if (!IsPlayerNear(pos, ai.DetectionRange * 1.5f))
                    {
                        ai.CurrentState = FSMState.Wander;
                        ai.StateTimer = 0f;
                        break;
                    }
                    // 警觉 2 秒后：被动型生物进入戒备，攻击型生物追击
                    if (ai.StateTimer > 2f)
                    {
                        bool isPassive = taming?.Method == TamingMethod.Passive;
                        if (isPassive)
                        {
                            // 被动生物：进入戒备，可喂食驯养
                            if (taming != null && taming.State == TamingState.Wild)
                                taming.State = TamingState.Cautious;
                            ai.CurrentState = FSMState.Cautious;
                        }
                        else
                        {
                            ai.CurrentState = FSMState.Hostile;
                        }
                        ai.StateTimer = 0f;
                    }
                    break;

                case FSMState.Hostile:
                    // 超出追击系留范围（DetectionRange × 3）且无远程仇恨 → 放弃追击，回归游荡
                    if (pos.Position.DistanceTo(_playerPos) > ai.DetectionRange * 3f
                        && ai.AggroIgnoreDistTimer <= 0f)
                    {
                        ai.CurrentState   = FSMState.Wander;
                        ai.StateTimer     = 0f;
                        ai.TargetEntityId = -1;
                        break;
                    }
                    ChaseAndAttack(id, ai, pos, delta);
                    break;

                case FSMState.Cautious:
                    // 等待玩家喂食，保持距离 3m
                    MaintainDistance(pos, _playerPos, 3f, delta);
                    // 喂食后进入 Bonding，同步 FSM → 留在 Cautious 等待下次喂食
                    if (taming?.State == TamingState.Bonding)
                        ai.CurrentState = FSMState.Cautious; // 保持，让玩家继续喂
                    break;

                case FSMState.Follow:
                    // 驯服后根据当前指令执行
                    if (stats?.CurrentOrder == AIBehaviorOrder.Harvest)
                    {
                        ai.CurrentState = FSMState.Harvest;
                        ai.StateTimer = 0f;
                    }
                    else
                    {
                        FollowPlayer(id, pos, delta);
                    }
                    break;

                case FSMState.Guard:
                    GuardPosition(id, ai, pos, delta);
                    break;

                case FSMState.Harvest:
                    // 如果被取消了采集指令，回到跟随
                    if (stats?.CurrentOrder != AIBehaviorOrder.Harvest)
                    {
                        ai.CurrentState = FSMState.Follow;
                        ai.StateTimer = 0f;
                        break;
                    }
                    HarvestNearby(id, ai, pos, stats, delta);
                    break;

                case FSMState.Flee:
                    // 远离玩家 6 秒后恢复游荡
                    FleeFromPlayer(pos, delta);
                    if (ai.StateTimer > 6f)
                    {
                        ai.CurrentState = FSMState.Wander;
                        ai.StateTimer   = 0f;
                    }
                    break;
            }
        }

        private void TickPassive(int id, AIComponent ai, PositionComponent pos, float dt)
        {
            // 被动 tick：只更新饥饿/忠诚等慢速状态，不做寻路
            var stats = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(id);
            if (stats != null)
                stats.Loyalty = Mathf.Max(0f, stats.Loyalty - 0.1f * dt);
        }

        // ── 具体行为 ─────────────────────────────────────────────────────

        private void Wander(int id, AIComponent ai, PositionComponent pos, float delta)
        {
            if (ai.StateTimer > 3f || pos.Position.DistanceTo(ai.Destination) < 0.5f)
            {
                // 随机选下一个目标点
                var rng = new RandomNumberGenerator();
                ai.Destination = pos.Position + new Vector3(
                    rng.RandfRange(-8f, 8f), 0f, rng.RandfRange(-8f, 8f));
                ai.StateTimer = 0f;
            }
            MoveToward(pos, ai.Destination, 2f, delta);
        }

        private void ChaseAndAttack(int id, AIComponent ai, PositionComponent pos, float delta)
        {
            float speed = ai.MoveSpeed;
            ai.AttackTimer -= delta;
            MoveToward(pos, _playerPos, speed, delta);
            if (pos.Position.DistanceTo(_playerPos) <= ai.AttackRange && ai.AttackTimer <= 0f)
            {
                EventBus.Instance.Emit("creature_attack", new AttackEventData(id, -1));
                ai.AttackTimer = ai.AttackCooldown;
            }
        }

        private void FollowPlayer(int id, PositionComponent pos, float delta)
        {
            if (pos.Position.DistanceTo(_playerPos) > 4f)
                MoveToward(pos, _playerPos, 4f, delta);
        }

        private void GuardPosition(int id, AIComponent ai, PositionComponent pos, float delta)
        {
            MoveToward(pos, ai.Destination, 3f, delta);
        }

        private void HarvestNearby(int id, AIComponent ai, PositionComponent pos, CreatureStatsComponent stats, float delta)
        {
            const float HarvestRange = 1.5f;
            const float HarvestCooldown = 2.5f;

            // 寻找最近的符合资源类型的可采集节点
            if (ai.TargetEntityId < 0 || !HasValidTarget(ai.TargetEntityId, stats.HarvestResourceType))
            {
                ai.TargetEntityId = FindNearestHarvestable(pos.Position, stats.HarvestResourceType);
                if (ai.TargetEntityId < 0)
                {
                    // 没有可采集目标 → 回到跟随玩家
                    MoveToward(pos, _playerPos, 3f, delta);
                    return;
                }
            }

            var targetPos = EcsWorld.Instance.GetComponent<PositionComponent>(ai.TargetEntityId);
            if (targetPos == null) { ai.TargetEntityId = -1; return; }

            float dist = pos.Position.DistanceTo(targetPos.Position);
            if (dist > HarvestRange)
            {
                // 移动到资源节点旁边
                MoveToward(pos, targetPos.Position, 3f, delta);
            }
            else
            {
                // 到达 → 等待冷却后采集
                pos.Velocity = Vector3.Zero;
                if (ai.StateTimer >= HarvestCooldown)
                {
                    int ownerId = stats.OwnerId;
                    var ownerInv = ownerId >= 0
                        ? EcsWorld.Instance.GetComponent<InventoryComponent>(ownerId)
                        : null;
                    GameManager.Instance?.Harvest?.TryHarvestAt(ai.TargetEntityId, ownerInv);
                    ai.StateTimer = 0f;
                    GD.Print($"[AI] 生物ID={id} 正在采集资源ID={ai.TargetEntityId}");
                }
            }
        }

        private static bool HasValidTarget(int targetId, string? resourceType)
        {
            var h = EcsWorld.Instance.GetComponent<HarvestableComponent>(targetId);
            if (h == null || h.Depleted) return false;
            if (!string.IsNullOrEmpty(resourceType) && h.ResourceId != resourceType) return false;
            return true;
        }

        private static int FindNearestHarvestable(Vector3 from, string? resourceType)
        {
            int bestId = -1;
            float bestDist = float.MaxValue;
            foreach (var id in EcsWorld.Instance.Query<HarvestableComponent, PositionComponent>())
            {
                var h = EcsWorld.Instance.GetComponent<HarvestableComponent>(id)!;
                if (h.Depleted) continue;
                if (!string.IsNullOrEmpty(resourceType) && h.ResourceId != resourceType) continue;
                var p = EcsWorld.Instance.GetComponent<PositionComponent>(id)!;
                float d = from.DistanceTo(p.Position);
                if (d < bestDist) { bestDist = d; bestId = id; }
            }
            return bestId;
        }

        private static void MoveToward(PositionComponent pos, Vector3 target, float speed, float delta)
        {
            var dir = (target - pos.Position).Normalized();
            pos.Velocity = dir * speed;
            var newPos = pos.Position + pos.Velocity * delta;
            newPos = ResolveBuildingCollision(newPos);
            pos.Position = newPos;
            if (dir.LengthSquared() > 0.01f)
                pos.FacingAngle = Mathf.Atan2(dir.X, dir.Z);
        }

        /// <summary>
        /// 检查新位置是否落入已放置建筑的 AABB，若是则沿最短轴推出。
        /// 建筑占地尺寸与 BuildingPiece.HouseHalfSize（2 m）一致。
        /// </summary>
        private static Vector3 ResolveBuildingCollision(Vector3 newPos)
        {
            const float HouseHalf   = 2.0f;
            const float CreatureRad = 0.4f;          // 生物碰撞半径
            const float Half        = HouseHalf + CreatureRad;

            foreach (var bid in EcsWorld.Instance.Query<BuildingPieceComponent, PositionComponent>())
            {
                var bp = EcsWorld.Instance.GetComponent<BuildingPieceComponent>(bid)!;
                if (bp.PieceType != BuildingPieceType.House) continue;

                var bpos = EcsWorld.Instance.GetComponent<PositionComponent>(bid)!;
                float dx = newPos.X - bpos.Position.X;
                float dz = newPos.Z - bpos.Position.Z;

                if (Mathf.Abs(dx) >= Half || Mathf.Abs(dz) >= Half) continue;

                // 落在建筑 AABB 内 → 沿重叠最小轴推出
                float overlapX = Half - Mathf.Abs(dx);
                float overlapZ = Half - Mathf.Abs(dz);
                if (overlapX < overlapZ)
                    newPos.X += dx >= 0f ? overlapX : -overlapX;
                else
                    newPos.Z += dz >= 0f ? overlapZ : -overlapZ;
            }
            return newPos;
        }

        private void FleeFromPlayer(PositionComponent pos, float delta)
        {
            var away = (pos.Position - _playerPos).Normalized();
            if (away.LengthSquared() < 0.01f) away = Vector3.Right;
            var newPos = pos.Position + away * 5f * delta;
            newPos = ResolveBuildingCollision(newPos);
            pos.Position = newPos;
            pos.Velocity = away * 5f;
            pos.FacingAngle = Mathf.Atan2(away.X, away.Z);
        }

        private static void MaintainDistance(PositionComponent pos, Vector3 target, float dist, float delta)
        {
            float current = pos.Position.DistanceTo(target);
            if (current < dist)
            {
                var away = (pos.Position - target).Normalized();
                pos.Position += away * 2f * delta;
                pos.Velocity = away * 2f;
            }
            else
            {
                pos.Velocity = Vector3.Zero;
            }
        }

        private bool IsPlayerNear(PositionComponent pos, float range)
            => pos.Position.DistanceTo(_playerPos) < range;

        private static Vector3 GetPlayerPosition()
            => WorldManager.Instance?.PlayerPosition ?? Vector3.Zero;
    }

    public record AttackEventData(int AttackerId, int TargetId);
}
