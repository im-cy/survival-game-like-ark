using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Systems;

namespace SurvivalGame.UI
{
    /// <summary>HUD — 监听 ECS 数据变化，更新状态条与时间标签。</summary>
    public partial class HUD : CanvasLayer
    {
        private ProgressBar _hungerBar  = null!;
        private ProgressBar _thirstBar  = null!;
        private ProgressBar _healthBar  = null!;
        private ProgressBar _staminaBar = null!;
        private Label _dayLabel = null!;

        private int _playerEntityId = -1;

        public override void _Ready()
        {
            _hungerBar  = GetNode<ProgressBar>("StatusBars/HungerBar");
            _thirstBar  = GetNode<ProgressBar>("StatusBars/ThirstBar");
            _healthBar  = GetNode<ProgressBar>("StatusBars/HealthBar");
            _staminaBar = GetNode<ProgressBar>("StatusBars/StaminaBar");
            _dayLabel   = GetNode<Label>("DayNightLabel");

            EventBus.Instance.Subscribe("time_updated", OnTimeUpdated);
        }

        public override void _Process(double delta)
        {
            // 找玩家实体（懒加载）
            if (_playerEntityId < 0)
            {
                foreach (var id in World.Instance.Query<SurvivalComponent>())
                { _playerEntityId = id; break; }
                return;
            }

            var s = World.Instance.GetComponent<SurvivalComponent>(_playerEntityId);
            var h = World.Instance.GetComponent<HealthComponent>(_playerEntityId);
            if (s == null || h == null) return;

            _hungerBar.Value  = s.Hunger;
            _thirstBar.Value  = s.Thirst;
            _healthBar.Value  = h.CurrentHp / h.MaxHp * 100f;
            _staminaBar.Value = s.Stamina;
        }

        private void OnTimeUpdated(object? payload)
        {
            if (payload is not float t) return;
            var dn = DayNightSystem.Instance;
            if (dn == null) return;

            float hours = t * 24f;
            int h = (int)hours;
            int m = (int)((hours - h) * 60f);
            _dayLabel.Text = $"Day {dn.DayCount}  {h:D2}:{m:D2}";
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("time_updated", OnTimeUpdated);
        }
    }
}
