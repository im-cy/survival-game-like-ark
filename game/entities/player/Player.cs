using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Systems;
using SurvivalGame.World;

namespace SurvivalGame.Entities.Player
{
    /// <summary>
    /// 玩家控制器 — CharacterBody3D。
    /// 输入 → 移动 → 同步 ECS PositionComponent。
    /// </summary>
    public partial class Player : CharacterBody3D
    {
        [Export] public float MoveSpeed   = 5f;
        [Export] public float SprintSpeed = 9f;

        public int EntityId { get; private set; } = -1;

        private SurvivalComponent? _survival;
        private PositionComponent? _position;

        public override void _Ready()
        {
            // 在 ECS World 中创建玩家实体
            EntityId = EcsWorld.Instance.CreateEntity();

            _survival = new SurvivalComponent();
            _position = new PositionComponent { Position = GlobalPosition };

            EcsWorld.Instance.AddComponent(EntityId, _survival);
            EcsWorld.Instance.AddComponent(EntityId, _position);
            EcsWorld.Instance.AddComponent(EntityId, new HealthComponent { MaxHp = 100f, CurrentHp = 100f });
            EcsWorld.Instance.AddComponent(EntityId, new InventoryComponent());
            EcsWorld.Instance.AddComponent(EntityId, new PlayerStatsComponent());

            GD.Print($"[Player] EntityId={EntityId}");
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            Vector3 input = GetInputDirection();
            bool sprint = Input.IsActionPressed("sprint") && _survival?.Stamina > 0f;

            float speed = sprint ? SprintSpeed : MoveSpeed;
            if (_survival != null) _survival.IsSprinting = sprint && input.LengthSquared() > 0.01f;

            Velocity = input * speed;
            MoveAndSlide();

            // 同步位置到 ECS
            if (_position != null)
            {
                _position.Position = GlobalPosition;
                _position.Velocity = Velocity;
                if (input.LengthSquared() > 0.01f)
                    _position.FacingAngle = Mathf.Atan2(input.X, input.Z);
            }

            // 更新 WorldManager 玩家坐标（供 AI/Chunk 系统使用）
            if (WorldManager.Instance != null)
                WorldManager.Instance.PlayerPosition = GlobalPosition;
        }

        public override void _Input(InputEvent @event)
        {
            if (@event.IsActionPressed("interact"))
                TryInteract();
            if (@event.IsActionPressed("inventory"))
                EventBus.Instance.Emit("toggle_inventory");
        }

        private Vector3 GetInputDirection()
        {
            Vector3 dir = Vector3.Zero;
            if (Input.IsActionPressed("move_forward")) dir.Z -= 1f;
            if (Input.IsActionPressed("move_back"))    dir.Z += 1f;
            if (Input.IsActionPressed("move_left"))    dir.X -= 1f;
            if (Input.IsActionPressed("move_right"))   dir.X += 1f;
            return dir.LengthSquared() > 0f ? dir.Normalized() : Vector3.Zero;
        }

        private void TryInteract()
        {
            var result = GameManager.Instance?.Harvest?.TryHarvest(EntityId);
            if (result == HarvestResult.NoTarget)
                GD.Print("[Player] 附近没有可采集的资源（距离 > 2.5m）");
        }
    }
}
