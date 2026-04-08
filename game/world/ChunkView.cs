using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Systems;
using SurvivalGame.Entities.Creatures;

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
        private static readonly Color ColDesert    = new(0.82f, 0.72f, 0.42f);
        private static readonly Color ColSnow      = new(0.88f, 0.92f, 0.96f);
        private static readonly Color ColSwamp     = new(0.28f, 0.38f, 0.22f);
        private static readonly Color ColVolcano   = new(0.28f, 0.18f, 0.15f);
        private static readonly Color ColTrunk     = new(0.40f, 0.25f, 0.10f);
        private static readonly Color ColLeaf      = new(0.20f, 0.50f, 0.15f);
        private static readonly Color ColBush      = new(0.22f, 0.58f, 0.18f);
        private static readonly Color ColStone     = new(0.55f, 0.55f, 0.55f);
        private static readonly Color ColFiber     = new(0.70f, 0.85f, 0.45f);
        // 沙漠
        private static readonly Color ColCactus    = new(0.35f, 0.60f, 0.25f);
        private static readonly Color ColSandRock  = new(0.72f, 0.60f, 0.35f);
        // 雪原
        private static readonly Color ColDeadTree  = new(0.50f, 0.45f, 0.38f);
        private static readonly Color ColSnowRock  = new(0.80f, 0.85f, 0.90f);
        // 沼泽
        private static readonly Color ColReed      = new(0.45f, 0.55f, 0.28f);
        private static readonly Color ColMudRock   = new(0.38f, 0.32f, 0.22f);
        // 火山
        private static readonly Color ColObsidian  = new(0.18f, 0.12f, 0.14f);
        private static readonly Color ColLavaRock  = new(0.55f, 0.22f, 0.10f);

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

            var biome = data.GetTile(ChunkData.Size / 2, ChunkData.Size / 2).Biome;

            BuildTerrain(biome);
            SpawnResources(data, biome);
            SpawnBiomeCreature(data, biome);
        }

        // ── 地形 ──────────────────────────────────────────────────────

        private void BuildTerrain(BiomeType biome)
        {
            float half   = ChunkData.Size / 2f;
            var   center = new Vector3(half, 0f, half);

            Color groundColor = biome switch
            {
                BiomeType.Forest    => ColForest,
                BiomeType.Desert    => ColDesert,
                BiomeType.Snow      => ColSnow,
                BiomeType.Swamp     => ColSwamp,
                BiomeType.Volcano   => ColVolcano,
                _                   => ColGrassland,
            };

            // 地面网格
            AddChild(new MeshInstance3D
            {
                Mesh             = new PlaneMesh { Size = new Vector2(ChunkData.Size, ChunkData.Size) },
                Position         = center,
                MaterialOverride = new StandardMaterial3D { AlbedoColor = groundColor },
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

        private void SpawnResources(ChunkData data, BiomeType biome)
        {
            var rng = new RandomNumberGenerator();
            rng.Seed = (ulong)unchecked(data.Coord.X * 73856093 ^ data.Coord.Y * 19349663);

            const int Grid = 4;
            float cell = (float)ChunkData.Size / Grid;

            // 主资源（每群系各有特色）
            int   maxSpawn = 6;
            float chance   = 0.40f;
            int   spawned  = 0;

            for (int gx = 0; gx < Grid && spawned < maxSpawn; gx++)
            for (int gz = 0; gz < Grid && spawned < maxSpawn; gz++)
            {
                if (rng.Randf() > chance) continue;
                float lx = (gx + 0.5f) * cell + rng.RandfRange(-cell * 0.3f, cell * 0.3f);
                float lz = (gz + 0.5f) * cell + rng.RandfRange(-cell * 0.3f, cell * 0.3f);
                var lp = new Vector3(lx, 0f, lz);

                switch (biome)
                {
                    case BiomeType.Forest:    SpawnTree(lp);      break;
                    case BiomeType.Grassland: SpawnBush(lp);      break;
                    case BiomeType.Desert:    SpawnCactus(lp);    break;
                    case BiomeType.Snow:      SpawnDeadTree(lp);  break;
                    case BiomeType.Swamp:     SpawnReed(lp);      break;
                    case BiomeType.Volcano:   SpawnLavaRock(lp);  break;
                }
                spawned++;
            }

            // 次级资源（石头 / 纤维 / 沙岩 / 雪石 / 泥石 / 黑曜石）
            int   maxExtra    = 3;
            float extraChance = 0.28f;
            int   extraSpawn  = 0;

            for (int gx = 0; gx < Grid && extraSpawn < maxExtra; gx++)
            for (int gz = 0; gz < Grid && extraSpawn < maxExtra; gz++)
            {
                if (rng.Randf() > extraChance) continue;
                float lx = (gx + 0.5f) * cell + rng.RandfRange(-cell * 0.3f, cell * 0.3f);
                float lz = (gz + 0.5f) * cell + rng.RandfRange(-cell * 0.3f, cell * 0.3f);
                var lp = new Vector3(lx, 0f, lz);

                switch (biome)
                {
                    case BiomeType.Forest:    SpawnStone(lp);     break;
                    case BiomeType.Grassland: SpawnFiber(lp);     break;
                    case BiomeType.Desert:    SpawnSandRock(lp);  break;
                    case BiomeType.Snow:      SpawnSnowRock(lp);  break;
                    case BiomeType.Swamp:     SpawnMudRock(lp);   break;
                    case BiomeType.Volcano:   SpawnObsidian(lp);  break;
                }
                extraSpawn++;
            }
        }

        // ── 群系生物随机生成（每个 Chunk 约 20% 概率出现一只）────────

        private void SpawnBiomeCreature(ChunkData data, BiomeType biome)
        {
            string? speciesId = biome switch
            {
                BiomeType.Desert  => "sand_fox",
                BiomeType.Snow    => "snow_wolf",
                BiomeType.Swamp   => "swamp_toad",
                BiomeType.Volcano => "fire_lizard",
                _                 => null,   // 森林/草原由 GameManager 负责初始生成
            };
            if (speciesId == null) return;

            var spawner = CreatureSpawner.Instance;
            if (spawner == null) return;

            var rng = new RandomNumberGenerator();
            rng.Seed = (ulong)unchecked(data.Coord.X * 48271 ^ data.Coord.Y * 97331 ^ 999);
            if (rng.Randf() > 0.20f) return;  // 20% 概率

            float lx = rng.RandfRange(3f, ChunkData.Size - 3f);
            float lz = rng.RandfRange(3f, ChunkData.Size - 3f);
            var worldPos = data.ChunkOriginWorld + new Vector3(lx, 0f, lz);
            spawner.SpawnCreature(speciesId, worldPos);
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

        // ── 仙人掌（Desert 群系，产纤维）─────────────────────────────

        private void SpawnCactus(Vector3 localPos)
        {
            var body_mesh = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.22f, Height = 1.8f },
                Position = localPos + new Vector3(0f, 0.9f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColCactus },
            };
            var arm = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 0.8f },
                Position = localPos + new Vector3(0.4f, 1.1f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColCactus },
            };
            arm.RotationDegrees = new Vector3(0f, 0f, 90f);
            AddChild(body_mesh);
            AddChild(arm);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + localPos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent
            {
                ResourceId = "fiber", YieldMin = 2, YieldMax = 4, HitsRemaining = 3,
            });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { body_mesh, arm };
        }

        // ── 沙岩（Desert 群系，产石头）───────────────────────────────

        private void SpawnSandRock(Vector3 localPos)
        {
            var rock = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.40f, BottomRadius = 0.55f, Height = 0.6f },
                Position = localPos + new Vector3(0f, 0.3f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColSandRock },
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + localPos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent
            {
                ResourceId = "stone", YieldMin = 2, YieldMax = 4, HitsRemaining = 4,
            });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        // ── 枯树（Snow 群系，产木材）──────────────────────────────────

        private void SpawnDeadTree(Vector3 localPos)
        {
            var trunk = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.25f, Height = 2.5f },
                Position = localPos + new Vector3(0f, 1.25f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColDeadTree },
            };
            AddChild(trunk);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + localPos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent
            {
                ResourceId = "wood", YieldMin = 1, YieldMax = 3, HitsRemaining = 2,
            });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { trunk };
        }

        // ── 雪地岩石（Snow 群系，产石头）─────────────────────────────

        private void SpawnSnowRock(Vector3 localPos)
        {
            var rock = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.5f, Height = 0.7f },
                Position = localPos + new Vector3(0f, 0.35f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColSnowRock },
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + localPos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent
            {
                ResourceId = "stone", YieldMin = 2, YieldMax = 5, HitsRemaining = 5,
            });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        // ── 沼泽芦苇（Swamp 群系，产纤维）────────────────────────────

        private void SpawnReed(Vector3 localPos)
        {
            var reed = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.08f, Height = 1.6f },
                Position = localPos + new Vector3(0f, 0.8f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColReed },
            };
            AddChild(reed);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + localPos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent
            {
                ResourceId = "fiber", YieldMin = 3, YieldMax = 6, HitsRemaining = 4,
            });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { reed };
        }

        // ── 泥石（Swamp 群系，产石头）────────────────────────────────

        private void SpawnMudRock(Vector3 localPos)
        {
            var rock = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.45f, Height = 0.6f },
                Position = localPos + new Vector3(0f, 0.3f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColMudRock },
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + localPos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent
            {
                ResourceId = "stone", YieldMin = 1, YieldMax = 3, HitsRemaining = 3,
            });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        // ── 熔岩岩石（Volcano 群系，产石头）──────────────────────────

        private void SpawnLavaRock(Vector3 localPos)
        {
            var rock = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.45f, BottomRadius = 0.65f, Height = 0.8f },
                Position = localPos + new Vector3(0f, 0.4f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColLavaRock },
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + localPos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent
            {
                ResourceId = "stone", YieldMin = 3, YieldMax = 6, HitsRemaining = 4,
            });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        // ── 黑曜石（Volcano 群系，产石头 × 更多）────────────────────

        private void SpawnObsidian(Vector3 localPos)
        {
            var rock = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.55f, Height = 0.9f },
                Position = localPos + new Vector3(0f, 0.45f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColObsidian },
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + localPos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent
            {
                ResourceId = "stone", YieldMin = 4, YieldMax = 8, HitsRemaining = 6,
            });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
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
