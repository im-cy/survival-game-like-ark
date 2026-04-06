using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Systems;

namespace SurvivalGame.UI
{
    /// <summary>HUD — 监听 ECS 数据变化，更新状态条、时间标签与背包显示。</summary>
    public partial class HUD : CanvasLayer
    {
        private ProgressBar _hungerBar       = null!;
        private ProgressBar _thirstBar       = null!;
        private ProgressBar _healthBar       = null!;
        private ProgressBar _staminaBar      = null!;
        private Label       _dayLabel        = null!;
        private Label       _inventoryLabel  = null!;

        private int _playerEntityId = -1;

        public override void _Ready()
        {
            _hungerBar      = GetNode<ProgressBar>("StatusBars/HungerBar");
            _thirstBar      = GetNode<ProgressBar>("StatusBars/ThirstBar");
            _healthBar      = GetNode<ProgressBar>("StatusBars/HealthBar");
            _staminaBar     = GetNode<ProgressBar>("StatusBars/StaminaBar");
            _dayLabel       = GetNode<Label>("DayNightLabel");
            _inventoryLabel = GetNode<Label>("InventoryPanel/InventoryLabel");

            EventBus.Instance.Subscribe("time_updated", OnTimeUpdated);
        }

        public override void _Process(double delta)
        {
            // 找玩家实体（懒加载）
            if (_playerEntityId < 0)
            {
                foreach (var id in EcsWorld.Instance.Query<SurvivalComponent>())
                { _playerEntityId = id; break; }
                return;
            }

            var s = EcsWorld.Instance.GetComponent<SurvivalComponent>(_playerEntityId);
            var h = EcsWorld.Instance.GetComponent<HealthComponent>(_playerEntityId);
            if (s == null || h == null) return;

            _hungerBar.Value  = s.Hunger;
            _thirstBar.Value  = s.Thirst;
            _healthBar.Value  = h.CurrentHp / h.MaxHp * 100f;
            _staminaBar.Value = s.Stamina;

            // 背包显示
            var inv = EcsWorld.Instance.GetComponent<InventoryComponent>(_playerEntityId);
            if (inv != null)
            {
                var sb = new System.Text.StringBuilder("背包\n");
                foreach (var item in inv.Items)
                    sb.AppendLine($"{item.ItemId}  x{item.Quantity}");
                if (inv.Items.Count == 0) sb.Append("（空）");
                _inventoryLabel.Text = sb.ToString().TrimEnd();
            }
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
