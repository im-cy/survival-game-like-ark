using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;
using SurvivalGame.Core.Systems;
using SurvivalGame.World;
using SurvivalGame.Entities.Building;

namespace SurvivalGame.Entities.Player
{
    /// <summary>
    /// 玩家控制器 — CharacterBody3D。
    /// 输入 → 移动 → 同步 ECS PositionComponent。
    /// 按键：E=采集/喂食  F=吃/喝  B=建造菜单  H=指令（跟随↔采集）
    /// </summary>
    public partial class Player : CharacterBody3D
    {
        [Export] public float MoveSpeed   = 5f;
        [Export] public float SprintSpeed = 9f;

        public int EntityId { get; private set; } = -1;

        private SurvivalComponent? _survival;
        private PositionComponent? _position;

        private const float InteractRange = 3f;   // 喂食/交互范围

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
            if (!InputMap.HasAction("give_order"))
            {
                InputMap.AddAction("give_order");
                InputMap.ActionAddEvent("give_order", new InputEventKey { Keycode = Key.H });
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

            if (@event.IsActionPressed("give_order"))
                TryGiveOrder();
        }

        // ── 鼠标世界坐标（投影到 Y=0 平面）────────────────────────────

        private Vector3? GetMouseWorldPosition()
        {
            var camera = GetViewport().GetCamera3D();
            if (camera == null) return null;
            var mousePos = GetViewport().GetMousePosition();
            var from = camera.ProjectRayOrigin(mousePos);
            var dir  = camera.ProjectRayNormal(mousePos);
            if (Mathf.Abs(dir.Y) < 0.001f) return null;
            float t = -from.Y / dir.Y;
            return from + dir * t;
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

        // ── 交互（E键）：优先喂食附近生物，其次采集资源 ──────────────

        private void TryInteract()
        {
            // 优先：建筑门交互
            if (TryInteractWithBuilding()) return;

            // 其次：喂食附近可驯养生物
            if (TryFeedNearbyCreature()) return;

            // 最后：采集鼠标指向的资源
            var mouseWorld = GetMouseWorldPosition();
            var result = GameManager.Instance?.Harvest?.TryHarvest(EntityId, mouseWorld);
            if (result == HarvestResult.NoTarget)
                GD.Print("[Player] 鼠标附近没有可采集的资源");
        }

        private bool TryInteractWithBuilding()
        {
            foreach (var node in GetTree().GetNodesInGroup("building_pieces"))
            {
                if (node is BuildingPiece bp && bp.TryInteractDoor(GlobalPosition))
                    return true;
            }
            return false;
        }

        private bool TryFeedNearbyCreature()
        {
            var myPos = EcsWorld.Instance.GetComponent<PositionComponent>(EntityId);
            if (myPos == null) return false;

            int nearestId = -1;
            float nearestDist = InteractRange;

            foreach (var id in EcsWorld.Instance.Query<TamingComponent, PositionComponent>())
            {
                var taming = EcsWorld.Instance.GetComponent<TamingComponent>(id)!;
                // 只尝试喂食未驯服的生物
                if (taming.State == TamingState.Tamed) continue;

                var pos = EcsWorld.Instance.GetComponent<PositionComponent>(id)!;
                float dist = myPos.Position.DistanceTo(pos.Position);
                if (dist < nearestDist) { nearestDist = dist; nearestId = id; }
            }

            if (nearestId < 0) return false;

            var result = GameManager.Instance?.Taming?.TryFeedCreature(EntityId, nearestId);
            switch (result)
            {
                case TamingFeedResult.Success:
                    var t = EcsWorld.Instance.GetComponent<TamingComponent>(nearestId)!;
                    GD.Print($"[Player] 喂食成功，驯养进度 {t.TrustProgress:F0}%");
                    return true;
                case TamingFeedResult.TamingComplete:
                    GD.Print("[Player] 驯养完成！生物现在跟随你。");
                    return true;
                case TamingFeedResult.NoFood:
                    GD.Print("[Player] 背包中没有合适的食物，改为采集资源");
                    return false;  // 没食物 → 让采集逻辑接管
                case TamingFeedResult.AlreadyTamed:
                    return false;  // 已驯服 → 交给采集逻辑
                default:
                    return false;
            }
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

        // ── 指令（H键）：切换已驯服生物的跟随/采集指令 ──────────────

        private void TryGiveOrder()
        {
            // 找最近的已驯服生物（归属于本玩家）
            var myPos = EcsWorld.Instance.GetComponent<PositionComponent>(EntityId);
            if (myPos == null) return;

            int nearestId = -1;
            float nearestDist = 20f;   // 指令范围更大

            foreach (var id in EcsWorld.Instance.Query<TamingComponent, CreatureStatsComponent, PositionComponent>())
            {
                var taming = EcsWorld.Instance.GetComponent<TamingComponent>(id)!;
                var stats  = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(id)!;
                if (taming.State != TamingState.Tamed) continue;
                if (stats.OwnerId != EntityId) continue;

                var pos = EcsWorld.Instance.GetComponent<PositionComponent>(id)!;
                float dist = myPos.Position.DistanceTo(pos.Position);
                if (dist < nearestDist) { nearestDist = dist; nearestId = id; }
            }

            if (nearestId < 0)
            {
                GD.Print("[Player] 附近没有已驯服的生物");
                return;
            }

            var creatureStats = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(nearestId)!;
            var ai = EcsWorld.Instance.GetComponent<AIComponent>(nearestId)!;

            if (creatureStats.CurrentOrder == AIBehaviorOrder.Follow)
            {
                if (string.IsNullOrEmpty(creatureStats.HarvestResourceType))
                {
                    GD.Print($"[Player] 该生物不具备采集能力");
                    return;
                }
                creatureStats.CurrentOrder = AIBehaviorOrder.Harvest;
                ai.CurrentState = FSMState.Harvest;
                ai.StateTimer = 0f;
                ai.TargetEntityId = -1;
                GD.Print("[Player] 指令：自动采集");
            }
            else
            {
                creatureStats.CurrentOrder = AIBehaviorOrder.Follow;
                ai.CurrentState = FSMState.Follow;
                ai.StateTimer = 0f;
                GD.Print("[Player] 指令：跟随");
            }
        }
    }
}
