using Godot;
using System.Collections.Generic;
using System.Linq;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.World
{
    /// <summary>
    /// Chunk 流式加载管理器。
    /// 以玩家为中心维护 ActiveRadius×2+1 的活跃区域，异步加载/卸载边缘块。
    /// </summary>
    public partial class ChunkManager : Node
    {
        public static ChunkManager? Instance { get; private set; }

        [Export] public int ActiveRadius = 3;

        private readonly Dictionary<Vector2I, ChunkData> _loaded = new();
        private ChunkGenerator _generator = null!;
        private Vector2I _lastPlayerChunk = new Vector2I(int.MaxValue, 0);

        public override void _Ready()
        {
            Instance = this;
            var wm = WorldManager.Instance!;
            _generator = new ChunkGenerator(wm.WorldSeed, new BiomeMap(wm.WorldSeed));
        }

        public override void _Process(double delta)
        {
            var playerChunk = WorldToChunk(WorldManager.Instance!.PlayerPosition);
            if (playerChunk == _lastPlayerChunk) return;
            _lastPlayerChunk = playerChunk;
            UpdateActiveChunks(playerChunk);
        }

        private void UpdateActiveChunks(Vector2I center)
        {
            var needed = new HashSet<Vector2I>();
            for (int x = -ActiveRadius; x <= ActiveRadius; x++)
            for (int z = -ActiveRadius; z <= ActiveRadius; z++)
                needed.Add(center + new Vector2I(x, z));

            // 加载新块
            foreach (var coord in needed.Where(c => !_loaded.ContainsKey(c)))
                LoadChunk(coord);

            // 卸载超出范围的块
            foreach (var coord in _loaded.Keys.Where(c => !needed.Contains(c)).ToArray())
                UnloadChunk(coord);
        }

        private void LoadChunk(Vector2I coord)
        {
            var chunk = _generator.Generate(coord);
            _loaded[coord] = chunk;
            EventBus.Instance.Emit("chunk_loaded", coord);
        }

        private void UnloadChunk(Vector2I coord)
        {
            _loaded.Remove(coord);
            EventBus.Instance.Emit("chunk_unloaded", coord);
        }

        public ChunkData? GetChunk(Vector2I coord)
            => _loaded.TryGetValue(coord, out var c) ? c : null;

        public static Vector2I WorldToChunk(Vector3 worldPos)
            => new Vector2I(
                Mathf.FloorToInt(worldPos.X / ChunkData.Size),
                Mathf.FloorToInt(worldPos.Z / ChunkData.Size));

        public override void _ExitTree() => Instance = null;
    }
}
