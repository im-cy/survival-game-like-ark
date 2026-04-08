using Godot;

namespace SurvivalGame.World
{
    /// <summary>
    /// M5：两个群系 — 森林（Forest）和 草原（Grassland）。
    /// 用单层 Perlin 噪声决定分布，正值为森林，负值为草原。
    /// </summary>
    public enum BiomeType { Forest, Grassland }

    public class BiomeMap
    {
        private readonly FastNoiseLite _noise;

        private static readonly float[] BiomeTemperatures = { 18f, 22f };

        public BiomeMap(int seed)
        {
            _noise = new FastNoiseLite();
            _noise.Seed      = seed;
            _noise.Frequency = 0.004f;   // 大块群系，约 250m 一个过渡
            _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        }

        public BiomeType GetBiome(Vector3 worldPos)
        {
            float n = _noise.GetNoise2D(worldPos.X, worldPos.Z);
            return n > 0f ? BiomeType.Forest : BiomeType.Grassland;
        }

        public float GetTemperature(Vector3 worldPos)
            => BiomeTemperatures[(int)GetBiome(worldPos)];
    }
}
