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
            ai.StateTimer += delta;

            switch (ai.CurrentState)
            {
                case FSMState.Wander:
                    Wander(id, ai, pos, delta);
                    if (IsPlayerNear(pos, ai.DetectionRange))
                        ai.CurrentState = taming?.State == TamingState.Tamed
                            ? FSMState.Follow : FSMState.Alert;
                    break;

                case FSMState.Alert:
                    // 警觉 2 秒后根据驯养状态决定行为
                    if (ai.StateTimer > 2f)
                    {
                        ai.CurrentState = taming?.State == TamingState.Cautious
                            ? FSMState.Cautious : FSMState.Hostile;
                        ai.StateTimer = 0f;
                    }
                    break;

                case FSMState.Hostile:
                    ChaseAndAttack(id, ai, pos, delta);
                    break;

                case FSMState.Cautious:
                    // 等待玩家喂食，保持距离 3m
                    MaintainDistance(pos, _playerPos, 3f, delta);
                    break;

                case FSMState.Follow:
                    FollowPlayer(id, pos, delta);
                    break;

                case FSMState.Guard:
                    GuardPosition(id, ai, pos, delta);
                    break;

                case FSMState.Harvest:
                    HarvestNearby(id, ai, pos, delta);
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
            float speed = 4f;
            MoveToward(pos, _playerPos, speed, delta);
            if (pos.Position.DistanceTo(_playerPos) <= ai.AttackRange)
            {
                // TODO: 触发攻击动画 + 伤害，通过 EventBus 通知
                EventBus.Instance.Emit("creature_attack", new AttackEventData(id, -1));
                ai.StateTimer = 0f;
            }
        }

        private void FollowPlayer(int id, PositionComponent pos, float delta)
        {
            if (pos.Position.DistanceTo(_playerPos) > 4f)
                MoveToward(pos, _playerPos, 4f, delta);
        }

        private void GuardPosition(int id, AIComponent ai, PositionComponent pos, float delta)
        {
            // 守卫目标点，感知范围内有敌人则攻击
            MoveToward(pos, ai.Destination, 3f, delta);
        }

        private void HarvestNearby(int id, AIComponent ai, PositionComponent pos, float delta)
        {
            // TODO: 寻找最近可采集资源节点，移动并触发采集
        }

        private static void MoveToward(PositionComponent pos, Vector3 target, float speed, float delta)
        {
            var dir = (target - pos.Position).Normalized();
            pos.Velocity = dir * speed;
            pos.Position += pos.Velocity * delta;
            // 朝向更新（视图层 CreatureView 会读取这个值）
            if (dir.LengthSquared() > 0.01f)
                pos.FacingAngle = Mathf.Atan2(dir.X, dir.Z);
        }

        private static void MaintainDistance(PositionComponent pos, Vector3 target, float dist, float delta)
        {
            float current = pos.Position.DistanceTo(target);
            if (current < dist)
            {
                var away = (pos.Position - target).Normalized();
                pos.Position += away * 2f * delta;
            }
        }

        private bool IsPlayerNear(PositionComponent pos, float range)
            => pos.Position.DistanceTo(_playerPos) < range;

        private static Vector3 GetPlayerPosition()
        {
            // 通过 EventBus 或 WorldManager 获取玩家位置
            return WorldManager.Instance?.PlayerPosition ?? Vector3.Zero;
        }
    }

    public record AttackEventData(int AttackerId, int TargetId);
}
