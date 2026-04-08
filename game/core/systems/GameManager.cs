using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;
using SurvivalGame.Entities.Creatures;

namespace SurvivalGame.Core.Systems
{
    /// <summary>
    /// 游戏总管理器 — 单例 Node，挂在 Main 场景根节点上。
    /// 负责注册并按顺序 Tick 所有系统。
    /// </summary>
    public partial class GameManager : Node
    {
        public static GameManager? Instance { get; private set; }

        private readonly List<SystemBase> _systems = new();

        // 对外暴露各系统引用，方便其他地方直接访问
        public SurvivalSystem  Survival  { get; private set; } = null!;
        public TamingSystem    Taming    { get; private set; } = null!;
        public AISystem        AI        { get; private set; } = null!;
        public DayNightSystem  DayNight  { get; private set; } = null!;
        public HarvestSystem   Harvest   { get; private set; } = null!;
        public CampfireSystem  Campfire  { get; private set; } = null!;
        public BuildingSystem  Building  { get; private set; } = null!;
        public CombatSystem    Combat    { get; private set; } = null!;
        public CraftingSystem    Crafting    { get; private set; } = null!;
        public ProjectileSystem  Projectile  { get; private set; } = null!;

        public override void _Ready()
        {
            Instance = this;

            // 每次进入游戏场景时重置 ECS 世界，防止编辑器多次运行残留旧实体
            EcsWorld.Reset();

            // 注册硬编码数据（物品、建造件、生物）
            DataSetup.Register();

            // 注册系统（顺序即执行顺序）
            Harvest  = Register(new HarvestSystem());
            Campfire = Register(new CampfireSystem());
            Building = Register(new BuildingSystem());
            Survival = Register(new SurvivalSystem());
            Taming   = Register(new TamingSystem());
            AI       = Register(new AISystem());
            DayNight = Register(new DayNightSystem());
            Combat     = Register(new CombatSystem());
            Crafting   = Register(new CraftingSystem());
            Projectile = Register(new ProjectileSystem());

            foreach (var system in _systems)
                system.Initialize();

            // 订阅 Boss 击败事件 → 解锁二阶配方
            EventBus.Instance.Subscribe("boss_defeated", OnBossDefeated);

            GD.Print("[GameManager] 所有系统已初始化");

            // 场景树就绪后再生成生物（避免 CreatureSpawner 未就绪）
            CallDeferred(nameof(SpawnInitialCreatures));
        }

        private void SpawnInitialCreatures()
        {
            var spawner = CreatureSpawner.Instance;
            if (spawner == null)
            {
                GD.PrintErr("[GameManager] CreatureSpawner 未就绪，跳过生物生成");
                return;
            }

            // 在玩家出生点周围生成 2 只野猪
            spawner.SpawnCreature("boar", new Vector3(8f,  0f,  6f));
            spawner.SpawnCreature("boar", new Vector3(-7f, 0f,  8f));

            // 在稍远处生成 Boss（森林守卫者）
            spawner.SpawnCreature("forest_guardian", new Vector3(25f, 0f, 20f));
        }

        // ── Boss 击败 → 解锁二阶配方 ─────────────────────────────────

        private void OnBossDefeated(object? payload)
        {
            if (payload is not string bossId) return;

            string unlockKey = $"boss:{bossId}";
            int count = 0;

            foreach (var playerId in EcsWorld.Instance.Query<PlayerStatsComponent>())
            {
                var ps = EcsWorld.Instance.GetComponent<PlayerStatsComponent>(playerId)!;
                foreach (var recipe in RecipeRegistry.Instance.All)
                {
                    if (recipe.UnlockSource == unlockKey && ps.UnlockedRecipes.Add(recipe.Id))
                        count++;
                }
            }

            GD.Print($"[GameManager] Boss [{bossId}] 已击败，解锁 {count} 条新配方");
            EventBus.Instance.Emit("recipes_unlocked", null);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            foreach (var system in _systems)
                if (system.Enabled) system.Tick(dt);
        }

        private T Register<T>(T system) where T : SystemBase
        {
            _systems.Add(system);
            return system;
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("boss_defeated", OnBossDefeated);
            Instance = null;
        }
    }
}
