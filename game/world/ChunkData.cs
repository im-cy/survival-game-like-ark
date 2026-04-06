using Godot;
using System.Collections.Generic;

namespace SurvivalGame.World
{
    public enum TileType { Grass, Dirt, Stone, Sand, Snow, Lava, Water, DeepGrass }

    public class TileData
    {
        public TileType Tile;
        public BiomeType Biome;
    }

    public class ChunkData
    {
        public const int Size = 32;
        public Vector2I Coord { get; }

        private readonly TileData[,] _tiles = new TileData[Size, Size];
        public List<int> EntityIds { get; } = new();  // 该 Chunk 内的实体

        public ChunkData(Vector2I coord) => Coord = coord;

        public void SetTile(int x, int z, TileType tile, BiomeType biome)
            => _tiles[x, z] = new TileData { Tile = tile, Biome = biome };

        public TileData GetTile(int x, int z) => _tiles[x, z];

        public Vector3 ChunkOriginWorld => new Vector3(Coord.X * Size, 0f, Coord.Y * Size);
    }
}
