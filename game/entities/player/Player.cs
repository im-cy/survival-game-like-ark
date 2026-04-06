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
            EntityId = World.Instance.CreateEntity();

            _survival = new SurvivalComponent();
            _position = new PositionComponent { Position = GlobalPosition };

            World.Instance.AddComponent(EntityId, _survival);
            World.Instance.AddComponent(EntityId, _position);
            World.Instance.AddComponent(EntityId, new HealthComponent { MaxHp = 100f, CurrentHp = 100f });
            World.Instance.AddComponent(EntityId, new InventoryComponent());
            World.Instance.AddComponent(EntityId, new PlayerStatsComponent());

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
            // TODO: 射线检测最近的可交互实体（生物/资源/建造件）
            EventBus.Instance.Emit("player_interact", EntityId);
        }
    }
}
