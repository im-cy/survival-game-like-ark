using Godot;

namespace SurvivalGame.World
{
    /// <summary>
    /// M7：六个群系 — 森林、草原、沙漠、雪原、沼泽、火山。
    /// 用两层独立 Perlin 噪声（温度 + 湿度）交叉决定群系分布。
    /// </summary>
    public enum BiomeType { Forest, Grassland, Desert, Snow, Swamp, Volcano }

    public class BiomeMap
    {
        public static BiomeMap? Instance { get; private set; }

        private readonly FastNoiseLite _tempNoise;
        private readonly FastNoiseLite _moistNoise;

        // 对应 BiomeType 枚举顺序的环境基础温度（°C）
        private static readonly float[] BiomeTemperatures =
        {
            18f,   // Forest   森林
            22f,   // Grassland 草原
            38f,   // Desert   沙漠
            -8f,   // Snow     雪原
            26f,   // Swamp    沼泽
            55f,   // Volcano  火山
        };

        public BiomeMap(int seed)
        {
            Instance = this;

            _tempNoise = new FastNoiseLite();
            _tempNoise.Seed      = seed;
            _tempNoise.Frequency = 0.004f;
            _tempNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;

            _moistNoise = new FastNoiseLite();
            _moistNoise.Seed      = seed + 137;
            _moistNoise.Frequency = 0.005f;
            _moistNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        }

        /// <summary>
        /// 温度/湿度双轴映射：
        ///   temp &lt; -0.30               → 雪原
        ///   temp &gt;  0.50               → 火山
        ///   temp &gt;  0.15 且 moist &lt; -0.20 → 沙漠
        ///   moist &gt;  0.35              → 沼泽
        ///   moist &gt;  0.00              → 森林
        ///   其余                         → 草原
        /// </summary>
        public BiomeType GetBiome(Vector3 worldPos)
        {
            float temp  = _tempNoise.GetNoise2D(worldPos.X, worldPos.Z);
            float moist = _moistNoise.GetNoise2D(worldPos.X, worldPos.Z);

            if (temp  < -0.30f)                    return BiomeType.Snow;
            if (temp  >  0.50f)                    return BiomeType.Volcano;
            if (temp  >  0.15f && moist < -0.20f)  return BiomeType.Desert;
            if (moist >  0.35f)                    return BiomeType.Swamp;
            if (moist >  0.00f)                    return BiomeType.Forest;
            return BiomeType.Grassland;
        }

        public float GetTemperature(Vector3 worldPos)
            => BiomeTemperatures[(int)GetBiome(worldPos)];
    }
}
