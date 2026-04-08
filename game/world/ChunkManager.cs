using Godot;
using System.Collections.Generic;
using System.Linq;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.World
{
    /// <summary>
    /// Chunk 流式加载管理器。
    /// 以玩家为中心维护 ActiveRadius×2+1 的活跃区域，
    /// 动态创建/销毁 ChunkView 节点。
    /// </summary>
    public partial class ChunkManager : Node
    {
        public static ChunkManager? Instance { get; private set; }

        [Export] public int ActiveRadius = 3;

        private readonly Dictionary<Vector2I, ChunkData> _loaded = new();
        private readonly Dictionary<Vector2I, ChunkView> _views  = new();

        private ChunkGenerator _generator = null!;
        private Vector2I _lastPlayerChunk  = new Vector2I(int.MaxValue, 0);

        public override void _Ready()
        {
            Instance   = this;
            var wm     = WorldManager.Instance!;
            _generator = new ChunkGenerator(wm.WorldSeed, new BiomeMap(wm.WorldSeed));

            // 同步预加载出生点周围的块，确保玩家落地有地面碰撞
            UpdateActiveChunks(Vector2I.Zero);
            _lastPlayerChunk = Vector2I.Zero;
        }

        public override void _Process(double delta)
        {
            var playerChunk = WorldToChunk(WorldManager.Instance!.PlayerPosition);
            if (playerChunk == _lastPlayerChunk) return;
            _lastPlayerChunk = playerChunk;
            UpdateActiveChunks(playerChunk);
        }

        // ── Chunk 更新 ────────────────────────────────────────────────

        private void UpdateActiveChunks(Vector2I center)
        {
            var needed = new HashSet<Vector2I>();
            for (int x = -ActiveRadius; x <= ActiveRadius; x++)
            for (int z = -ActiveRadius; z <= ActiveRadius; z++)
                needed.Add(center + new Vector2I(x, z));

            foreach (var coord in needed.Where(c => !_loaded.ContainsKey(c)))
                LoadChunk(coord);

            foreach (var coord in _loaded.Keys.Where(c => !needed.Contains(c)).ToArray())
                UnloadChunk(coord);
        }

        private void LoadChunk(Vector2I coord)
        {
            var chunk = _generator.Generate(coord);
            _loaded[coord] = chunk;

            var view = new ChunkView();
            AddChild(view);         // 先加入场景树，再 Setup（保证 GlobalPosition 有效）
            view.Setup(chunk);
            _views[coord] = view;

            EventBus.Instance.Emit("chunk_loaded", coord);
        }

        private void UnloadChunk(Vector2I coord)
        {
            if (_views.TryGetValue(coord, out var view))
            {
                view.QueueFree();
                _views.Remove(coord);
            }
            _loaded.Remove(coord);
            EventBus.Instance.Emit("chunk_unloaded", coord);
        }

        // ── 查询 API ──────────────────────────────────────────────────

        public ChunkData? GetChunk(Vector2I coord)
            => _loaded.TryGetValue(coord, out var c) ? c : null;

        public static Vector2I WorldToChunk(Vector3 worldPos)
            => new Vector2I(
                Mathf.FloorToInt(worldPos.X / ChunkData.Size),
                Mathf.FloorToInt(worldPos.Z / ChunkData.Size));

        public override void _ExitTree() => Instance = null;
    }
}
