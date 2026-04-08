using Godot;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Core.Systems
{
    /// <summary>
    /// 昼夜循环系统 — 驱动天空光照、环境温度、危险度。
    /// 一天 = DayDurationSeconds 秒真实时间（默认 20 分钟）
    /// </summary>
    public class DayNightSystem : SystemBase
    {
        public static DayNightSystem? Instance { get; private set; }

        public float DayDurationSeconds = 300f;   // 5 分钟一天（测试用，可按需调大）

        public float TimeOfDay { get; private set; } = 0.25f;  // 0=午夜, 0.25=日出, 0.5=正午, 0.75=日落
        public int DayCount { get; private set; } = 1;

        // 供 SurvivalSystem 调用
        public float GetTemperatureModifier()
        {
            // 正午最热 +5，午夜最冷 -8
            float t = TimeOfDay;
            if (t < 0.5f) return Mathf.Lerp(-8f, 5f, t * 2f);
            return Mathf.Lerp(5f, -8f, (t - 0.5f) * 2f);
        }

        public bool IsNight => TimeOfDay < 0.2f || TimeOfDay > 0.8f;
        public bool IsDusk  => TimeOfDay is >= 0.7f and <= 0.8f;

        public override void Initialize() => Instance = this;

        public override void Tick(float delta)
        {
            TimeOfDay += delta / DayDurationSeconds;
            if (TimeOfDay >= 1f)
            {
                TimeOfDay -= 1f;
                DayCount++;
                EventBus.Instance.Emit("new_day", DayCount);
            }

            UpdateEnvironmentLight();
        }

        private void UpdateEnvironmentLight()
        {
            // TODO: 通过 Godot DirectionalLight3D 控制太阳角度与颜色
            // 这里只做数据层，视图层由 Main 场景的 WorldEnvironment 节点负责监听
            EventBus.Instance.Emit("time_updated", TimeOfDay);
        }
    }
}
