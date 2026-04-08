using Godot;
using SurvivalGame.Core.Systems;

namespace SurvivalGame.World
{
    /// <summary>
    /// 昼夜视觉控制器 — 每帧根据 DayNightSystem.TimeOfDay 更新：
    ///   · DirectionalLight3D 旋转角度（太阳位置）
    ///   · 光照能量与颜色（日出橙 → 正午白 → 日落橙 → 夜间蓝）
    ///   · WorldEnvironment 程序天空（ProceduralSkyMaterial）
    ///
    /// 挂载位置：Main/Environment/DayNightController
    /// 兄弟节点：DirectionalLight（日光）、WorldEnvironment（环境）
    /// </summary>
    public partial class DayNightController : Node3D
    {
        private DirectionalLight3D? _sun;
        private WorldEnvironment?   _worldEnv;

        public override void _Ready()
        {
            var env = GetParent();
            _sun      = env.GetNodeOrNull<DirectionalLight3D>("DirectionalLight");
            _worldEnv = env.GetNodeOrNull<WorldEnvironment>("WorldEnvironment");

            SetupSky();

            if (_sun != null)
                _sun.ShadowEnabled = true;
        }

        // ── 天空初始化 ────────────────────────────────────────────────

        private void SetupSky()
        {
            if (_worldEnv == null) return;

            var skyMat = new ProceduralSkyMaterial();
            // 日间天空色
            skyMat.SkyTopColor     = new Color(0.08f, 0.25f, 0.70f);
            skyMat.SkyHorizonColor = new Color(0.75f, 0.88f, 1.00f);
            skyMat.SkyCurve        = 0.09f;
            // 地面色（从下往上看）
            skyMat.GroundBottomColor   = new Color(0.28f, 0.28f, 0.28f);
            skyMat.GroundHorizonColor  = new Color(0.55f, 0.55f, 0.55f);
            skyMat.GroundCurve         = 0.02f;
            // 太阳光晕（与 DirectionalLight 联动）
            skyMat.SunAngleMax = 30f;

            var godotEnv = new Environment();
            godotEnv.BackgroundMode = Environment.BGMode.Sky;
            godotEnv.Sky            = new Sky { SkyMaterial = skyMat };
            // 环境光来自天空，随天色变化自动调整室外亮度
            godotEnv.AmbientLightSource          = Environment.AmbientSource.Sky;
            godotEnv.AmbientLightSkyContribution = 0.5f;
            godotEnv.AmbientLightEnergy          = 0.4f;

            _worldEnv.Environment = godotEnv;
        }

        // ── 每帧更新 ──────────────────────────────────────────────────

        public override void _Process(double delta)
        {
            var dn = DayNightSystem.Instance;
            if (dn == null || _sun == null) return;

            float t = dn.TimeOfDay;

            // 太阳仰角（绕 X 轴）：
            //   t=0.00 → rx= 90° （午夜，地平线以下）
            //   t=0.25 → rx=  0° （日出，地平线）
            //   t=0.50 → rx=-90° （正午，头顶）
            //   t=0.75 → rx=  0° （日落，地平线）
            float rx = Mathf.Sin((t + 0.25f) * Mathf.Tau) * 90f;
            _sun.RotationDegrees = new Vector3(rx, 30f, 0f);

            // 白天因子：正午=1，午夜=0
            float day = Mathf.Clamp(-Mathf.Sin((t + 0.25f) * Mathf.Tau), 0f, 1f);

            // 光照能量
            _sun.LightEnergy = Mathf.Lerp(0.04f, 1.2f, day);

            // 光照颜色：日出/日落偏橙红，正午近白，夜间蓝白（月光）
            if (day > 0f)
            {
                // 白天：dawn/dusk 橙，noon 白
                float warmth   = 1f - Mathf.Pow(day, 2f);   // 0 at noon, 1 at horizon
                _sun.LightColor = new Color(
                    1f,
                    Mathf.Lerp(0.95f, 0.58f, warmth),
                    Mathf.Lerp(0.85f, 0.22f, warmth)
                );
            }
            else
            {
                // 夜晚：冷蓝月光
                _sun.LightColor = new Color(0.55f, 0.65f, 1.0f);
            }
        }
    }
}
