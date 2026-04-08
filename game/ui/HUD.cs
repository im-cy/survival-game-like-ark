using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Systems;

namespace SurvivalGame.UI
{
    /// <summary>HUD — 状态条（生命/饥饿/口渴/体力）、时间、背包。</summary>
    public partial class HUD : CanvasLayer
    {
        // 状态条引用（代码生成）
        private ProgressBar _healthBar  = null!;
        private ProgressBar _hungerBar  = null!;
        private ProgressBar _thirstBar  = null!;
        private ProgressBar _staminaBar = null!;
        private Label _healthVal  = null!;
        private Label _hungerVal  = null!;
        private Label _thirstVal  = null!;
        private Label _staminaVal = null!;

        private Label _dayLabel       = null!;
        private Label _inventoryLabel = null!;
        private Label _tempLabel      = null!;

        private int _playerEntityId = -1;

        private static readonly (string icon, string name, Color color)[] BarDefs =
        {
            ("❤", "生命",  new Color(0.87f, 0.20f, 0.20f)),
            ("🍖", "饥饿", new Color(0.90f, 0.50f, 0.10f)),
            ("💧", "口渴", new Color(0.20f, 0.60f, 0.90f)),
            ("⚡", "体力", new Color(0.95f, 0.80f, 0.10f)),
        };

        public override void _Ready()
        {
            _dayLabel       = GetNode<Label>("DayNightLabel");
            _inventoryLabel = GetNode<Label>("InventoryPanel/InventoryLabel");

            BuildStatusPanel();
            EventBus.Instance.Subscribe("time_updated", OnTimeUpdated);
        }

        // ── 动态构建状态面板 ──────────────────────────────────────────────

        private void BuildStatusPanel()
        {
            // 外层面板
            var panel = new PanelContainer();
            panel.OffsetLeft = 12;
            panel.OffsetTop  = 12;

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            vbox.CustomMinimumSize = new Vector2(260, 0);
            panel.AddChild(vbox);
            AddChild(panel);

            // 每行：标签 + 进度条 + 数值
            var bars  = new ProgressBar[4];
            var vals  = new Label[4];
            for (int i = 0; i < BarDefs.Length; i++)
            {
                var (icon, name, color) = BarDefs[i];

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);
                row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

                var lbl = new Label();
                lbl.Text = $"{icon} {name}";
                lbl.CustomMinimumSize       = new Vector2(72, 22);
                lbl.VerticalAlignment       = VerticalAlignment.Center;
                row.AddChild(lbl);

                var bar = new ProgressBar();
                bar.MaxValue                = 100;
                bar.Value                   = 100;
                bar.ShowPercentage          = false;
                bar.SizeFlagsHorizontal     = Control.SizeFlags.ExpandFill;
                bar.CustomMinimumSize       = new Vector2(0, 22);
                ApplyBarColor(bar, color);
                row.AddChild(bar);

                var val = new Label();
                val.Text                    = "100";
                val.CustomMinimumSize       = new Vector2(56, 22);
                val.HorizontalAlignment     = HorizontalAlignment.Right;
                val.VerticalAlignment       = VerticalAlignment.Center;
                row.AddChild(val);

                vbox.AddChild(row);
                bars[i] = bar;
                vals[i] = val;
            }

            _healthBar  = bars[0]; _healthVal  = vals[0];
            _hungerBar  = bars[1]; _hungerVal  = vals[1];
            _thirstBar  = bars[2]; _thirstVal  = vals[2];
            _staminaBar = bars[3]; _staminaVal = vals[3];

            // ── 体温行（文字，不用进度条）─────────────────────────────
            var tempRow = new HBoxContainer();
            tempRow.AddThemeConstantOverride("separation", 6);
            tempRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var tempIconLbl = new Label { Text = "🌡 体温" };
            tempIconLbl.CustomMinimumSize = new Vector2(72, 22);
            tempIconLbl.VerticalAlignment = VerticalAlignment.Center;
            tempRow.AddChild(tempIconLbl);

            _tempLabel = new Label { Text = "37.0°C" };
            _tempLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _tempLabel.VerticalAlignment   = VerticalAlignment.Center;
            tempRow.AddChild(_tempLabel);

            vbox.AddChild(tempRow);

            // ── 操作提示行 ───────────────────────────────────────────
            var hintLbl = new Label { Text = "E=采集  F=食用  B=建造" };
            hintLbl.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
            hintLbl.AddThemeConstantOverride("font_size", 11);
            vbox.AddChild(hintLbl);
        }

        // ── 每帧更新 ──────────────────────────────────────────────────────

        public override void _Process(double delta)
        {
            if (_playerEntityId < 0)
            {
                foreach (var id in EcsWorld.Instance.Query<SurvivalComponent>())
                { _playerEntityId = id; break; }
                return;
            }

            var s = EcsWorld.Instance.GetComponent<SurvivalComponent>(_playerEntityId);
            var h = EcsWorld.Instance.GetComponent<HealthComponent>(_playerEntityId);
            if (s == null || h == null) return;

            float hpPct = h.CurrentHp / h.MaxHp * 100f;
            SetBar(_healthBar,  _healthVal,  hpPct,    $"{(int)h.CurrentHp}/{(int)h.MaxHp}");
            SetBar(_hungerBar,  _hungerVal,  s.Hunger,  $"{(int)s.Hunger}");
            SetBar(_thirstBar,  _thirstVal,  s.Thirst,  $"{(int)s.Thirst}");
            SetBar(_staminaBar, _staminaVal, s.Stamina, $"{(int)s.Stamina}");

            // 体温显示（颜色：正常=绿，偏冷=蓝，过冷/热=红）
            float temp = s.Temperature;
            _tempLabel.Text = $"{temp:F1}°C";
            Color tempColor;
            if (temp < 10f || temp > 50f)
                tempColor = new Color(1f, 0.25f, 0.25f);   // 危险（红）
            else if (temp < 20f || temp > 42f)
                tempColor = new Color(1f, 0.75f, 0.2f);    // 警告（橙）
            else
                tempColor = new Color(0.5f, 1f, 0.5f);     // 正常（绿）
            _tempLabel.AddThemeColorOverride("font_color", tempColor);

            var inv = EcsWorld.Instance.GetComponent<InventoryComponent>(_playerEntityId);
            if (inv != null)
            {
                var sb = new System.Text.StringBuilder("🎒 背包\n");
                foreach (var item in inv.Items)
                    sb.AppendLine($"  {item.ItemId}  ×{item.Quantity}");
                if (inv.Items.Count == 0) sb.Append("  （空）");
                _inventoryLabel.Text = sb.ToString().TrimEnd();
            }
        }

        // ── 工具 ──────────────────────────────────────────────────────────

        private static void SetBar(ProgressBar bar, Label val, float pct, string text)
        {
            bar.Value = Mathf.Clamp(pct, 0f, 100f);
            val.Text  = text;
        }

        private static void ApplyBarColor(ProgressBar bar, Color color)
        {
            var fill = new StyleBoxFlat();
            fill.BgColor = color;
            fill.SetCornerRadiusAll(3);
            bar.AddThemeStyleboxOverride("fill", fill);

            var bg = new StyleBoxFlat();
            bg.BgColor = new Color(0.12f, 0.12f, 0.12f, 0.55f);
            bg.SetCornerRadiusAll(3);
            bar.AddThemeStyleboxOverride("background", bg);
        }

        private void OnTimeUpdated(object? payload)
        {
            if (payload is not float t) return;
            var dn = DayNightSystem.Instance;
            if (dn == null) return;
            float hours = t * 24f;
            int hh = (int)hours;
            int mm = (int)((hours - hh) * 60f);
            _dayLabel.Text = $"☀ Day {dn.DayCount}  {hh:D2}:{mm:D2}";
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("time_updated", OnTimeUpdated);
        }
    }
}
