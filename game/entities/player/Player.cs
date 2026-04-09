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
            // 有限地图：传送到岛屿中心（CallDeferred 确保 FiniteWorldMap 已初始化）
            CallDeferred(nameof(TeleportToIslandCenter));

            // ECS 注册
            EntityId  = EcsWorld.Instance.CreateEntity();
            _survival = new SurvivalComponent();
            _position = new PositionComponent { Position = GlobalPosition };

            EcsWorld.Instance.AddComponent(EntityId, _survival);
            EcsWorld.Instance.AddComponent(EntityId, _position);
            EcsWorld.Instance.AddComponent(EntityId, new HealthComponent { MaxHp = 100f, CurrentHp = 100f });
            var pstats = new PlayerStatsComponent();
            EcsWorld.Instance.AddComponent(EntityId, pstats);

            // 自动解锁一阶配方
            foreach (var recipe in RecipeRegistry.Instance.All)
                if (recipe.UnlockSource == "tier1")
                    pstats.UnlockedRecipes.Add(recipe.Id);

            // 初始背包（3 浆果 + 2 水袋）
            var inv = new InventoryComponent();
            inv.AddItem("berry",      3, 0.1f);
            inv.AddItem("water_skin", 2, 0.3f);
            EcsWorld.Instance.AddComponent(EntityId, inv);

            // 装备组件（空槽）
            EcsWorld.Instance.AddComponent(EntityId, new EquipmentComponent());

            RegisterInputActions();

            GD.Print($"[Player] EntityId={EntityId}，初始背包：3×浆果  2×水袋，已解锁一阶配方 {pstats.UnlockedRecipes.Count} 条");
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
            if (!InputMap.HasAction("attack"))
            {
                InputMap.AddAction("attack");
                InputMap.ActionAddEvent("attack", new InputEventMouseButton
                    { ButtonIndex = MouseButton.Left });
            }
            if (!InputMap.HasAction("open_craft"))
            {
                InputMap.AddAction("open_craft");
                InputMap.ActionAddEvent("open_craft", new InputEventKey { Keycode = Key.C });
            }
            if (!InputMap.HasAction("evolve_creature"))
            {
                InputMap.AddAction("evolve_creature");
                InputMap.ActionAddEvent("evolve_creature", new InputEventKey { Keycode = Key.V });
            }
            if (!InputMap.HasAction("mount"))
            {
                InputMap.AddAction("mount");
                InputMap.ActionAddEvent("mount", new InputEventKey { Keycode = Key.R });
            }
            // inventory 在 project.godot 里已注册，此处无需重复添加
        }

        private void TeleportToIslandCenter()
        {
            var map = FiniteWorldMap.Instance;
            if (map == null) return;
            float cx = FiniteWorldMap.MapSize * 0.5f;
            float cz = FiniteWorldMap.MapSize * 0.5f;
            float h  = map.GetTerrainHeight(new Vector3(cx, 0f, cz));
            GlobalPosition = new Vector3(cx, h + 0.5f, cz);
            if (_position != null) _position.Position = GlobalPosition;
            if (WorldManager.Instance != null)
                WorldManager.Instance.PlayerPosition = GlobalPosition;
            GD.Print($"[Player] 传送到岛屿中心 ({cx:F0}, {h+0.5f:F1}, {cz:F0})");
        }

        // 是否在水中（Y 低于水面）
        private bool IsInWater => GlobalPosition.Y < FiniteWorldMap.WaterLevel + 0.15f;

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            // 骑乘状态：输入驱动坐骑，玩家跟随
            if (_survival != null && _survival.RidingEntityId >= 0)
            {
                HandleRidingMovement(dt);
                return;
            }

            Vector3 dir = GetInputDirection();

            // 水中禁止冲刺，速度减半
            bool inWater = IsInWater;
            bool sprint  = !inWater && Input.IsActionPressed("sprint") && _survival?.Stamina > 0f;
            float speed  = inWater ? MoveSpeed * 0.55f : (sprint ? SprintSpeed : MoveSpeed);
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

        private void HandleRidingMovement(float dt)
        {
            int mountId = _survival!.RidingEntityId;
            var creaturePos   = EcsWorld.Instance.GetComponent<PositionComponent>(mountId);
            var mountAi       = EcsWorld.Instance.GetComponent<AIComponent>(mountId);

            // 坐骑消失时自动下马
            if (creaturePos == null)
            {
                _survival.RidingEntityId = -1;
                return;
            }

            Vector3 dir   = GetInputDirection();
            float   speed = mountAi?.MoveSpeed ?? 4f;

            if (dir.LengthSquared() > 0.01f)
            {
                creaturePos.Position  += dir * speed * dt;
                creaturePos.FacingAngle = Mathf.Atan2(dir.X, dir.Z);
                creaturePos.Velocity    = dir * speed;
            }
            else
            {
                creaturePos.Velocity = Vector3.Zero;
            }

            // 玩家骑在坐骑上方（Y+0.8 模拟骑乘高度）
            GlobalPosition = creaturePos.Position + new Vector3(0f, 0.8f, 0f);
            Velocity       = Vector3.Zero;

            if (_position != null)
            {
                _position.Position = GlobalPosition;
                _position.Velocity = Vector3.Zero;
            }

            if (WorldManager.Instance != null)
                WorldManager.Instance.PlayerPosition = GlobalPosition;
        }

        public override void _Input(InputEvent @event)
        {
            if (@event.IsActionPressed("interact"))
                TryInteract();

            if (@event.IsActionPressed("inventory"))
            {
                GD.Print("[Player] I键：发送 toggle_inventory");
                EventBus.Instance.Emit("toggle_inventory");
            }

            if (@event.IsActionPressed("consume_food"))
                TryConsumeFood();

            if (@event.IsActionPressed("open_build"))
                EventBus.Instance.Emit("toggle_build_menu");

            if (@event.IsActionPressed("give_order"))
                TryGiveOrder();

            if (@event.IsActionPressed("attack"))
                TryAttack();

            if (@event.IsActionPressed("open_craft"))
                EventBus.Instance.Emit("toggle_craft_menu", null);

            if (@event.IsActionPressed("evolve_creature"))
                TryEvolveNearbyCreature();

            if (@event.IsActionPressed("mount"))
                TryToggleMount();
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

            // 先投影到 Y=0 平面，得到近似 XZ
            float t0 = -from.Y / dir.Y;
            var flatPos = from + dir * t0;

            // 用地形高度做一次修正，消除摄像机倾斜引起的 XZ 偏移
            var map = FiniteWorldMap.Instance;
            if (map != null && map.IsInBounds(flatPos))
            {
                float h = map.GetTerrainHeight(flatPos);
                float t1 = (h - from.Y) / dir.Y;
                return from + dir * t1;
            }

            return flatPos;
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
                // 只尝试喂食未驯服的活体生物
                if (taming.State == TamingState.Tamed) continue;
                var aiState = EcsWorld.Instance.GetComponent<AIComponent>(id);
                if (aiState?.CurrentState == FSMState.Dead) continue;

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

        // ── 攻击（鼠标左键）───────────────────────────────────────────

        private void TryAttack()
        {
            var aimPos = GetMouseWorldPosition() ?? GlobalPosition + Transform.Basis.Z * -2f;
            bool hit = GameManager.Instance?.Combat?.TryPlayerAttack(EntityId, aimPos) ?? false;
            if (!hit)
                GD.Print("[Player] 攻击：范围内没有目标");
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

        // ── 进化（V键）：触发附近可进化的驯服生物进化 ───────────────

        private void TryEvolveNearbyCreature()
        {
            var myPos = EcsWorld.Instance.GetComponent<PositionComponent>(EntityId);
            if (myPos == null) return;

            int nearestId = -1;
            float nearestDist = InteractRange * 1.5f;   // 5.5m 范围

            foreach (var id in EcsWorld.Instance.Query<ExperienceComponent, CreatureStatsComponent, PositionComponent>())
            {
                var exp   = EcsWorld.Instance.GetComponent<ExperienceComponent>(id)!;
                var stats = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(id)!;
                if (!exp.CanEvolve || stats.OwnerId != EntityId) continue;

                var pos  = EcsWorld.Instance.GetComponent<PositionComponent>(id)!;
                float d  = myPos.Position.DistanceTo(pos.Position);
                if (d < nearestDist) { nearestDist = d; nearestId = id; }
            }

            if (nearestId < 0)
            {
                GD.Print("[Player] 附近没有可进化的驯服生物");
                return;
            }

            GameManager.Instance?.Experience?.TryEvolve(nearestId);
        }

        // ── 骑乘（R键）：上马 / 下马 ─────────────────────────────────

        private void TryToggleMount()
        {
            if (_survival == null) return;

            // 当前正在骑乘 → 下马
            if (_survival.RidingEntityId >= 0)
            {
                var mountAi = EcsWorld.Instance.GetComponent<AIComponent>(_survival.RidingEntityId);
                if (mountAi != null)
                {
                    mountAi.CurrentState = FSMState.Follow;
                    mountAi.StateTimer   = 0f;
                }
                GD.Print("[Player] 下马");
                _survival.RidingEntityId = -1;
                return;
            }

            // 寻找附近可骑乘的已驯服生物
            var myPos = EcsWorld.Instance.GetComponent<PositionComponent>(EntityId);
            if (myPos == null) return;

            int nearestId   = -1;
            float nearestDist = InteractRange;

            foreach (var id in EcsWorld.Instance.Query<TamingComponent, CreatureStatsComponent, PositionComponent>())
            {
                var taming = EcsWorld.Instance.GetComponent<TamingComponent>(id)!;
                var stats  = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(id)!;
                if (taming.State != TamingState.Tamed) continue;
                if (stats.OwnerId != EntityId)         continue;
                if (!stats.CanRide)                    continue;

                var pos  = EcsWorld.Instance.GetComponent<PositionComponent>(id)!;
                float d  = myPos.Position.DistanceTo(pos.Position);
                if (d < nearestDist) { nearestDist = d; nearestId = id; }
            }

            if (nearestId < 0)
            {
                GD.Print("[Player] 附近没有可骑乘的驯服生物（需要进化到最终形态）");
                return;
            }

            // 上马
            var ai = EcsWorld.Instance.GetComponent<AIComponent>(nearestId)!;
            ai.CurrentState = FSMState.Mounted;
            ai.StateTimer   = 0f;
            _survival.RidingEntityId = nearestId;

            var name = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(nearestId)?.SpeciesId ?? "?";
            GD.Print($"[Player] 上马：骑乘 {name}");
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
