using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;
using SurvivalGame.Entities.Creatures;
using SurvivalGame.World;

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
        public BreedingSystem    Breeding    { get; private set; } = null!;
        public ExperienceSystem  Experience  { get; private set; } = null!;

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
            Breeding   = Register(new BreedingSystem());
            Experience = Register(new ExperienceSystem());

            foreach (var system in _systems)
                system.Initialize();

            // 订阅 Boss 击败事件 → 解锁二阶配方
            EventBus.Instance.Subscribe("boss_defeated", OnBossDefeated);

            GD.Print("[GameManager] 所有系统已初始化");

            SetupSceneLighting();

            // 禁用无限 Chunk 流式系统，使用有限地图替代
            DisableChunkSystem();

            // 有限地图（包含地形、资源、装饰散布）
            var finiteMap = new FiniteWorldMap();
            AddChild(finiteMap);

            // 场景树就绪后再生成生物（避免 CreatureSpawner 未就绪）
            CallDeferred(nameof(SpawnInitialCreatures));
        }

        // ── 禁用 Chunk 系统 ───────────────────────────────────────────────

        private void DisableChunkSystem()
        {
            // ChunkManager 节点存在则禁用其处理，不删除（避免改 .tscn 文件）
            var chunkManager = GetTree().Root.FindChild("ChunkManager", true, false);
            if (chunkManager is Node cm)
            {
                cm.ProcessMode = ProcessModeEnum.Disabled;
                GD.Print("[GameManager] ChunkManager 已禁用（使用 FiniteWorldMap 替代）");
            }
        }

        // ── 场景光照 ──────────────────────────────────────────────────────

        private void SetupSceneLighting()
        {
            // 主方向光（模拟太阳，带阴影）
            var sun = new DirectionalLight3D();
            sun.Name = "Sun";
            sun.LightColor        = new Color(1.00f, 0.93f, 0.76f);
            sun.LightEnergy       = 1.15f;
            sun.RotationDegrees   = new Vector3(-52f, 35f, 0f);
            sun.ShadowEnabled     = true;
            sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits;
            AddChild(sun);

            // 补光（模拟天空散射，填充阴影面）
            var fill = new DirectionalLight3D();
            fill.Name         = "FillLight";
            fill.LightColor   = new Color(0.60f, 0.72f, 0.90f);
            fill.LightEnergy  = 0.38f;
            fill.RotationDegrees = new Vector3(-30f, -150f, 0f);
            fill.ShadowEnabled = false;
            AddChild(fill);

            // 环境（天空背景色 + 环境光）
            var env = new Environment();
            env.BackgroundMode  = Environment.BGMode.Color;
            env.BackgroundColor = new Color(0.46f, 0.60f, 0.78f);
            env.AmbientLightSource = Environment.AmbientSource.Color;
            env.AmbientLightColor  = new Color(0.32f, 0.40f, 0.50f);
            env.AmbientLightEnergy = 0.45f;
            var worldEnv = new WorldEnvironment { Environment = env };
            AddChild(worldEnv);

            GD.Print("[GameManager] 场景光照已设置（太阳 + 补光 + 环境光）");
        }

        private void SpawnInitialCreatures()
        {
            var spawner = CreatureSpawner.Instance;
            if (spawner == null)
            {
                GD.PrintErr("[GameManager] CreatureSpawner 未就绪，跳过生物生成");
                return;
            }

            // 有限地图：出生点在岛屿中心 (128,0,128) 附近
            float cx = World.FiniteWorldMap.MapSize * 0.5f;
            float cz = World.FiniteWorldMap.MapSize * 0.5f;

            spawner.SpawnCreature("boar", new Vector3(cx + 8f,  0f, cz + 6f));
            spawner.SpawnCreature("boar", new Vector3(cx - 7f,  0f, cz + 8f));
            spawner.SpawnCreature("forest_guardian", new Vector3(cx + 25f, 0f, cz + 20f));
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
