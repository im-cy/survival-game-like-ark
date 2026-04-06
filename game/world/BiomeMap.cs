using Godot;

namespace SurvivalGame.World
{
    public enum BiomeType { Forest, Grassland, Desert, Snow, Lava, DeepForest, Water }

    /// <summary>
    /// 群系分布图 — 用两层噪声（温度 + 湿度）确定世界各点的群系类型。
    /// </summary>
    public class BiomeMap
    {
        private readonly FastNoiseLite _tempNoise;
        private readonly FastNoiseLite _humidNoise;

        private static readonly float[] BiomeTemperatures = new[]
        {
            18f,   // Forest
            22f,   // Grassland
            40f,   // Desert
            -5f,   // Snow
            60f,   // Lava
            15f,   // DeepForest
            10f,   // Water
        };

        public BiomeMap(int seed)
        {
            _tempNoise = new FastNoiseLite();
            _tempNoise.Seed = seed;
            _tempNoise.Frequency = 0.003f;

            _humidNoise = new FastNoiseLite();
            _humidNoise.Seed = seed + 1;
            _humidNoise.Frequency = 0.005f;
        }

        public BiomeType GetBiome(Vector3 worldPos)
        {
            float t = _tempNoise.GetNoise2D(worldPos.X, worldPos.Z);   // -1..1
            float h = _humidNoise.GetNoise2D(worldPos.X, worldPos.Z);  // -1..1

            if (h < -0.3f) return BiomeType.Water;
            if (t > 0.6f && h < 0.1f) return BiomeType.Lava;
            if (t > 0.4f) return BiomeType.Desert;
            if (t < -0.5f) return BiomeType.Snow;
            if (h > 0.4f && t < -0.1f) return BiomeType.DeepForest;
            if (h > 0.2f) return BiomeType.Forest;
            return BiomeType.Grassland;
        }

        public float GetTemperature(Vector3 worldPos)
            => BiomeTemperatures[(int)GetBiome(worldPos)];
    }
}
