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
    /// 视觉升级（四步）：
    ///  ① 光照由 GameManager 统一设置
    ///  ② ArrayMesh 地形：20×20 细分 + 高度噪声（±0.12 m）+ 顶点颜色扰动
    ///  ③ 树木 / 资源网格：随机缩放 / 旋转 / 多球冠，所有资源落在地面高度上
    ///  ④ SpawnAmbientDetails：每 Chunk 散布 24–34 个纯视觉装饰（草丛/蘑菇/花/卵石…）
    /// </summary>
    public partial class ChunkView : Node3D
    {
        private readonly List<int>                    _entityIds     = new();
        private readonly Dictionary<int, List<Node3D>> _resourceNodes = new();

        // 地形噪声（BuildTerrain 初始化，TerrainY 使用）
        private FastNoiseLite? _heightNoise;
        private float          _biomeHeightScale;
        private Vector2I       _chunkCoord;

        // ── 颜色常量 ──────────────────────────────────────────────────
        private static readonly Color ColForest    = new(0.22f, 0.45f, 0.18f);
        private static readonly Color ColGrassland = new(0.38f, 0.62f, 0.25f);
        private static readonly Color ColDesert    = new(0.82f, 0.72f, 0.42f);
        private static readonly Color ColSnow      = new(0.88f, 0.92f, 0.96f);
        private static readonly Color ColSwamp     = new(0.28f, 0.38f, 0.22f);
        private static readonly Color ColVolcano   = new(0.28f, 0.18f, 0.15f);

        private static readonly Color ColTrunk    = new(0.40f, 0.25f, 0.10f);
        private static readonly Color ColLeaf     = new(0.20f, 0.50f, 0.15f);
        private static readonly Color ColBush     = new(0.22f, 0.58f, 0.18f);
        private static readonly Color ColStone    = new(0.55f, 0.55f, 0.55f);
        private static readonly Color ColFiber    = new(0.70f, 0.85f, 0.45f);
        private static readonly Color ColCactus   = new(0.35f, 0.60f, 0.25f);
        private static readonly Color ColSandRock = new(0.72f, 0.60f, 0.35f);
        private static readonly Color ColDeadTree = new(0.50f, 0.45f, 0.38f);
        private static readonly Color ColSnowRock = new(0.80f, 0.85f, 0.90f);
        private static readonly Color ColReed     = new(0.45f, 0.55f, 0.28f);
        private static readonly Color ColMudRock  = new(0.38f, 0.32f, 0.22f);
        private static readonly Color ColObsidian = new(0.18f, 0.12f, 0.14f);
        private static readonly Color ColLavaRock = new(0.55f, 0.22f, 0.10f);

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

        public void Setup(ChunkData data)
        {
            GlobalPosition = data.ChunkOriginWorld;
            _chunkCoord    = data.Coord;

            var biome = data.GetTile(ChunkData.Size / 2, ChunkData.Size / 2).Biome;

            BuildTerrain(biome);
            SpawnResources(data, biome);
            SpawnAmbientDetails(biome);
            SpawnBiomeCreature(data, biome);
        }

        // ══════════════════════════════════════════════════════════════
        // ② 地形：ArrayMesh + 高度噪声 + 顶点颜色扰动
        // ══════════════════════════════════════════════════════════════

        private void BuildTerrain(BiomeType biome)
        {
            // 高度噪声（世界坐标采样 → 相邻 Chunk 无缝拼接）
            _heightNoise = new FastNoiseLite();
            _heightNoise.Seed        = 42;
            _heightNoise.Frequency   = 0.055f;
            _heightNoise.FractalOctaves = 2;

            _biomeHeightScale = biome switch
            {
                BiomeType.Desert  => 0.06f,
                BiomeType.Snow    => 0.08f,
                BiomeType.Volcano => 0.07f,
                _                 => 0.12f,
            };

            // 颜色扰动噪声
            var colorNoise = new FastNoiseLite();
            colorNoise.Seed      = 137;
            colorNoise.Frequency = 0.12f;

            Color baseColor = biome switch
            {
                BiomeType.Forest    => ColForest,
                BiomeType.Desert    => ColDesert,
                BiomeType.Snow      => ColSnow,
                BiomeType.Swamp     => ColSwamp,
                BiomeType.Volcano   => ColVolcano,
                _                   => ColGrassland,
            };

            const int Divs  = 20;
            float     size  = ChunkData.Size;
            float     step  = size / Divs;
            int       vCnt  = (Divs + 1) * (Divs + 1);

            var verts   = new Vector3[vCnt];
            var normals = new Vector3[vCnt];
            var colors  = new Color[vCnt];

            // 顶点位置 + 颜色
            for (int zi = 0; zi <= Divs; zi++)
            for (int xi = 0; xi <= Divs; xi++)
            {
                int   i  = zi * (Divs + 1) + xi;
                float lx = xi * step;
                float lz = zi * step;
                float wx = GlobalPosition.X + lx;
                float wz = GlobalPosition.Z + lz;

                float h  = _heightNoise.GetNoise2D(wx, wz) * _biomeHeightScale;
                verts[i] = new Vector3(lx, h, lz);

                // 亮度微扰动（±5%），低处略暗模拟湿润感
                float cv   = colorNoise.GetNoise2D(wx, wz) * 0.05f - h * 0.3f;
                colors[i]  = new Color(
                    Mathf.Clamp(baseColor.R + cv,        0f, 1f),
                    Mathf.Clamp(baseColor.G + cv * 0.8f, 0f, 1f),
                    Mathf.Clamp(baseColor.B + cv,        0f, 1f));
            }

            // 法线（中央差分近似梯度）
            for (int zi = 0; zi <= Divs; zi++)
            for (int xi = 0; xi <= Divs; xi++)
            {
                int i  = zi * (Divs + 1) + xi;
                int xl = Mathf.Max(xi - 1, 0), xr = Mathf.Min(xi + 1, Divs);
                int zd = Mathf.Max(zi - 1, 0), zu = Mathf.Min(zi + 1, Divs);
                float hl = verts[zi * (Divs + 1) + xl].Y;
                float hr = verts[zi * (Divs + 1) + xr].Y;
                float hd = verts[zd * (Divs + 1) + xi].Y;
                float hu = verts[zu * (Divs + 1) + xi].Y;
                normals[i] = new Vector3(
                    -(hr - hl) / ((xr - xl) * step),
                    1f,
                    -(hu - hd) / ((zu - zd) * step)).Normalized();
            }

            // 三角形索引
            int[] indices = new int[Divs * Divs * 6];
            int   idx     = 0;
            for (int zi = 0; zi < Divs; zi++)
            for (int xi = 0; xi < Divs; xi++)
            {
                int tl = zi * (Divs + 1) + xi, tr = tl + 1;
                int bl = (zi + 1) * (Divs + 1) + xi, br = bl + 1;
                indices[idx++] = tl; indices[idx++] = bl; indices[idx++] = tr;
                indices[idx++] = tr; indices[idx++] = bl; indices[idx++] = br;
            }

            // 组装 ArrayMesh
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = verts;
            arrays[(int)Mesh.ArrayType.Normal] = normals;
            arrays[(int)Mesh.ArrayType.Color]  = colors;
            arrays[(int)Mesh.ArrayType.Index]  = indices;

            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

            var mat = new StandardMaterial3D { VertexColorUseAsAlbedo = true };
            AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = mat });

            // 碰撞（薄箱体，顶面在 Y≈0，高度变化幅度小可接受）
            float half = ChunkData.Size / 2f;
            var body   = new StaticBody3D { Position = new Vector3(half, -0.05f, half) };
            body.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(ChunkData.Size, 0.1f, ChunkData.Size) }
            });
            AddChild(body);
        }

        /// <summary>返回局部坐标 (lx,lz) 处的地形高度，供资源 / 装饰落地使用。</summary>
        private float TerrainY(float lx, float lz)
        {
            if (_heightNoise == null) return 0f;
            return _heightNoise.GetNoise2D(GlobalPosition.X + lx, GlobalPosition.Z + lz)
                   * _biomeHeightScale;
        }

        // ══════════════════════════════════════════════════════════════
        // 程序化资源（有 ECS + 碰撞）
        // ══════════════════════════════════════════════════════════════

        private void SpawnResources(ChunkData data, BiomeType biome)
        {
            var rng  = new RandomNumberGenerator();
            rng.Seed = (ulong)unchecked(data.Coord.X * 73856093 ^ data.Coord.Y * 19349663);

            const int Grid = 4;
            float cell = (float)ChunkData.Size / Grid;

            int   maxSpawn = 6;
            float chance   = 0.40f;
            int   spawned  = 0;

            for (int gx = 0; gx < Grid && spawned < maxSpawn; gx++)
            for (int gz = 0; gz < Grid && spawned < maxSpawn; gz++)
            {
                if (rng.Randf() > chance) continue;
                float lx = (gx + 0.5f) * cell + rng.RandfRange(-cell * 0.3f, cell * 0.3f);
                float lz = (gz + 0.5f) * cell + rng.RandfRange(-cell * 0.3f, cell * 0.3f);
                var   lp = new Vector3(lx, 0f, lz);

                switch (biome)
                {
                    case BiomeType.Forest:    SpawnTree(lp,     rng); break;
                    case BiomeType.Grassland: SpawnBush(lp,     rng); break;
                    case BiomeType.Desert:    SpawnCactus(lp,   rng); break;
                    case BiomeType.Snow:      SpawnDeadTree(lp, rng); break;
                    case BiomeType.Swamp:     SpawnReed(lp,     rng); break;
                    case BiomeType.Volcano:   SpawnLavaRock(lp, rng); break;
                }
                spawned++;
            }

            int   maxExtra    = 3;
            float extraChance = 0.28f;
            int   extraSpawn  = 0;

            for (int gx = 0; gx < Grid && extraSpawn < maxExtra; gx++)
            for (int gz = 0; gz < Grid && extraSpawn < maxExtra; gz++)
            {
                if (rng.Randf() > extraChance) continue;
                float lx = (gx + 0.5f) * cell + rng.RandfRange(-cell * 0.3f, cell * 0.3f);
                float lz = (gz + 0.5f) * cell + rng.RandfRange(-cell * 0.3f, cell * 0.3f);
                var   lp = new Vector3(lx, 0f, lz);

                switch (biome)
                {
                    case BiomeType.Forest:    SpawnStone(lp,    rng); break;
                    case BiomeType.Grassland: SpawnFiber(lp,    rng); break;
                    case BiomeType.Desert:    SpawnSandRock(lp, rng); break;
                    case BiomeType.Snow:      SpawnSnowRock(lp, rng); break;
                    case BiomeType.Swamp:     SpawnMudRock(lp,  rng); break;
                    case BiomeType.Volcano:   SpawnObsidian(lp, rng); break;
                }
                extraSpawn++;
            }
        }

        // ── 群系生物随机生成 ──────────────────────────────────────────

        private void SpawnBiomeCreature(ChunkData data, BiomeType biome)
        {
            string? speciesId = biome switch
            {
                BiomeType.Desert  => "sand_fox",
                BiomeType.Snow    => "snow_wolf",
                BiomeType.Swamp   => "swamp_toad",
                BiomeType.Volcano => "fire_lizard",
                _                 => null,
            };
            if (speciesId == null) return;

            var spawner = CreatureSpawner.Instance;
            if (spawner == null) return;

            var rng  = new RandomNumberGenerator();
            rng.Seed = (ulong)unchecked(data.Coord.X * 48271 ^ data.Coord.Y * 97331 ^ 999);
            if (rng.Randf() > 0.20f) return;

            float lx      = rng.RandfRange(3f, ChunkData.Size - 3f);
            float lz      = rng.RandfRange(3f, ChunkData.Size - 3f);
            var worldPos  = data.ChunkOriginWorld + new Vector3(lx, 0f, lz);
            spawner.SpawnCreature(speciesId, worldPos);
        }

        // ══════════════════════════════════════════════════════════════
        // ③ 资源网格（随机缩放 / 旋转 / 多球冠，落在地面高度）
        // ══════════════════════════════════════════════════════════════

        private void SpawnTree(Vector3 lp, RandomNumberGenerator rng)
        {
            float scale    = rng.RandfRange(0.85f, 1.20f);
            float yRot     = rng.RandfRange(0f, 360f);
            float lean     = rng.RandfRange(-4f, 4f);
            float ty       = TerrainY(lp.X, lp.Z);
            var   basePos  = new Vector3(lp.X, ty, lp.Z);

            // 树干
            var trunk = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.18f * scale, BottomRadius = 0.28f * scale, Height = 1.8f * scale },
                Position = basePos + new Vector3(0f, 0.9f * scale, 0f),
                RotationDegrees = new Vector3(lean, yRot, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColTrunk },
            };

            // 主球冠
            float lv1 = rng.RandfRange(-0.04f, 0.04f);
            var canopy1 = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 1.05f * scale, Height = 2.1f * scale },
                Position = basePos + new Vector3(0f, 3.1f * scale, 0f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = lv1 < 0 ? ColLeaf.Darkened(-lv1) : ColLeaf.Lightened(lv1)
                },
            };

            // 次级球冠（偏移，更亮）
            float ox = rng.RandfRange(-0.55f, 0.55f) * scale;
            float oz = rng.RandfRange(-0.55f, 0.55f) * scale;
            var canopy2 = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.65f * scale, Height = 1.3f * scale },
                Position = basePos + new Vector3(ox, 4.0f * scale, oz),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColLeaf.Lightened(0.10f) },
            };

            // 碰撞
            var body = new StaticBody3D { Position = basePos + new Vector3(0f, 1f, 0f) };
            body.AddChild(new CollisionShape3D
            {
                Shape = new CapsuleShape3D { Radius = 0.3f * scale, Height = 1.8f * scale }
            });

            AddChild(trunk); AddChild(canopy1); AddChild(canopy2); AddChild(body);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + basePos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "wood", YieldMin = 2, YieldMax = 5, HitsRemaining = 3 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { trunk, canopy1, canopy2, body };
        }

        private void SpawnBush(Vector3 lp, RandomNumberGenerator rng)
        {
            float scale   = rng.RandfRange(0.75f, 1.25f);
            float yRot    = rng.RandfRange(0f, 360f);
            float cv      = rng.RandfRange(-0.05f, 0.05f);
            float ty      = TerrainY(lp.X, lp.Z);
            var   basePos = new Vector3(lp.X, ty, lp.Z);
            var   bushCol = new Color(
                Mathf.Clamp(ColBush.R + cv, 0f, 1f),
                Mathf.Clamp(ColBush.G + cv, 0f, 1f),
                Mathf.Clamp(ColBush.B + cv, 0f, 1f));

            var bush = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.65f * scale, Height = 1.3f * scale },
                Position = basePos + new Vector3(0f, 0.65f * scale, 0f),
                RotationDegrees = new Vector3(0f, yRot, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = bushCol },
            };
            AddChild(bush);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + basePos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "berry", YieldMin = 2, YieldMax = 4, HitsRemaining = 5 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { bush };
        }

        private void SpawnStone(Vector3 lp, RandomNumberGenerator rng)
        {
            float scale   = rng.RandfRange(0.7f, 1.3f);
            float yRot    = rng.RandfRange(0f, 360f);
            float ty      = TerrainY(lp.X, lp.Z);
            var   basePos = new Vector3(lp.X, ty, lp.Z);

            var rock = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.45f * scale, BottomRadius = 0.55f * scale, Height = 0.5f * scale },
                Position = basePos + new Vector3(0f, 0.25f * scale, 0f),
                RotationDegrees = new Vector3(0f, yRot, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColStone },
            };
            var body = new StaticBody3D { Position = basePos + new Vector3(0f, 0.25f * scale, 0f) };
            body.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.5f * scale } });
            AddChild(rock); AddChild(body);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + basePos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "stone", YieldMin = 2, YieldMax = 4, HitsRemaining = 5 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock, body };
        }

        private void SpawnFiber(Vector3 lp, RandomNumberGenerator rng)
        {
            float scale   = rng.RandfRange(0.8f, 1.2f);
            float yRot    = rng.RandfRange(0f, 360f);
            float lean    = rng.RandfRange(-8f, 8f);
            float ty      = TerrainY(lp.X, lp.Z);
            var   basePos = new Vector3(lp.X, ty, lp.Z);

            var plant = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.14f * scale, Height = 0.8f * scale },
                Position = basePos + new Vector3(0f, 0.4f * scale, 0f),
                RotationDegrees = new Vector3(lean, yRot, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColFiber },
            };
            AddChild(plant);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + basePos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "fiber", YieldMin = 2, YieldMax = 5, HitsRemaining = 4 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { plant };
        }

        private void SpawnCactus(Vector3 lp, RandomNumberGenerator rng)
        {
            float scale   = rng.RandfRange(0.8f, 1.2f);
            float yRot    = rng.RandfRange(0f, 360f);
            float ty      = TerrainY(lp.X, lp.Z);
            var   basePos = new Vector3(lp.X, ty, lp.Z);

            var bodyMesh = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.22f * scale, BottomRadius = 0.22f * scale, Height = 1.8f * scale },
                Position = basePos + new Vector3(0f, 0.9f * scale, 0f),
                RotationDegrees = new Vector3(0f, yRot, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColCactus },
            };
            var arm = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.12f * scale, Height = 0.8f * scale },
                Position = basePos + new Vector3(0.4f * scale, 1.1f * scale, 0f),
                RotationDegrees = new Vector3(0f, yRot, 90f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColCactus },
            };
            AddChild(bodyMesh); AddChild(arm);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + basePos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "fiber", YieldMin = 2, YieldMax = 4, HitsRemaining = 3 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { bodyMesh, arm };
        }

        private void SpawnSandRock(Vector3 lp, RandomNumberGenerator rng)
        {
            float scale   = rng.RandfRange(0.7f, 1.3f);
            float yRot    = rng.RandfRange(0f, 360f);
            float ty      = TerrainY(lp.X, lp.Z);
            var   basePos = new Vector3(lp.X, ty, lp.Z);

            var rock = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.40f * scale, BottomRadius = 0.55f * scale, Height = 0.6f * scale },
                Position = basePos + new Vector3(0f, 0.3f * scale, 0f),
                RotationDegrees = new Vector3(0f, yRot, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColSandRock },
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + basePos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "stone", YieldMin = 2, YieldMax = 4, HitsRemaining = 4 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        private void SpawnDeadTree(Vector3 lp, RandomNumberGenerator rng)
        {
            float scale   = rng.RandfRange(0.8f, 1.2f);
            float yRot    = rng.RandfRange(0f, 360f);
            float lean    = rng.RandfRange(-6f, 6f);
            float ty      = TerrainY(lp.X, lp.Z);
            var   basePos = new Vector3(lp.X, ty, lp.Z);

            var trunk = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.22f * scale, Height = 2.4f * scale },
                Position = basePos + new Vector3(0f, 1.2f * scale, 0f),
                RotationDegrees = new Vector3(lean, yRot, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColDeadTree },
            };
            AddChild(trunk);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + basePos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "wood", YieldMin = 1, YieldMax = 3, HitsRemaining = 2 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { trunk };
        }

        private void SpawnSnowRock(Vector3 lp, RandomNumberGenerator rng)
        {
            float scale   = rng.RandfRange(0.7f, 1.3f);
            float yRot    = rng.RandfRange(0f, 360f);
            float ty      = TerrainY(lp.X, lp.Z);
            var   basePos = new Vector3(lp.X, ty, lp.Z);

            var rock = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.50f * scale, Height = 0.70f * scale },
                Position = basePos + new Vector3(0f, 0.35f * scale, 0f),
                RotationDegrees = new Vector3(0f, yRot, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColSnowRock },
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + basePos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "stone", YieldMin = 2, YieldMax = 5, HitsRemaining = 5 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        private void SpawnReed(Vector3 lp, RandomNumberGenerator rng)
        {
            float scale   = rng.RandfRange(0.8f, 1.2f);
            float yRot    = rng.RandfRange(0f, 360f);
            float lean    = rng.RandfRange(-12f, 12f);
            float ty      = TerrainY(lp.X, lp.Z);
            var   basePos = new Vector3(lp.X, ty, lp.Z);

            var reed = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.08f * scale, Height = 1.6f * scale },
                Position = basePos + new Vector3(0f, 0.8f * scale, 0f),
                RotationDegrees = new Vector3(lean, yRot, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColReed },
            };
            AddChild(reed);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + basePos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "fiber", YieldMin = 3, YieldMax = 6, HitsRemaining = 4 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { reed };
        }

        private void SpawnMudRock(Vector3 lp, RandomNumberGenerator rng)
        {
            float scale   = rng.RandfRange(0.8f, 1.2f);
            float yRot    = rng.RandfRange(0f, 360f);
            float ty      = TerrainY(lp.X, lp.Z);
            var   basePos = new Vector3(lp.X, ty, lp.Z);

            var rock = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.45f * scale, Height = 0.60f * scale },
                Position = basePos + new Vector3(0f, 0.3f * scale, 0f),
                RotationDegrees = new Vector3(0f, yRot, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColMudRock },
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + basePos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "stone", YieldMin = 1, YieldMax = 3, HitsRemaining = 3 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        private void SpawnLavaRock(Vector3 lp, RandomNumberGenerator rng)
        {
            float scale   = rng.RandfRange(0.75f, 1.30f);
            float yRot    = rng.RandfRange(0f, 360f);
            float ty      = TerrainY(lp.X, lp.Z);
            var   basePos = new Vector3(lp.X, ty, lp.Z);

            var rock = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.45f * scale, BottomRadius = 0.65f * scale, Height = 0.8f * scale },
                Position = basePos + new Vector3(0f, 0.4f * scale, 0f),
                RotationDegrees = new Vector3(0f, yRot, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColLavaRock },
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + basePos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "stone", YieldMin = 3, YieldMax = 6, HitsRemaining = 4 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        private void SpawnObsidian(Vector3 lp, RandomNumberGenerator rng)
        {
            float scale   = rng.RandfRange(0.8f, 1.2f);
            float yRot    = rng.RandfRange(0f, 360f);
            float ty      = TerrainY(lp.X, lp.Z);
            var   basePos = new Vector3(lp.X, ty, lp.Z);

            var rock = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.55f * scale, Height = 0.90f * scale },
                Position = basePos + new Vector3(0f, 0.45f * scale, 0f),
                RotationDegrees = new Vector3(0f, yRot, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ColObsidian },
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = GlobalPosition + basePos });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "stone", YieldMin = 4, YieldMax = 8, HitsRemaining = 6 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        // ══════════════════════════════════════════════════════════════
        // ④ 纯视觉装饰（无 ECS / 碰撞，随 ChunkView 自动释放）
        // ══════════════════════════════════════════════════════════════

        private void SpawnAmbientDetails(BiomeType biome)
        {
            var rng  = new RandomNumberGenerator();
            rng.Seed = (ulong)unchecked(_chunkCoord.X * 31415927L ^ _chunkCoord.Y * 27182818L ^ 11111111L);

            float size  = ChunkData.Size;
            int   count = rng.RandiRange(24, 34);

            for (int i = 0; i < count; i++)
            {
                float lx      = rng.RandfRange(0.8f, size - 0.8f);
                float lz      = rng.RandfRange(0.8f, size - 0.8f);
                float ty      = TerrainY(lx, lz);
                var   localPos = new Vector3(lx, ty, lz);

                switch (biome)
                {
                    case BiomeType.Forest:    SpawnForestDetail(localPos,    rng); break;
                    case BiomeType.Grassland: SpawnGrasslandDetail(localPos, rng); break;
                    case BiomeType.Desert:    SpawnDesertDetail(localPos,    rng); break;
                    case BiomeType.Snow:      SpawnSnowDetail(localPos,      rng); break;
                    case BiomeType.Swamp:     SpawnSwampDetail(localPos,     rng); break;
                    case BiomeType.Volcano:   SpawnVolcanoDetail(localPos,   rng); break;
                }
            }
        }

        // ── 各群系细节 ────────────────────────────────────────────────

        private void SpawnForestDetail(Vector3 pos, RandomNumberGenerator rng)
        {
            float roll  = rng.Randf();
            float scale = rng.RandfRange(0.7f, 1.3f);

            if (roll < 0.40f)          // 草丛（最多）
            {
                float cv  = rng.RandfRange(-0.04f, 0.04f);
                SpawnGrassTuft(pos, new Color(0.18f + cv, 0.42f + cv, 0.12f + cv),
                               scale, rng.RandfRange(0f, 360f));
            }
            else if (roll < 0.60f)     // 蘑菇
            {
                var capColors = new Color[]
                {
                    new(0.90f, 0.85f, 0.80f), // 白色
                    new(0.60f, 0.35f, 0.15f), // 棕色
                    new(0.82f, 0.14f, 0.10f), // 红色
                };
                SpawnMushroom(pos, capColors[rng.RandiRange(0, 2)], scale);
            }
            else if (roll < 0.78f)     // 落叶堆
            {
                float lv = rng.RandfRange(0f, 0.12f);
                DetailDisc(pos, new Color(0.52f + lv, 0.32f + lv, 0.10f), 0.30f * scale, 0.04f);
            }
            else                       // 鹅卵石
            {
                SmallPebble(pos, new Color(0.50f, 0.50f, 0.48f), scale);
            }
        }

        private void SpawnGrasslandDetail(Vector3 pos, RandomNumberGenerator rng)
        {
            float roll  = rng.Randf();
            float scale = rng.RandfRange(0.8f, 1.2f);

            if (roll < 0.48f)          // 草丛
            {
                float cv = rng.RandfRange(-0.04f, 0.04f);
                SpawnGrassTuft(pos, new Color(0.32f + cv, 0.58f + cv, 0.20f + cv),
                               scale, rng.RandfRange(0f, 360f));
            }
            else if (roll < 0.75f)     // 野花
            {
                var petals = new Color[]
                {
                    new(0.96f, 0.90f, 0.20f), // 黄
                    new(0.90f, 0.90f, 0.95f), // 白
                    new(0.62f, 0.30f, 0.88f), // 紫
                };
                SpawnWildflower(pos, petals[rng.RandiRange(0, 2)], scale);
            }
            else                       // 小石头
            {
                SmallPebble(pos, new Color(0.52f, 0.50f, 0.45f), scale);
            }
        }

        private void SpawnDesertDetail(Vector3 pos, RandomNumberGenerator rng)
        {
            float roll  = rng.Randf();
            float scale = rng.RandfRange(0.7f, 1.3f);

            if (roll < 0.45f)          // 沙堆
            {
                DetailDisc(pos, new Color(0.86f, 0.76f, 0.52f), 0.38f * scale, 0.07f);
            }
            else if (roll < 0.72f)     // 枯草
            {
                float cv = rng.RandfRange(-0.04f, 0.04f);
                SpawnGrassTuft(pos, new Color(0.70f + cv, 0.60f, 0.24f),
                               scale * 0.65f, rng.RandfRange(0f, 360f));
            }
            else                       // 沙色碎石
            {
                SmallPebble(pos, new Color(0.70f, 0.60f, 0.40f), scale);
            }
        }

        private void SpawnSnowDetail(Vector3 pos, RandomNumberGenerator rng)
        {
            float roll  = rng.Randf();
            float scale = rng.RandfRange(0.7f, 1.3f);

            if (roll < 0.50f)          // 雪堆
            {
                DetailDisc(pos, new Color(0.92f, 0.94f, 0.98f), 0.32f * scale, 0.09f);
            }
            else if (roll < 0.78f)     // 冰晶
            {
                AddChild(new MeshInstance3D
                {
                    Mesh     = new CylinderMesh { TopRadius = 0.03f * scale, BottomRadius = 0.07f * scale, Height = 0.22f * scale },
                    Position = pos + new Vector3(0f, 0.11f * scale, 0f),
                    RotationDegrees = new Vector3(rng.RandfRange(-15f, 15f), rng.RandfRange(0f, 360f), 0f),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.78f, 0.88f, 0.98f) },
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
            }
            else                       // 雪盖石
            {
                SmallPebble(pos, new Color(0.82f, 0.86f, 0.92f), scale);
            }
        }

        private void SpawnSwampDetail(Vector3 pos, RandomNumberGenerator rng)
        {
            float roll  = rng.Randf();
            float scale = rng.RandfRange(0.8f, 1.2f);

            if (roll < 0.38f)          // 泥坑
            {
                DetailDisc(pos, new Color(0.20f, 0.16f, 0.11f), 0.42f * scale, 0.04f);
            }
            else if (roll < 0.68f)     // 沼草
            {
                float cv = rng.RandfRange(-0.03f, 0.03f);
                SpawnGrassTuft(pos, new Color(0.24f + cv, 0.36f, 0.18f + cv),
                               scale, rng.RandfRange(0f, 360f));
            }
            else                       // 树根碎块
            {
                AddChild(new MeshInstance3D
                {
                    Mesh     = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.08f * scale, Height = 0.30f * scale },
                    Position = pos + new Vector3(0f, 0.05f * scale, 0f),
                    RotationDegrees = new Vector3(rng.RandfRange(-35f, -15f), rng.RandfRange(0f, 360f), 0f),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.25f, 0.14f) },
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
            }
        }

        private void SpawnVolcanoDetail(Vector3 pos, RandomNumberGenerator rng)
        {
            float roll  = rng.Randf();
            float scale = rng.RandfRange(0.8f, 1.3f);

            if (roll < 0.48f)          // 灰烬堆
            {
                DetailDisc(pos, new Color(0.34f, 0.30f, 0.30f), 0.38f * scale, 0.06f);
            }
            else if (roll < 0.75f)     // 余烬（自发光）
            {
                var emberMat = new StandardMaterial3D
                {
                    AlbedoColor              = new Color(0.95f, 0.35f, 0.05f),
                    EmissionEnabled          = true,
                    Emission                 = new Color(0.80f, 0.20f, 0.02f),
                    EmissionEnergyMultiplier = 1.5f,
                };
                AddChild(new MeshInstance3D
                {
                    Mesh     = new SphereMesh { Radius = 0.06f * scale, Height = 0.08f * scale },
                    Position = pos + new Vector3(0f, 0.04f * scale, 0f),
                    MaterialOverride = emberMat,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
            }
            else                       // 暗色碎石
            {
                SmallPebble(pos, new Color(0.20f, 0.14f, 0.14f), scale * 1.2f);
            }
        }

        // ── 装饰原语 ──────────────────────────────────────────────────

        /// <summary>两块薄板交叉形成草丛。</summary>
        private void SpawnGrassTuft(Vector3 pos, Color col, float scale, float yRot)
        {
            var mat = new StandardMaterial3D { AlbedoColor = col };
            for (int i = 0; i < 2; i++)
            {
                AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.35f * scale, 0.28f * scale, 0.025f) },
                    Position = pos + new Vector3(0f, 0.14f * scale, 0f),
                    RotationDegrees = new Vector3(0f, yRot + i * 90f, 0f),
                    MaterialOverride = mat,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
            }
        }

        /// <summary>茎 + 花冠。</summary>
        private void SpawnWildflower(Vector3 pos, Color petalColor, float scale)
        {
            var stemMat  = new StandardMaterial3D { AlbedoColor = new Color(0.28f, 0.55f, 0.20f) };
            var petalMat = new StandardMaterial3D { AlbedoColor = petalColor };
            AddChild(new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.018f, Height = 0.28f * scale },
                Position = pos + new Vector3(0f, 0.14f * scale, 0f),
                MaterialOverride = stemMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
            AddChild(new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.07f * scale, Height = 0.10f * scale },
                Position = pos + new Vector3(0f, 0.32f * scale, 0f),
                MaterialOverride = petalMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }

        /// <summary>茎 + 伞盖。</summary>
        private void SpawnMushroom(Vector3 pos, Color capColor, float scale)
        {
            var stemMat = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.82f, 0.76f) };
            var capMat  = new StandardMaterial3D { AlbedoColor = capColor };
            AddChild(new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.055f * scale, BottomRadius = 0.055f * scale, Height = 0.18f * scale },
                Position = pos + new Vector3(0f, 0.09f * scale, 0f),
                MaterialOverride = stemMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
            AddChild(new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.12f * scale, Height = 0.13f * scale },
                Position = pos + new Vector3(0f, 0.24f * scale, 0f),
                MaterialOverride = capMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }

        /// <summary>扁平圆盘（落叶堆 / 沙堆 / 泥坑 / 灰烬）。</summary>
        private void DetailDisc(Vector3 pos, Color col, float radius, float thickness)
        {
            AddChild(new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = thickness },
                Position = pos + new Vector3(0f, thickness * 0.5f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = col },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }

        /// <summary>小卵石。</summary>
        private void SmallPebble(Vector3 pos, Color col, float scale)
        {
            AddChild(new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.09f * scale, Height = 0.12f * scale },
                Position = pos + new Vector3(0f, 0.06f * scale, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = col },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
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
