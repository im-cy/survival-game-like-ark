using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;

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

        public override void _Ready()
        {
            Instance = this;

            // 注册硬编码数据（物品、建造件）
            DataSetup.Register();

            // 注册系统（顺序即执行顺序）
            Harvest  = Register(new HarvestSystem());
            Campfire = Register(new CampfireSystem());
            Survival = Register(new SurvivalSystem());
            Taming   = Register(new TamingSystem());
            AI       = Register(new AISystem());
            DayNight = Register(new DayNightSystem());

            foreach (var system in _systems)
                system.Initialize();

            GD.Print("[GameManager] 所有系统已初始化");
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
            Instance = null;
        }
    }
}
