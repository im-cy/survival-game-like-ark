using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Systems;

namespace SurvivalGame.World
{
    /// <summary>
    /// 单个 Chunk 的可视化节点。
    ///
    /// - 根据群系生成彩色地面平面（含 StaticBody3D 碰撞）。
    /// - 程序化生成资源：森林 → 树木（wood），草原 → 浆果丛（berry）。
    /// - 卸载时自动清理所有 ECS 实体。
    /// </summary>
    public partial class ChunkView : Node3D
    {
        private readonly List<int>                    _entityIds     = new();
        // 实体 ID → 该资源对应的所有可视/碰撞节点，耗尽时一并删除
        private readonly Dictionary<int, List<Node3D>> _resourceNodes = new();

        // ── 颜色常量 ──────────────────────────────────────────────────

        private static readonly Color ColForest    = new(0.22f, 0.45f, 0.18f);
        private static readonly Color ColGrassland = new(0.38f, 0.62f, 0.25f);
        private static readonly Color ColTrunk     = new(0.40f, 0.25f, 0.10f);
        private static readonly Color ColLeaf      = new(0.20f, 0.50f, 0.15f);
        private static readonly Color ColBush      = new(0.22f, 0.58f, 0.18f);
        private static readonly Color ColStone     = new(0.55f, 0.55f, 0.55f);
        private static readonly Color ColFiber     = new(0.70f, 0.85f, 0.45f);

        // ── 初始化 ────────────────────────────────────────────────────

        public override void _Ready()
        {
            EventBus.Instance.Subscribe("resource_harvested", OnResourceHarvested);
        }

        private void OnResourceHarvested(object? payload)
        {
            if (payload is not HarvestEventData data) return;
            if (!data.Depleted) return;
            if (!_resourceNodes.TryGetValue(data.NodeEntityId, out var nodes)) return;

            foreach (var n in nodes) n.QueueFree();
            _resourceNodes.Remove(data.NodeEntityId);
        }

        /// <summary>在 AddChild 之后调用。</summary>
        public void Setup(ChunkData data)
        {
            GlobalPosition = data.ChunkOriginWorld;

            var centerBiome = data.GetTile(ChunkData.Size / 2, ChunkData.Size / 2).Biome;
            bool isForest   = centerBiome == BiomeType.Forest;

            BuildTerrain(isForest);
            SpawnResources(data, isForest);
        }

        // ── 地形 ──────────────────────────────────────────────────────

        private void BuildTerrain(bool isForest)
        {
            float half   = ChunkData.Size / 2f;
            var   center = new Vector3(half, 0f, half);

            // 地面网格
            AddChild(new MeshInstance3D
            {
                Mesh             = new PlaneMesh { Size = new Vector2(ChunkData.Size, ChunkData.Size) },
                Position         = center,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = isForest ? ColForest : ColGrassland
                },
            });

            // 地面碰撞（薄箱体，顶面在 Y=0）
            var body = new StaticBody3D { Position = new Vector3(half, -0.05f, half) };
            body.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(ChunkData.Size, 0.1f, ChunkData.Size) }
            });
            AddChild(body);
        }

        // ── 程序化资源 ────────────────────────────────────────────────

        private void SpawnResources(ChunkData data, bool isForest)
        {
            var rng = new RandomNumberGenerator();
            // 用块坐标作为 seed，保证相同块每次生成相同内容
            rng.Seed = (ulong)unchecked(data.Coord.X * 73856093 ^ data.Coord.Y * 19349663);

            int maxSpawn  = isForest ? 6 : 5;
            float chance  = isForest ? 0.45f : 0.35f;
            int spawned   = 0;

            // 将 Chunk 分成 4×4 格子，每格随机尝试生成一次资源
            const int Grid = 4;
            float cell = (float)ChunkData.Size / Grid;

            for (int gx = 0; gx < Grid && spawned < maxSpawn; gx++)
            for (int gz = 0; gz < Grid && spawned < maxSpawn; gz++)
            {
                if (rng.Randf() > chance) continue;

                float lx = (gx + 0.5f) * cell + rng.RandfRange(-cell * 0.3f, cell * 0.3f);
                float lz = (gz + 0.5f) * cell + rng.RandfRange(-cell * 0.3f, cell * 0.3f);
                var localPos = new Vector3(lx, 0f, lz);

                if (isForest) SpawnTree(localPos);
                else          SpawnBush(localPos);

                spawned++;
            }

            // 二次遍历：生成石头（Forest）或纤维（Grassland），密度更低
            int maxExtra = 3;
            float extraChance = 0.30f;
            int extraSpawned  = 0;

            for (int gx = 0; gx < Grid && extraSpawned < maxExtra; gx++)
            for (int gz = 0; gz < Grid && extraSpawned < maxExtra; gz++)
            {
                if (rng.Randf() > extraChance) continue;
                float lx = (gx + 0.5f) * cell + rng.RandfRange(-cell * 0.3f, cell * 0.3f);
                float lz = (gz + 0.5f) * cell + rng.RandfRange(-cell * 0.3f, cell * 0.3f);
                var localPos = new Vector3(lx, 0f, lz);

                if (isForest) SpawnStone(localPos);
                else          SpawnFiber(localPos);

                extraSpawned++;
            }
        }

        // ── 树木 ──────────────────────────────────────────────────────

        private void SpawnTree(Vector3 localPos)
        {
            var trunk = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.3f, Height = 2f },
                Position = localPos + new Vector3(0f, 1f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColTrunk },
            };
            var canopy = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 1.1f, Height = 2.2f },
                Position = localPos + new Vector3(0f, 3.3f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColLeaf },
            };
            var body = new StaticBody3D { Position = localPos + new Vector3(0f, 1f, 0f) };
            body.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.3f, Height = 2f } });

            AddChild(trunk);
            AddChild(canopy);
            AddChild(body);

            // ECS
            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + localPos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent
            {
                ResourceId = "wood", YieldMin = 2, YieldMax = 5, HitsRemaining = 3,
            });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { trunk, canopy, body };
        }

        // ── 浆果丛 ────────────────────────────────────────────────────

        private void SpawnBush(Vector3 localPos)
        {
            var bush = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.65f, Height = 1.3f },
                Position = localPos + new Vector3(0f, 0.65f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColBush },
            };
            AddChild(bush);

            // ECS
            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + localPos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent
            {
                ResourceId = "berry", YieldMin = 2, YieldMax = 4, HitsRemaining = 5,
            });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { bush };
        }

        // ── 石头（Forest 群系）────────────────────────────────────────

        private void SpawnStone(Vector3 localPos)
        {
            var rock = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.45f, BottomRadius = 0.55f, Height = 0.5f },
                Position = localPos + new Vector3(0f, 0.25f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColStone },
            };
            AddChild(rock);

            var body = new StaticBody3D { Position = localPos + new Vector3(0f, 0.25f, 0f) };
            body.AddChild(new CollisionShape3D
            {
                Shape = new SphereShape3D { Radius = 0.5f }
            });
            AddChild(body);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + localPos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent
            {
                ResourceId = "stone", YieldMin = 2, YieldMax = 4, HitsRemaining = 5,
            });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock, body };
        }

        // ── 纤维丛（Grassland 群系）───────────────────────────────────

        private void SpawnFiber(Vector3 localPos)
        {
            var plant = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.15f, Height = 0.8f },
                Position = localPos + new Vector3(0f, 0.4f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColFiber },
            };
            AddChild(plant);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + localPos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent
            {
                ResourceId = "fiber", YieldMin = 2, YieldMax = 5, HitsRemaining = 4,
            });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { plant };
        }

        // ── 清理 ──────────────────────────────────────────────────────

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("resource_harvested", OnResourceHarvested);
            foreach (var eid in _entityIds)
                EcsWorld.Instance.DestroyEntity(eid);
            _entityIds.Clear();
            _resourceNodes.Clear();
        }
    }
}
