using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;
using SurvivalGame.Core.Systems;
using SurvivalGame.World;

namespace SurvivalGame.Entities.Player
{
    /// <summary>
    /// 玩家控制器 — CharacterBody3D。
    /// 输入 → 移动 → 同步 ECS PositionComponent。
    /// 按键：E=采集/交互  F=吃/喝  B=建造菜单
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
            // ECS 注册
            EntityId  = EcsWorld.Instance.CreateEntity();
            _survival = new SurvivalComponent();
            _position = new PositionComponent { Position = GlobalPosition };

            EcsWorld.Instance.AddComponent(EntityId, _survival);
            EcsWorld.Instance.AddComponent(EntityId, _position);
            EcsWorld.Instance.AddComponent(EntityId, new HealthComponent { MaxHp = 100f, CurrentHp = 100f });
            EcsWorld.Instance.AddComponent(EntityId, new PlayerStatsComponent());

            // 初始背包（3 浆果 + 2 水袋）
            var inv = new InventoryComponent();
            inv.AddItem("berry",      3, 0.1f);
            inv.AddItem("water_skin", 2, 0.3f);
            EcsWorld.Instance.AddComponent(EntityId, inv);

            // 注册自定义输入动作
            RegisterInputActions();

            GD.Print($"[Player] EntityId={EntityId}，初始背包：3×浆果  2×水袋");
        }

        private static void RegisterInputActions()
        {
            if (!InputMap.HasAction("consume_food"))
            {
                InputMap.AddAction("consume_food");
                InputMap.ActionAddEvent("consume_food", new InputEventKey { Keycode = Key.F });
            }
            if (!InputMap.HasAction("open_build"))
            {
                InputMap.AddAction("open_build");
                InputMap.ActionAddEvent("open_build", new InputEventKey { Keycode = Key.B });
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt    = (float)delta;
            Vector3 dir = GetInputDirection();
            bool sprint = Input.IsActionPressed("sprint") && _survival?.Stamina > 0f;

            float speed = sprint ? SprintSpeed : MoveSpeed;
            if (_survival != null) _survival.IsSprinting = sprint && dir.LengthSquared() > 0.01f;

            Velocity = dir * speed;
            MoveAndSlide();

            if (_position != null)
            {
                _position.Position = GlobalPosition;
                _position.Velocity = Velocity;
                if (dir.LengthSquared() > 0.01f)
                    _position.FacingAngle = Mathf.Atan2(dir.X, dir.Z);
            }

            if (WorldManager.Instance != null)
                WorldManager.Instance.PlayerPosition = GlobalPosition;
        }

        public override void _Input(InputEvent @event)
        {
            if (@event.IsActionPressed("interact"))
                TryInteract();

            if (@event.IsActionPressed("inventory"))
                EventBus.Instance.Emit("toggle_inventory");

            if (@event.IsActionPressed("consume_food"))
                TryConsumeFood();

            if (@event.IsActionPressed("open_build"))
                EventBus.Instance.Emit("toggle_build_menu");
        }

        // ── 输入方向 ──────────────────────────────────────────────────

        private Vector3 GetInputDirection()
        {
            Vector3 dir = Vector3.Zero;
            if (Input.IsActionPressed("move_forward")) dir.Z -= 1f;
            if (Input.IsActionPressed("move_back"))    dir.Z += 1f;
            if (Input.IsActionPressed("move_left"))    dir.X -= 1f;
            if (Input.IsActionPressed("move_right"))   dir.X += 1f;
            return dir.LengthSquared() > 0f ? dir.Normalized() : Vector3.Zero;
        }

        // ── 交互（E键）采集资源 ────────────────────────────────────────

        private void TryInteract()
        {
            var result = GameManager.Instance?.Harvest?.TryHarvest(EntityId);
            if (result == HarvestResult.NoTarget)
                GD.Print("[Player] 附近没有可采集的资源（距离 > 2.5m）");
        }

        // ── 消耗食物（F键）────────────────────────────────────────────

        private void TryConsumeFood()
        {
            var inv = EcsWorld.Instance.GetComponent<InventoryComponent>(EntityId);
            if (inv == null) return;

            ItemInstance? target = null;
            foreach (var item in inv.Items)
            {
                var def = ItemRegistry.Instance.Get(item.ItemId);
                if (def != null && (def.HungerRestore > 0f || def.ThirstRestore > 0f))
                { target = item; break; }
            }

            if (target == null)
            {
                GD.Print("[Player] 背包中没有可食用/饮用的物品");
                return;
            }

            var itemDef = ItemRegistry.Instance.Get(target.ItemId)!;
            GameManager.Instance?.Survival?.EatFood(EntityId, itemDef.HungerRestore, itemDef.ThirstRestore);
            inv.RemoveItem(target.ItemId, 1);
            GD.Print($"[Player] 消耗 {itemDef.DisplayName}，饥饿+{itemDef.HungerRestore} 口渴+{itemDef.ThirstRestore}");
        }
    }
}
