using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.World;

namespace SurvivalGame.Core.Systems
{
    /// <summary>
    /// 生存系统 — 处理饥饿、口渴、体温、耐力的被动消耗与伤害。
    /// </summary>
    public class SurvivalSystem : SystemBase
    {
        private const float TempDamagePerSec = 2f;

        public override void Tick(float delta)
        {
            foreach (var entityId in EcsWorld.Instance.Query<SurvivalComponent, HealthComponent>())
            {
                var s = EcsWorld.Instance.GetComponent<SurvivalComponent>(entityId)!;
                var h = EcsWorld.Instance.GetComponent<HealthComponent>(entityId)!;

                // ── 饥饿/口渴消耗 ────────────────────────────────────────
                float hungerDrain = s.HungerDrainRate;
                if (s.IsSprinting) hungerDrain *= 1.5f;
                s.Hunger = Mathf.Max(0f, s.Hunger - hungerDrain * delta);
                s.Thirst = Mathf.Max(0f, s.Thirst - s.ThirstDrainRate * delta);

                // ── 归零扣血 ─────────────────────────────────────────────
                if (s.Hunger <= 0f) h.TakeDamage(1f * delta);
                if (s.Thirst <= 0f) h.TakeDamage(2f * delta);

                // ── 体温伤害 ─────────────────────────────────────────────
                if (s.Temperature < 30f || s.Temperature > 42f)
                    h.TakeDamage(TempDamagePerSec * delta);

                // ── 体温更新 ─────────────────────────────────────────────
                UpdateTemperature(entityId, s, delta);

                // ── 耐力 ─────────────────────────────────────────────────
                if (s.IsSprinting)
                    s.Stamina = Mathf.Max(0f, s.Stamina - s.StaminaDrainRate * delta);
                else
                    s.Stamina = Mathf.Min(100f, s.Stamina + s.StaminaRegenRate * delta);
            }
        }

        private void UpdateTemperature(int entityId, SurvivalComponent s, float delta)
        {
            var pos = EcsWorld.Instance.GetComponent<PositionComponent>(entityId);
            float biomeTemp = WorldManager.Instance?.GetBiomeTemperature(pos?.Position ?? Vector3.Zero) ?? 22f;
            float timeTemp  = DayNightSystem.Instance?.GetTemperatureModifier() ?? 0f;
            float target    = biomeTemp + timeTemp;
            s.Temperature   = Mathf.Lerp(s.Temperature, target, 0.05f * delta);
        }

        // ── 外部调用 ─────────────────────────────────────────────────────

        public void EatFood(int entityId, float hungerRestore, float thirstRestore = 0f)
        {
            var s = EcsWorld.Instance.GetComponent<SurvivalComponent>(entityId);
            if (s == null) return;
            s.Hunger = Mathf.Min(100f, s.Hunger + hungerRestore);
            s.Thirst = Mathf.Min(100f, s.Thirst + thirstRestore);
        }
    }
}
