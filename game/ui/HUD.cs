using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Systems;

namespace SurvivalGame.UI
{
    /// <summary>HUD — 状态条（生命/饥饿/口渴/体力）、时间、背包、驯养状态。</summary>
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
        private Label _hintLabel      = null!;
        private Label _companionLabel = null!;

        private int _playerEntityId = -1;

        // Boss 血条
        private PanelContainer _bossPanel = null!;
        private ProgressBar    _bossBar   = null!;
        private Label          _bossLabel = null!;

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
            BuildBossPanel();
            EventBus.Instance.Subscribe("time_updated", OnTimeUpdated);
        }

        // ── 动态构建状态面板 ──────────────────────────────────────────────

        private void BuildStatusPanel()
        {
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

            // ── 体温行 ────────────────────────────────────────────────
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

            // ── 伴侣状态行 ────────────────────────────────────────────
            _companionLabel = new Label { Text = "" };
            _companionLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.4f));
            _companionLabel.AddThemeConstantOverride("font_size", 11);
            vbox.AddChild(_companionLabel);

            // ── 操作提示行 ────────────────────────────────────────────
            _hintLabel = new Label { Text = "左键=攻击  E=采集/喂食  F=食用  B=建造  C=制作  I=背包  H=指令  V=进化  R=骑乘/下马" };
            _hintLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
            _hintLabel.AddThemeConstantOverride("font_size", 11);
            vbox.AddChild(_hintLabel);
        }

        // ── Boss 血条面板 ─────────────────────────────────────────────────

        private void BuildBossPanel()
        {
            _bossPanel = new PanelContainer();
            _bossPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
            _bossPanel.OffsetTop    = 10f;
            _bossPanel.OffsetBottom = 60f;
            _bossPanel.OffsetLeft   = -200f;
            _bossPanel.OffsetRight  =  200f;
            _bossPanel.Visible = false;
            AddChild(_bossPanel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            vbox.CustomMinimumSize = new Vector2(380f, 0f);
            _bossPanel.AddChild(vbox);

            _bossLabel = new Label { Text = "森林守卫者" };
            _bossLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _bossLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.25f, 0.25f));
            _bossLabel.AddThemeConstantOverride("font_size", 14);
            vbox.AddChild(_bossLabel);

            _bossBar = new ProgressBar();
            _bossBar.MaxValue = 100;
            _bossBar.Value    = 100;
            _bossBar.ShowPercentage = false;
            _bossBar.CustomMinimumSize = new Vector2(0f, 20f);
            ApplyBarColor(_bossBar, new Color(0.85f, 0.15f, 0.15f));
            vbox.AddChild(_bossBar);
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

            // 体温显示（水中显示蓝色）
            float temp = s.Temperature;
            var playerPos = EcsWorld.Instance.GetComponent<PositionComponent>(_playerEntityId);
            bool inWater  = playerPos != null && playerPos.Position.Y < World.FiniteWorldMap.WaterLevel + 0.15f;
            if (inWater)
            {
                _tempLabel.Text = $"{temp:F1}°C  🌊";
                _tempLabel.AddThemeColorOverride("font_color", new Color(0.30f, 0.70f, 1f));
            }
            else
            {
                _tempLabel.Text = $"{temp:F1}°C";
                Color tempColor;
                if (temp < 10f || temp > 50f)
                    tempColor = new Color(1f, 0.25f, 0.25f);
                else if (temp < 20f || temp > 42f)
                    tempColor = new Color(1f, 0.75f, 0.2f);
                else
                    tempColor = new Color(0.5f, 1f, 0.5f);
                _tempLabel.AddThemeColorOverride("font_color", tempColor);
            }

            // 伴侣状态（驯服进度 / 已驯服指令状态）
            UpdateCompanionStatus();

            // Boss 血条（附近有 Boss 时显示）
            UpdateBossBar();

            var inv   = EcsWorld.Instance.GetComponent<InventoryComponent>(_playerEntityId);
            var equip = EcsWorld.Instance.GetComponent<EquipmentComponent>(_playerEntityId);
            if (inv != null)
            {
                var sb = new System.Text.StringBuilder("🎒 背包\n");
                foreach (var item in inv.Items)
                {
                    var def = Core.Data.ItemRegistry.Instance.Get(item.ItemId);
                    string name = def?.DisplayName ?? item.ItemId;
                    sb.AppendLine($"  {name}  ×{item.Quantity}");
                }
                if (inv.Items.Count == 0) sb.Append("  （空）");

                // 装备栏摘要
                if (equip != null && (equip.WeaponId != null || equip.ChestId != null))
                {
                    sb.AppendLine("⚙ 已装备");
                    if (equip.WeaponId != null)
                    {
                        var wDef = Core.Data.ItemRegistry.Instance.Get(equip.WeaponId);
                        sb.AppendLine($"  ⚔ {wDef?.DisplayName ?? equip.WeaponId}");
                    }
                    if (equip.ChestId != null)
                    {
                        var aDef = Core.Data.ItemRegistry.Instance.Get(equip.ChestId);
                        sb.AppendLine($"  🛡 {aDef?.DisplayName ?? equip.ChestId}");
                    }
                }

                _inventoryLabel.Text = sb.ToString().TrimEnd();
            }
        }

        private void UpdateBossBar()
        {
            // 查找已进入仇恨状态（Alert 或 Hostile）的 Boss
            foreach (var id in EcsWorld.Instance.Query<HealthComponent, CreatureStatsComponent, AIComponent>())
            {
                var stats = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(id)!;
                var def   = Core.Data.CreatureRegistry.Instance.Get(stats.SpeciesId);
                if (def?.Tier != Core.Data.CreatureTier.Boss) continue;

                var ai = EcsWorld.Instance.GetComponent<AIComponent>(id)!;
                // 只有进入 Alert 或 Hostile 状态（即仇恨了玩家）时才显示
                bool aggrod = ai.CurrentState == FSMState.Alert
                           || ai.CurrentState == FSMState.Hostile;
                if (!aggrod) continue;

                var hp = EcsWorld.Instance.GetComponent<HealthComponent>(id)!;
                _bossPanel.Visible = true;
                _bossLabel.Text    = $"⚔ {def.DisplayName}  {(int)hp.CurrentHp} / {(int)hp.MaxHp}";
                _bossBar.Value     = hp.CurrentHp / hp.MaxHp * 100f;
                return;
            }

            _bossPanel.Visible = false;
        }

        private void UpdateCompanionStatus()
        {
            // 先查找驯养中的生物（显示驯养进度）
            foreach (var id in EcsWorld.Instance.Query<TamingComponent, PositionComponent>())
            {
                var taming = EcsWorld.Instance.GetComponent<TamingComponent>(id)!;
                // 仅在玩家已主动喂食后（Bonding）才显示进度，Cautious 阶段由悬停面板负责
                if (taming.State == TamingState.Bonding)
                {
                    _companionLabel.Text = $"🐾 驯养中 {taming.TrustProgress:F0}%";
                    return;
                }
            }

            // 查找已驯服的伴侣（显示当前指令）
            foreach (var id in EcsWorld.Instance.Query<TamingComponent, CreatureStatsComponent>())
            {
                var taming = EcsWorld.Instance.GetComponent<TamingComponent>(id)!;
                var stats  = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(id)!;
                if (taming.State != TamingState.Tamed) continue;
                if (stats.OwnerId != _playerEntityId) continue;

                var def = Core.Data.CreatureRegistry.Instance.Get(stats.SpeciesId);
                string name = def?.DisplayName ?? stats.SpeciesId;
                // 骑乘中时显示特殊状态
                var playerSurvival = EcsWorld.Instance.GetComponent<SurvivalComponent>(_playerEntityId);
                bool isMounted = playerSurvival?.RidingEntityId == id;
                string order = isMounted ? "骑乘中" : stats.CurrentOrder switch
                {
                    AIBehaviorOrder.Follow  => "跟随",
                    AIBehaviorOrder.Harvest => "采集中",
                    AIBehaviorOrder.Guard   => "守卫",
                    _                       => "待机"
                };
                _companionLabel.Text = $"🐗 {name}  [{order}]  忠诚:{stats.Loyalty:F0}";
                return;
            }

            _companionLabel.Text = "";
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
