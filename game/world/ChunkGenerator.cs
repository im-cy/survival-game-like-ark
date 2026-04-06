using Godot;

namespace SurvivalGame.World
{
    /// <summary>
    /// 程序化 Chunk 生成器 — 用噪声生成高度/群系，再叠加资源/生物。
    /// </summary>
    public class ChunkGenerator
    {
        public static ChunkGenerator? Instance { get; private set; }

        private readonly FastNoiseLite _heightNoise;
        private readonly FastNoiseLite _detailNoise;
        private readonly BiomeMap _biomeMap;
        private readonly RandomNumberGenerator _rng = new();

        public ChunkGenerator(int seed, BiomeMap biomeMap)
        {
            Instance = this;
            _biomeMap = biomeMap;

            _heightNoise = new FastNoiseLite();
            _heightNoise.Seed = seed + 2;
            _heightNoise.Frequency = 0.01f;

            _detailNoise = new FastNoiseLite();
            _detailNoise.Seed = seed + 3;
            _detailNoise.Frequency = 0.05f;

            _rng.Seed = (ulong)seed;
        }

        public ChunkData Generate(Vector2I coord)
        {
            var chunk = new ChunkData(coord);
            var origin = new Vector2(coord.X * ChunkData.Size, coord.Y * ChunkData.Size);

            for (int x = 0; x < ChunkData.Size; x++)
            for (int z = 0; z < ChunkData.Size; z++)
            {
                float wx = origin.X + x;
                float wz = origin.Y + z;

                var worldPos = new Vector3(wx, 0f, wz);
                var biome = _biomeMap.GetBiome(worldPos);
                var tile  = BiomeToTile(biome, _heightNoise.GetNoise2D(wx, wz));

                chunk.SetTile(x, z, tile, biome);
            }

            return chunk;
        }

        private static TileType BiomeToTile(BiomeType biome, float height) => biome switch
        {
            BiomeType.Desert    => height > 0.3f ? TileType.Stone : TileType.Sand,
            BiomeType.Snow      => TileType.Snow,
            BiomeType.Lava      => height > 0.5f ? TileType.Stone : TileType.Lava,
            BiomeType.Water     => TileType.Water,
            BiomeType.DeepForest=> height > 0.2f ? TileType.DeepGrass : TileType.Dirt,
            BiomeType.Forest    => height > 0.1f ? TileType.Grass : TileType.Dirt,
            _                   => TileType.Grass,
        };
    }
}
