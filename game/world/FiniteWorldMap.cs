using Godot;
using System;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Systems;
using SurvivalGame.Entities.Creatures;

namespace SurvivalGame.World
{
    /// <summary>
    /// 有限大小地图 — 256×256 m 岛屿地形。
    /// 替代无限 Chunk 流式加载，提供精致有限世界。
    ///
    /// 特性：
    ///  • 128×128 网格（2 m/格，共 16641 顶点）
    ///  • FastNoiseLite 高度生成 ±14 m，带岛屿衰减
    ///  • 手工群系分区（归一化坐标）
    ///  • 每群系独立 ArrayMesh 面，加载 Flux 生成的地面贴图
    ///  • 水面：Y=0 半透明蓝色平面
    ///  • 碰撞：HeightMapShape3D 真实起伏碰撞
    ///  • 全图资源/装饰散布
    /// </summary>
    public partial class FiniteWorldMap : Node3D
    {
        public static FiniteWorldMap? Instance { get; private set; }

        // ── 地图参数 ─────────────────────────────────────────────────
        public const float MapSize    = 256f;
        public const float WaterLevel = 0f;
        private const int  GridDivs  = 128;
        private const float CellSize  = MapSize / GridDivs;   // 2 m

        // 高度场（GridDivs+1 × GridDivs+1 个顶点）
        private readonly float[,] _heights = new float[GridDivs + 1, GridDivs + 1];
        private FastNoiseLite      _noise   = null!;

        // 资源节点（用于采集后隐藏）
        private readonly List<int>                    _entityIds     = new();
        private readonly Dictionary<int, List<Node3D>> _resourceNodes = new();

        // ── 颜色常量（贴图缺失时的顶点颜色备用）────────────────────
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

        // ── 资源贴图缓存（实例级，每次运行重新加载，避免编辑器缓存旧 null）──
        private readonly Dictionary<string, Texture2D?> _texCache = new();

        private StandardMaterial3D MakeMat(string texName, Color fallback,
            bool billboard = false)
        {
            if (!_texCache.TryGetValue(texName, out var tex))
            {
                string path = $"res://assets/textures/resources/{texName}.png";
                tex = ResourceLoader.Exists(path)
                    ? ResourceLoader.Load<Texture2D>(path)
                    : null;
                _texCache[texName] = tex;
            }
            var mat = new StandardMaterial3D { AlbedoColor = fallback };
            if (tex != null)
            {
                mat.AlbedoTexture = tex;
                mat.AlbedoColor   = Colors.White;  // 不叠加色偏，让贴图显示原色
            }
            if (billboard)
            {
                mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
                mat.BillboardKeepScale = true;
                mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
                mat.AlphaScissorThreshold = 0.15f;
                // Crop bottom 22% of UV — removes the white ring Flux
                // tends to leave at the base of top-down sprites.
                mat.Uv1Scale = new Vector3(1f, 0.78f, 1f);
            }
            return mat;
        }

        // ── 生命周期 ─────────────────────────────────────────────────

        public override void _Ready()
        {
            Instance = this;
            Name     = "FiniteWorldMap";

            InitNoise();
            ComputeHeights();
            BuildTerrainMesh();
            BuildWater();
            BuildCollision();
            SpawnAllResources();
            SpawnAllAmbient();
            SpawnBiomeCreatures();

            EventBus.Instance.Subscribe("resource_harvested", OnResourceHarvested);

            GD.Print($"[FiniteWorldMap] {MapSize}×{MapSize} m 岛屿地图已生成，实体数 {_entityIds.Count}");
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("resource_harvested", OnResourceHarvested);
            Instance = null;
        }

        // ── 公共接口 ─────────────────────────────────────────────────

        /// <summary>根据世界坐标返回群系。</summary>
        public BiomeType GetBiomeAt(Vector3 worldPos)
        {
            float nx = Mathf.Clamp(worldPos.X / MapSize, 0f, 1f);
            float nz = Mathf.Clamp(worldPos.Z / MapSize, 0f, 1f);
            return BiomeFromNormalized(nx, nz);
        }

        /// <summary>双线性插值获取地形高度。</summary>
        public float GetTerrainHeight(Vector3 worldPos)
        {
            float px = Mathf.Clamp(worldPos.X / CellSize, 0f, GridDivs - 0.001f);
            float pz = Mathf.Clamp(worldPos.Z / CellSize, 0f, GridDivs - 0.001f);
            int   ix = (int)px, iz = (int)pz;
            float tx = px - ix, tz = pz - iz;
            return Mathf.Lerp(
                Mathf.Lerp(_heights[ix, iz],   _heights[ix + 1, iz],   tx),
                Mathf.Lerp(_heights[ix, iz + 1], _heights[ix + 1, iz + 1], tx),
                tz);
        }

        /// <summary>坐标是否在地图范围内。</summary>
        public bool IsInBounds(Vector3 worldPos)
            => worldPos.X >= 0f && worldPos.X <= MapSize
            && worldPos.Z >= 0f && worldPos.Z <= MapSize;

        // ── 噪声初始化 ───────────────────────────────────────────────

        private void InitNoise()
        {
            _noise = new FastNoiseLite
            {
                Seed            = 42,
                NoiseType       = FastNoiseLite.NoiseTypeEnum.Perlin,
                Frequency       = 0.012f,
                FractalType     = FastNoiseLite.FractalTypeEnum.Fbm,
                FractalOctaves  = 4,
            };
        }

        // ── 高度场计算（含岛屿衰减）──────────────────────────────────

        private void ComputeHeights()
        {
            float cx = MapSize * 0.5f, cz = MapSize * 0.5f;

            for (int ix = 0; ix <= GridDivs; ix++)
            for (int iz = 0; iz <= GridDivs; iz++)
            {
                float wx = ix * CellSize;
                float wz = iz * CellSize;

                float raw = _noise.GetNoise2D(wx, wz) * 14f;  // ±14 m

                // 岛屿衰减：距中心 >100 m 渐降
                float dist = Mathf.Sqrt((wx - cx) * (wx - cx) + (wz - cz) * (wz - cz));
                float falloff = Mathf.SmoothStep(0f, 1f, (dist - 90f) / 38f);
                raw = Mathf.Lerp(raw, -3f, falloff);

                _heights[ix, iz] = raw;
            }
        }

        // ── 地形网格（多面，每群系一面 + 各自贴图）───────────────────

        private void BuildTerrainMesh()
        {
            // 每群系的三角形顶点缓冲
            var verts  = new Dictionary<BiomeType, List<Vector3>>();
            var norms  = new Dictionary<BiomeType, List<Vector3>>();
            var uvs    = new Dictionary<BiomeType, List<Vector2>>();
            var inds   = new Dictionary<BiomeType, List<int>>();

            foreach (BiomeType bt in Enum.GetValues<BiomeType>())
            {
                verts[bt] = new(); norms[bt] = new();
                uvs[bt]   = new(); inds[bt]  = new();
            }

            for (int ix = 0; ix < GridDivs; ix++)
            for (int iz = 0; iz < GridDivs; iz++)
            {
                float wx0 = ix * CellSize,       wz0 = iz * CellSize;
                float wx1 = (ix + 1) * CellSize, wz1 = (iz + 1) * CellSize;

                var v00 = new Vector3(wx0, _heights[ix,   iz  ], wz0);
                var v10 = new Vector3(wx1, _heights[ix+1, iz  ], wz0);
                var v01 = new Vector3(wx0, _heights[ix,   iz+1], wz1);
                var v11 = new Vector3(wx1, _heights[ix+1, iz+1], wz1);

                var n00 = VertexNormal(ix,   iz);
                var n10 = VertexNormal(ix+1, iz);
                var n01 = VertexNormal(ix,   iz+1);
                var n11 = VertexNormal(ix+1, iz+1);

                // UV: 地图归一化坐标（贴图约 8 次重复）
                float uvScale = 8f;
                var u00 = new Vector2(wx0 / MapSize * uvScale, wz0 / MapSize * uvScale);
                var u10 = new Vector2(wx1 / MapSize * uvScale, wz0 / MapSize * uvScale);
                var u01 = new Vector2(wx0 / MapSize * uvScale, wz1 / MapSize * uvScale);
                var u11 = new Vector2(wx1 / MapSize * uvScale, wz1 / MapSize * uvScale);

                // 按格子中心归属群系
                float cnx = (ix + 0.5f) * CellSize / MapSize;
                float cnz = (iz + 0.5f) * CellSize / MapSize;
                var bt = BiomeFromNormalized(cnx, cnz);

                int  baseIdx = verts[bt].Count;
                verts[bt].AddRange(new[] { v00, v10, v11, v01 });
                norms[bt].AddRange(new[] { n00, n10, n11, n01 });
                uvs[bt].AddRange(new[]   { u00, u10, u11, u01 });
                // Winding matches ChunkView (working): tl,bl,tr / tr,bl,br
                // v00=baseIdx(tl), v10=baseIdx+1(tr), v11=baseIdx+2(br), v01=baseIdx+3(bl)
                inds[bt].AddRange(new[]  { baseIdx,   baseIdx+3, baseIdx+1,
                                           baseIdx+1, baseIdx+3, baseIdx+2 });
            }

            // 构建 ArrayMesh
            var mesh = new ArrayMesh();
            var texturePaths = new Dictionary<BiomeType, string>
            {
                [BiomeType.Forest]    = "res://assets/textures/terrain/forest.png",
                [BiomeType.Grassland] = "res://assets/textures/terrain/grassland.png",
                [BiomeType.Desert]    = "res://assets/textures/terrain/desert.png",
                [BiomeType.Snow]      = "res://assets/textures/terrain/snow.png",
                [BiomeType.Swamp]     = "res://assets/textures/terrain/swamp.png",
                [BiomeType.Volcano]   = "res://assets/textures/terrain/volcano.png",
            };
            var fallbackColors = new Dictionary<BiomeType, Color>
            {
                [BiomeType.Forest]    = ColForest,
                [BiomeType.Grassland] = ColGrassland,
                [BiomeType.Desert]    = ColDesert,
                [BiomeType.Snow]      = ColSnow,
                [BiomeType.Swamp]     = ColSwamp,
                [BiomeType.Volcano]   = ColVolcano,
            };

            foreach (BiomeType bt in Enum.GetValues<BiomeType>())
            {
                if (verts[bt].Count == 0) continue;

                var arrays = new Godot.Collections.Array();
                arrays.Resize((int)Mesh.ArrayType.Max);
                arrays[(int)Mesh.ArrayType.Vertex] = verts[bt].ToArray();
                arrays[(int)Mesh.ArrayType.Normal] = norms[bt].ToArray();
                arrays[(int)Mesh.ArrayType.TexUV]  = uvs[bt].ToArray();
                arrays[(int)Mesh.ArrayType.Index]  = inds[bt].ToArray();

                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                int surfaceIdx = mesh.GetSurfaceCount() - 1;

                var mat = new StandardMaterial3D
                {
                    // 关闭背面剔除：无论 winding order 如何，地形从相机任意角度可见
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                };

                // 尝试加载 Flux 贴图
                if (ResourceLoader.Exists(texturePaths[bt]))
                {
                    var tex = ResourceLoader.Load<Texture2D>(texturePaths[bt]);
                    if (tex != null)
                    {
                        mat.AlbedoTexture = tex;
                        mat.Uv1Scale      = new Vector3(1f, 1f, 1f);
                        GD.Print($"[FiniteWorldMap] 加载群系贴图：{texturePaths[bt]}");
                    }
                    else
                    {
                        mat.AlbedoColor = fallbackColors[bt];
                    }
                }
                else
                {
                    mat.AlbedoColor = fallbackColors[bt];
                }

                mesh.SurfaceSetMaterial(surfaceIdx, mat);
            }

            var mi = new MeshInstance3D { Mesh = mesh, Name = "TerrainMesh" };
            AddChild(mi);
        }

        // ── 水面 ─────────────────────────────────────────────────────

        private void BuildWater()
        {
            var waterMesh = new MeshInstance3D { Name = "Water" };
            waterMesh.Mesh = new PlaneMesh
            {
                Size          = new Vector2(MapSize, MapSize),
                SubdivideDepth = 0,
                SubdivideWidth = 0,
            };
            waterMesh.Position = new Vector3(MapSize * 0.5f, WaterLevel - 0.05f, MapSize * 0.5f);

            var mat = new StandardMaterial3D
            {
                AlbedoColor    = new Color(0.10f, 0.42f, 0.75f, 0.65f),
                Transparency   = BaseMaterial3D.TransparencyEnum.Alpha,
                EmissionEnabled = true,
                Emission       = new Color(0.05f, 0.20f, 0.45f),
                EmissionEnergyMultiplier = 0.18f,
                CullMode       = BaseMaterial3D.CullModeEnum.Disabled,
            };
            waterMesh.MaterialOverride = mat;
            AddChild(waterMesh);
        }

        // ── 碰撞（HeightMapShape3D）──────────────────────────────────

        private void BuildCollision()
        {
            float[] data = new float[(GridDivs + 1) * (GridDivs + 1)];
            for (int iz = 0; iz <= GridDivs; iz++)
            for (int ix = 0; ix <= GridDivs; ix++)
                data[iz * (GridDivs + 1) + ix] = _heights[ix, iz];

            var shape = new HeightMapShape3D
            {
                MapWidth  = GridDivs + 1,
                MapDepth  = GridDivs + 1,
                MapData   = data,
            };

            var body  = new StaticBody3D { Name = "TerrainBody" };
            var cshape = new CollisionShape3D { Shape = shape };
            // HeightMapShape3D 以中心为原点，偏移到地图中心
            cshape.Position = new Vector3(MapSize * 0.5f, 0f, MapSize * 0.5f);
            // 缩放使格子对齐
            cshape.Scale = new Vector3(CellSize, 1f, CellSize);
            body.AddChild(cshape);
            AddChild(body);
        }

        // ── 资源散布（全图，按群系） ──────────────────────────────────

        private void SpawnAllResources()
        {
            const int ResourceCellSize = 8;   // 每 8m 一格
            int cells = (int)(MapSize / ResourceCellSize);

            var rng = new RandomNumberGenerator();
            rng.Seed = 20240101UL;

            for (int gx = 0; gx < cells; gx++)
            for (int gz = 0; gz < cells; gz++)
            {
                if (rng.Randf() > 0.42f) continue;

                float wx = gx * ResourceCellSize + rng.RandfRange(1f, ResourceCellSize - 1f);
                float wz = gz * ResourceCellSize + rng.RandfRange(1f, ResourceCellSize - 1f);

                float terrainH = GetTerrainHeight(new Vector3(wx, 0f, wz));
                if (terrainH < 0.15f) continue;   // 跳过水下

                float nx = wx / MapSize, nz = wz / MapSize;
                var biome = BiomeFromNormalized(nx, nz);
                var wp = new Vector3(wx, terrainH, wz);

                switch (biome)
                {
                    case BiomeType.Forest:    SpawnTree(wp,     rng); break;
                    case BiomeType.Grassland: SpawnBush(wp,     rng); break;
                    case BiomeType.Desert:    SpawnCactus(wp,   rng); break;
                    case BiomeType.Snow:      SpawnDeadTree(wp, rng); break;
                    case BiomeType.Swamp:     SpawnReed(wp,     rng); break;
                    case BiomeType.Volcano:   SpawnLavaRock(wp, rng); break;
                }
            }

            // 二次散布（石头/纤维等）
            rng.Seed = 20240202UL;
            for (int gx = 0; gx < cells; gx++)
            for (int gz = 0; gz < cells; gz++)
            {
                if (rng.Randf() > 0.22f) continue;

                float wx = gx * ResourceCellSize + rng.RandfRange(1f, ResourceCellSize - 1f);
                float wz = gz * ResourceCellSize + rng.RandfRange(1f, ResourceCellSize - 1f);

                float terrainH = GetTerrainHeight(new Vector3(wx, 0f, wz));
                if (terrainH < 0.15f) continue;

                float nx = wx / MapSize, nz = wz / MapSize;
                var biome = BiomeFromNormalized(nx, nz);
                var wp = new Vector3(wx, terrainH, wz);

                switch (biome)
                {
                    case BiomeType.Forest:    SpawnStone(wp,    rng); break;
                    case BiomeType.Grassland: SpawnFiber(wp,    rng); break;
                    case BiomeType.Desert:    SpawnSandRock(wp, rng); break;
                    case BiomeType.Snow:      SpawnSnowRock(wp, rng); break;
                    case BiomeType.Swamp:     SpawnMudRock(wp,  rng); break;
                    case BiomeType.Volcano:   SpawnObsidian(wp, rng); break;
                }
            }
        }

        // ── 装饰散布（纯视觉，无 ECS） ───────────────────────────────

        private void SpawnAllAmbient()
        {
            const int TotalDecorations = 700;
            var rng = new RandomNumberGenerator();
            rng.Seed = 20240303UL;

            int placed = 0;
            int attempts = TotalDecorations * 3;

            for (int i = 0; i < attempts && placed < TotalDecorations; i++)
            {
                float wx = rng.RandfRange(2f, MapSize - 2f);
                float wz = rng.RandfRange(2f, MapSize - 2f);

                float terrainH = GetTerrainHeight(new Vector3(wx, 0f, wz));
                if (terrainH < 0.1f) continue;   // 水下跳过

                float nx = wx / MapSize, nz = wz / MapSize;
                var biome = BiomeFromNormalized(nx, nz);
                var wp = new Vector3(wx, terrainH, wz);

                SpawnDetailForBiome(wp, biome, rng);
                placed++;
            }
        }

        // ── 生物散布 ─────────────────────────────────────────────────

        private void SpawnBiomeCreatures()
        {
            var spawner = CreatureSpawner.Instance;
            if (spawner == null) return;

            var rng = new RandomNumberGenerator();
            rng.Seed = 20240404UL;

            var biomeSpecies = new Dictionary<BiomeType, (string id, int count)>
            {
                [BiomeType.Desert]  = ("sand_fox",   3),
                [BiomeType.Snow]    = ("snow_wolf",  3),
                [BiomeType.Swamp]   = ("swamp_toad", 3),
                [BiomeType.Volcano] = ("fire_lizard",2),
            };

            foreach (var (biome, (speciesId, count)) in biomeSpecies)
            {
                for (int k = 0; k < count; k++)
                {
                    Vector3? pos = FindLandPosition(biome, rng);
                    if (pos == null) continue;
                    spawner.SpawnCreature(speciesId, pos.Value);
                }
            }
        }

        private Vector3? FindLandPosition(BiomeType targetBiome, RandomNumberGenerator rng)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float wx = rng.RandfRange(5f, MapSize - 5f);
                float wz = rng.RandfRange(5f, MapSize - 5f);
                float h  = GetTerrainHeight(new Vector3(wx, 0f, wz));
                if (h < 0.5f) continue;

                var b = BiomeFromNormalized(wx / MapSize, wz / MapSize);
                if (b == targetBiome) return new Vector3(wx, h, wz);
            }
            return null;
        }

        // ── 群系分区（归一化坐标 nx/nz ∈ [0,1]）──────────────────────

        private static BiomeType BiomeFromNormalized(float nx, float nz)
        {
            if (nz < 0.18f)                               return BiomeType.Snow;
            if (nx < 0.20f && nz < 0.35f)                return BiomeType.Snow;
            if (nx > 0.75f && nz > 0.60f)                return BiomeType.Volcano;
            if (nx > 0.78f)                               return BiomeType.Desert;
            if (nx < 0.22f && nz > 0.38f)                return BiomeType.Swamp;
            if (nz < 0.48f && nx >= 0.20f && nx <= 0.75f) return BiomeType.Forest;
            return BiomeType.Grassland;
        }

        // ── 法线计算（中央差分）──────────────────────────────────────

        private Vector3 VertexNormal(int ix, int iz)
        {
            float left  = _heights[Math.Max(ix - 1, 0),        iz] ;
            float right = _heights[Math.Min(ix + 1, GridDivs), iz] ;
            float down  = _heights[ix, Math.Max(iz - 1, 0)       ] ;
            float up    = _heights[ix, Math.Min(iz + 1, GridDivs)] ;

            var tangentX = new Vector3(2f * CellSize, right - left, 0f);
            var tangentZ = new Vector3(0f, up - down, 2f * CellSize);
            // tangentZ × tangentX gives Y-up normal for Y-up height field
            return tangentZ.Cross(tangentX).Normalized();
        }

        // ── 资源采集事件 ─────────────────────────────────────────────

        private void OnResourceHarvested(object? payload)
        {
            if (payload is not HarvestEventData data) return;
            if (!data.Depleted) return;
            if (!_resourceNodes.TryGetValue(data.NodeEntityId, out var nodes)) return;
            foreach (var n in nodes) n.QueueFree();
            _resourceNodes.Remove(data.NodeEntityId);
        }

        // ══════════════════════════════════════════════════════════════
        // 资源网格（ECS + 碰撞）
        // ══════════════════════════════════════════════════════════════

        private void SpawnTree(Vector3 wp, RandomNumberGenerator rng)
        {
            float scale = rng.RandfRange(0.85f, 1.20f);
            float yRot  = rng.RandfRange(0f, 360f);
            float lean  = rng.RandfRange(-4f, 4f);

            var trunk = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.18f * scale, BottomRadius = 0.28f * scale, Height = 1.8f * scale },
                Position = wp + new Vector3(0f, 0.9f * scale, 0f),
                RotationDegrees  = new Vector3(lean, yRot, 0f),
                MaterialOverride = MakeMat("bark", ColTrunk),
            };
            float lv1 = rng.RandfRange(-0.04f, 0.04f);
            float csz1 = 2.2f * scale;
            var canopy1 = new MeshInstance3D
            {
                Mesh     = new QuadMesh { Size = new Vector2(csz1, csz1) },
                Position = wp + new Vector3(0f, 2.8f * scale, 0f),
                MaterialOverride = MakeMat("canopy",
                    lv1 < 0 ? ColLeaf.Darkened(-lv1) : ColLeaf.Lightened(lv1), billboard: true),
            };
            float ox = rng.RandfRange(-0.55f, 0.55f) * scale;
            float oz = rng.RandfRange(-0.55f, 0.55f) * scale;
            float csz2 = 1.4f * scale;
            var canopy2 = new MeshInstance3D
            {
                Mesh     = new QuadMesh { Size = new Vector2(csz2, csz2) },
                Position = wp + new Vector3(ox, 3.8f * scale, oz),
                MaterialOverride = MakeMat("canopy", ColLeaf.Lightened(0.10f), billboard: true),
            };
            var body = new StaticBody3D { Position = wp + new Vector3(0f, 1f, 0f) };
            body.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.3f * scale, Height = 1.8f * scale } });

            AddChild(trunk); AddChild(canopy1); AddChild(canopy2); AddChild(body);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = wp });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "wood", YieldMin = 2, YieldMax = 5, HitsRemaining = 3 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { trunk, canopy1, canopy2, body };
        }

        private void SpawnBush(Vector3 wp, RandomNumberGenerator rng)
        {
            float scale  = rng.RandfRange(0.75f, 1.25f);
            float cv     = rng.RandfRange(-0.05f, 0.05f);
            var   color  = new Color(Mathf.Clamp(ColBush.R + cv, 0f, 1f),
                                     Mathf.Clamp(ColBush.G + cv, 0f, 1f),
                                     Mathf.Clamp(ColBush.B + cv, 0f, 1f));

            float sz = 1.4f * scale;
            var bush = new MeshInstance3D
            {
                Mesh     = new QuadMesh { Size = new Vector2(sz, sz) },
                Position = wp + new Vector3(0f, sz * 0.4f, 0f),
                MaterialOverride = MakeMat("bush_berry", color, billboard: true),
            };
            AddChild(bush);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = wp });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "berry", YieldMin = 2, YieldMax = 4, HitsRemaining = 5 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { bush };
        }

        private void SpawnStone(Vector3 wp, RandomNumberGenerator rng)
        {
            float scale = rng.RandfRange(0.7f, 1.3f);
            float sz = 1.1f * scale;
            var rock = new MeshInstance3D
            {
                Mesh     = new QuadMesh { Size = new Vector2(sz, sz) },
                Position = wp + new Vector3(0f, sz * 0.4f, 0f),
                MaterialOverride = MakeMat("rock", ColStone, billboard: true),
            };
            var body = new StaticBody3D { Position = wp + new Vector3(0f, 0.3f * scale, 0f) };
            body.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.5f * scale } });
            AddChild(rock); AddChild(body);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = wp });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "stone", YieldMin = 2, YieldMax = 4, HitsRemaining = 5 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock, body };
        }

        private void SpawnFiber(Vector3 wp, RandomNumberGenerator rng)
        {
            float scale = rng.RandfRange(0.8f, 1.2f);
            float yRot  = rng.RandfRange(0f, 360f);
            float lean  = rng.RandfRange(-8f, 8f);
            var plant = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.14f * scale, Height = 0.8f * scale },
                Position = wp + new Vector3(0f, 0.4f * scale, 0f),
                RotationDegrees  = new Vector3(lean, yRot, 0f),
                MaterialOverride = MakeMat("plant_fiber", ColFiber),
            };
            AddChild(plant);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = wp });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "fiber", YieldMin = 2, YieldMax = 5, HitsRemaining = 4 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { plant };
        }

        private void SpawnCactus(Vector3 wp, RandomNumberGenerator rng)
        {
            float scale = rng.RandfRange(0.8f, 1.2f);
            float yRot  = rng.RandfRange(0f, 360f);
            var cactusMat = MakeMat("cactus", ColCactus);
            var bodyM = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.22f * scale, BottomRadius = 0.22f * scale, Height = 1.8f * scale },
                Position = wp + new Vector3(0f, 0.9f * scale, 0f),
                RotationDegrees  = new Vector3(0f, yRot, 0f),
                MaterialOverride = cactusMat,
            };
            var arm = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.12f * scale, Height = 0.8f * scale },
                Position = wp + new Vector3(0.4f * scale, 1.1f * scale, 0f),
                RotationDegrees  = new Vector3(0f, yRot, 90f),
                MaterialOverride = cactusMat,
            };
            AddChild(bodyM); AddChild(arm);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = wp });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "fiber", YieldMin = 2, YieldMax = 4, HitsRemaining = 3 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { bodyM, arm };
        }

        private void SpawnSandRock(Vector3 wp, RandomNumberGenerator rng)
        {
            float scale = rng.RandfRange(0.7f, 1.3f);
            float sz = 1.1f * scale;
            var rock = new MeshInstance3D
            {
                Mesh     = new QuadMesh { Size = new Vector2(sz, sz) },
                Position = wp + new Vector3(0f, sz * 0.4f, 0f),
                MaterialOverride = MakeMat("rock", ColSandRock, billboard: true),
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = wp });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "stone", YieldMin = 2, YieldMax = 4, HitsRemaining = 4 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        private void SpawnDeadTree(Vector3 wp, RandomNumberGenerator rng)
        {
            float scale = rng.RandfRange(0.8f, 1.2f);
            float yRot  = rng.RandfRange(0f, 360f);
            float lean  = rng.RandfRange(-6f, 6f);
            var trunk = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.22f * scale, Height = 2.4f * scale },
                Position = wp + new Vector3(0f, 1.2f * scale, 0f),
                RotationDegrees  = new Vector3(lean, yRot, 0f),
                MaterialOverride = MakeMat("dead_wood", ColDeadTree),
            };
            AddChild(trunk);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = wp });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "wood", YieldMin = 1, YieldMax = 3, HitsRemaining = 2 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { trunk };
        }

        private void SpawnSnowRock(Vector3 wp, RandomNumberGenerator rng)
        {
            float scale = rng.RandfRange(0.7f, 1.3f);
            float sz = 1.0f * scale;
            var rock = new MeshInstance3D
            {
                Mesh     = new QuadMesh { Size = new Vector2(sz, sz) },
                Position = wp + new Vector3(0f, sz * 0.4f, 0f),
                MaterialOverride = MakeMat("snow_rock", ColSnowRock, billboard: true),
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = wp });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "stone", YieldMin = 2, YieldMax = 5, HitsRemaining = 5 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        private void SpawnReed(Vector3 wp, RandomNumberGenerator rng)
        {
            float scale = rng.RandfRange(0.8f, 1.2f);
            float yRot  = rng.RandfRange(0f, 360f);
            float lean  = rng.RandfRange(-12f, 12f);
            var reed = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.08f * scale, Height = 1.6f * scale },
                Position = wp + new Vector3(0f, 0.8f * scale, 0f),
                RotationDegrees  = new Vector3(lean, yRot, 0f),
                MaterialOverride = MakeMat("reed", ColReed),
            };
            AddChild(reed);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = wp });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "fiber", YieldMin = 3, YieldMax = 6, HitsRemaining = 4 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { reed };
        }

        private void SpawnMudRock(Vector3 wp, RandomNumberGenerator rng)
        {
            float scale = rng.RandfRange(0.8f, 1.2f);
            float sz = 0.9f * scale;
            var rock = new MeshInstance3D
            {
                Mesh     = new QuadMesh { Size = new Vector2(sz, sz) },
                Position = wp + new Vector3(0f, sz * 0.4f, 0f),
                MaterialOverride = MakeMat("rock", ColMudRock, billboard: true),
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = wp });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "stone", YieldMin = 1, YieldMax = 3, HitsRemaining = 3 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        private void SpawnLavaRock(Vector3 wp, RandomNumberGenerator rng)
        {
            float scale = rng.RandfRange(0.75f, 1.30f);
            float sz = 1.2f * scale;
            var rock = new MeshInstance3D
            {
                Mesh     = new QuadMesh { Size = new Vector2(sz, sz) },
                Position = wp + new Vector3(0f, sz * 0.4f, 0f),
                MaterialOverride = MakeMat("volcanic", ColLavaRock, billboard: true),
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = wp });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "stone", YieldMin = 3, YieldMax = 6, HitsRemaining = 4 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        private void SpawnObsidian(Vector3 wp, RandomNumberGenerator rng)
        {
            float scale = rng.RandfRange(0.8f, 1.2f);
            float sz = 1.1f * scale;
            var rock = new MeshInstance3D
            {
                Mesh     = new QuadMesh { Size = new Vector2(sz, sz) },
                Position = wp + new Vector3(0f, sz * 0.4f, 0f),
                MaterialOverride = MakeMat("volcanic", ColObsidian, billboard: true),
            };
            AddChild(rock);

            int eid = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(eid, new PositionComponent { Position = wp });
            EcsWorld.Instance.AddComponent(eid, new HarvestableComponent { ResourceId = "stone", YieldMin = 4, YieldMax = 8, HitsRemaining = 6 });
            _entityIds.Add(eid);
            _resourceNodes[eid] = new List<Node3D> { rock };
        }

        // ══════════════════════════════════════════════════════════════
        // 纯视觉装饰
        // ══════════════════════════════════════════════════════════════

        private void SpawnDetailForBiome(Vector3 wp, BiomeType biome, RandomNumberGenerator rng)
        {
            switch (biome)
            {
                case BiomeType.Forest:    SpawnForestDetail(wp,    rng); break;
                case BiomeType.Grassland: SpawnGrasslandDetail(wp, rng); break;
                case BiomeType.Desert:    SpawnDesertDetail(wp,    rng); break;
                case BiomeType.Snow:      SpawnSnowDetail(wp,      rng); break;
                case BiomeType.Swamp:     SpawnSwampDetail(wp,     rng); break;
                case BiomeType.Volcano:   SpawnVolcanoDetail(wp,   rng); break;
            }
        }

        private void SpawnForestDetail(Vector3 wp, RandomNumberGenerator rng)
        {
            float roll = rng.Randf(), scale = rng.RandfRange(0.7f, 1.3f);
            if (roll < 0.40f)
            {
                float cv = rng.RandfRange(-0.04f, 0.04f);
                SpawnGrassTuft(wp, new Color(0.18f + cv, 0.42f + cv, 0.12f + cv),
                               scale, rng.RandfRange(0f, 360f));
            }
            else if (roll < 0.60f)
            {
                Color[] caps = { new(0.90f, 0.85f, 0.80f), new(0.60f, 0.35f, 0.15f), new(0.82f, 0.14f, 0.10f) };
                SpawnMushroom(wp, caps[rng.RandiRange(0, 2)], scale);
            }
            else if (roll < 0.78f)
            {
                float lv = rng.RandfRange(0f, 0.12f);
                DetailDisc(wp, new Color(0.52f + lv, 0.32f + lv, 0.10f), 0.30f * scale, 0.04f);
            }
            else
                SmallPebble(wp, new Color(0.50f, 0.50f, 0.48f), scale);
        }

        private void SpawnGrasslandDetail(Vector3 wp, RandomNumberGenerator rng)
        {
            float roll = rng.Randf(), scale = rng.RandfRange(0.8f, 1.2f);
            if (roll < 0.48f)
            {
                float cv = rng.RandfRange(-0.04f, 0.04f);
                SpawnGrassTuft(wp, new Color(0.32f + cv, 0.58f + cv, 0.20f + cv),
                               scale, rng.RandfRange(0f, 360f));
            }
            else if (roll < 0.75f)
            {
                Color[] petals = { new(0.95f, 0.30f, 0.30f), new(0.95f, 0.85f, 0.20f),
                                   new(0.95f, 0.55f, 0.85f), new(0.60f, 0.60f, 0.95f) };
                SpawnWildflower(wp, petals[rng.RandiRange(0, 3)], scale);
            }
            else
                SmallPebble(wp, new Color(0.60f, 0.58f, 0.50f), scale);
        }

        private void SpawnDesertDetail(Vector3 wp, RandomNumberGenerator rng)
        {
            float roll = rng.Randf(), scale = rng.RandfRange(0.7f, 1.3f);
            if (roll < 0.50f)
                DetailDisc(wp, new Color(0.80f + rng.RandfRange(-0.05f, 0.05f),
                                         0.65f, 0.38f), 0.28f * scale, 0.03f);
            else
                SmallPebble(wp, new Color(0.72f, 0.62f, 0.42f), scale);
        }

        private void SpawnSnowDetail(Vector3 wp, RandomNumberGenerator rng)
        {
            float roll = rng.Randf(), scale = rng.RandfRange(0.8f, 1.2f);
            if (roll < 0.55f)
                DetailDisc(wp, new Color(0.90f, 0.93f, 0.97f), 0.32f * scale, 0.03f);
            else
                SmallPebble(wp, new Color(0.75f, 0.80f, 0.85f), scale);
        }

        private void SpawnSwampDetail(Vector3 wp, RandomNumberGenerator rng)
        {
            float roll = rng.Randf(), scale = rng.RandfRange(0.8f, 1.2f);
            if (roll < 0.45f)
            {
                float cv = rng.RandfRange(-0.04f, 0.04f);
                SpawnGrassTuft(wp, new Color(0.22f + cv, 0.38f + cv, 0.15f + cv),
                               scale, rng.RandfRange(0f, 360f));
            }
            else if (roll < 0.70f)
            {
                Color[] caps = { new(0.50f, 0.70f, 0.30f), new(0.40f, 0.55f, 0.25f) };
                SpawnMushroom(wp, caps[rng.RandiRange(0, 1)], scale);
            }
            else
                DetailDisc(wp, new Color(0.28f, 0.35f, 0.18f), 0.30f * scale, 0.04f);
        }

        private void SpawnVolcanoDetail(Vector3 wp, RandomNumberGenerator rng)
        {
            float roll = rng.Randf(), scale = rng.RandfRange(0.7f, 1.3f);
            if (roll < 0.60f)
                SmallPebble(wp, new Color(0.18f + rng.RandfRange(-0.04f, 0.04f),
                                           0.12f, 0.10f), scale);
            else
                DetailDisc(wp, new Color(0.45f, 0.18f, 0.08f), 0.25f * scale, 0.03f);
        }

        // ── 低级装饰辅助 ──────────────────────────────────────────────

        private void SpawnGrassTuft(Vector3 wp, Color color, float scale, float yRot)
        {
            var m = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.22f * scale, Height = 0.45f * scale },
                Position = wp + new Vector3(0f, 0.2f * scale, 0f),
                RotationDegrees  = new Vector3(0f, yRot, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(m);
        }

        private void SpawnWildflower(Vector3 wp, Color petalColor, float scale)
        {
            var stem = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.02f * scale, BottomRadius = 0.03f * scale, Height = 0.35f * scale },
                Position = wp + new Vector3(0f, 0.175f * scale, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.55f, 0.15f) },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            var head = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.10f * scale, Height = 0.20f * scale },
                Position = wp + new Vector3(0f, 0.40f * scale, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = petalColor },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(stem); AddChild(head);
        }

        private void SpawnMushroom(Vector3 wp, Color capColor, float scale)
        {
            var stalk = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = 0.08f * scale, BottomRadius = 0.10f * scale, Height = 0.28f * scale },
                Position = wp + new Vector3(0f, 0.14f * scale, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.90f, 0.88f, 0.82f) },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            var cap = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.20f * scale, Height = 0.22f * scale },
                Position = wp + new Vector3(0f, 0.36f * scale, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = capColor },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(stalk); AddChild(cap);
        }

        private void DetailDisc(Vector3 wp, Color color, float radius, float height)
        {
            var m = new MeshInstance3D
            {
                Mesh     = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height },
                Position = wp + new Vector3(0f, height * 0.5f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(m);
        }

        private void SmallPebble(Vector3 wp, Color color, float scale)
        {
            var m = new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.10f * scale, Height = 0.14f * scale },
                Position = wp + new Vector3(0f, 0.07f * scale, 0f),
                RotationDegrees  = new Vector3(0f, (float)GD.RandRange(0f, 360f), 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(m);
        }
    }
}
