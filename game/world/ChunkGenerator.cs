using Godot;

namespace SurvivalGame.World
{
    /// <summary>
    /// 程序化 Chunk 生成器 — 按群系填充 TileType 数据。
    /// </summary>
    public class ChunkGenerator
    {
        public static ChunkGenerator? Instance { get; private set; }

        private readonly BiomeMap _biomeMap;

        public ChunkGenerator(int seed, BiomeMap biomeMap)
        {
            Instance  = this;
            _biomeMap = biomeMap;
        }

        public ChunkData Generate(Vector2I coord)
        {
            var chunk  = new ChunkData(coord);
            var origin = new Vector2(coord.X * ChunkData.Size, coord.Y * ChunkData.Size);

            for (int x = 0; x < ChunkData.Size; x++)
            for (int z = 0; z < ChunkData.Size; z++)
            {
                float wx   = origin.X + x;
                float wz   = origin.Y + z;
                var biome  = _biomeMap.GetBiome(new Vector3(wx, 0f, wz));
                var tile   = (biome == BiomeType.Forest || biome == BiomeType.Swamp)
                    ? TileType.DeepGrass : TileType.Grass;
                chunk.SetTile(x, z, tile, biome);
            }

            return chunk;
        }
    }
}
