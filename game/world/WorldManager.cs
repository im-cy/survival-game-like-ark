using Godot;

namespace SurvivalGame.World
{
    /// <summary>
    /// 世界总管理器 — 单例，持有玩家位置、全局世界状态。
    /// 供各系统查询群系温度、玩家坐标等。
    /// </summary>
    public partial class WorldManager : Node
    {
        public static WorldManager? Instance { get; private set; }

        public Vector3 PlayerPosition { get; set; } = Vector3.Zero;
        public int WorldSeed { get; private set; } = 12345;

        private BiomeMap _biomeMap = null!;

        public override void _Ready()
        {
            Instance = this;
            _biomeMap = new BiomeMap(WorldSeed);
            GD.Print($"[WorldManager] 世界已初始化，Seed={WorldSeed}");
        }

        public void Initialize(int seed = 0)
        {
            WorldSeed = seed == 0 ? (int)GD.Randi() : seed;
            _biomeMap = new BiomeMap(WorldSeed);
        }

        public float GetBiomeTemperature(Vector3 worldPos)
            => _biomeMap.GetTemperature(worldPos);

        public BiomeType GetBiomeAt(Vector3 worldPos)
            => _biomeMap.GetBiome(worldPos);

        public override void _ExitTree() => Instance = null;
    }
}
