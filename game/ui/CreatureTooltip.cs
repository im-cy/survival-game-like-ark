using Godot;
using SurvivalGame.Core.Data;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.UI
{
    /// <summary>
    /// 生物悬停提示面板 — 鼠标悬停在生物上时显示属性简报。
    /// 屏幕空间投影检测：将生物世界坐标投影到屏幕，判断鼠标距离。
    /// </summary>
    public partial class CreatureTooltip : CanvasLayer
    {
        private const float HoverPixelRadius = 60f;

        private PanelContainer _panel    = null!;
        private Label          _nameLbl  = null!;
        private ProgressBar    _hpBar    = null!;
        private Label          _hpValLbl = null!;
        private ProgressBar    _tamBar   = null!;
        private Label          _tamKeyLbl = null!;
        private Label          _tamValLbl = null!;
        private Label          _stateLbl  = null!;
        private Label          _traitLbl  = null!;

        private Camera3D? _camera;

        public override void _Ready()
        {
            Layer = 10;
            BuildPanel();
        }

        private void BuildPanel()
        {
            _panel = new PanelContainer();
            _panel.CustomMinimumSize = new Vector2(190, 0);
            _panel.Visible = false;

            var style = new StyleBoxFlat();
            style.BgColor     = new Color(0.05f, 0.05f, 0.08f, 0.90f);
            style.BorderColor = new Color(0.50f, 0.50f, 0.55f, 0.65f);
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(4);
            style.SetContentMarginAll(8);
            _panel.AddThemeStyleboxOverride("panel", style);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            _panel.AddChild(vbox);

            // ── 名称 ──────────────────────────────────────────────────
            _nameLbl = new Label();
            _nameLbl.AddThemeConstantOverride("font_size", 13);
            _nameLbl.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.55f));
            vbox.AddChild(_nameLbl);

            // ── HP 行 ─────────────────────────────────────────────────
            var hpRow = MakeBarRow("❤ HP", out _hpBar, out _hpValLbl,
                new Color(0.85f, 0.2f, 0.2f));
            vbox.AddChild(hpRow);

            // ── 驯养/忠诚 行 ──────────────────────────────────────────
            var tamRow = MakeBarRow("驯养", out _tamBar, out _tamValLbl,
                new Color(0.3f, 0.8f, 0.3f));
            // 取出标签引用（第一个子节点就是 key label）
            _tamKeyLbl = (Label)tamRow.GetChild(0);
            vbox.AddChild(tamRow);

            // ── 状态/指令 ─────────────────────────────────────────────
            _stateLbl = new Label();
            _stateLbl.AddThemeConstantOverride("font_size", 11);
            _stateLbl.AddThemeColorOverride("font_color", new Color(0.6f, 0.85f, 1f));
            _stateLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _stateLbl.CustomMinimumSize = new Vector2(174, 0);
            vbox.AddChild(_stateLbl);

            // ── 特性词条 ──────────────────────────────────────────────
            _traitLbl = new Label { Text = "" };
            _traitLbl.AddThemeConstantOverride("font_size", 11);
            _traitLbl.AddThemeColorOverride("font_color", new Color(0.95f, 0.80f, 0.30f)); // 金色
            _traitLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _traitLbl.CustomMinimumSize = new Vector2(174, 0);
            vbox.AddChild(_traitLbl);

            // ── 操作提示 ──────────────────────────────────────────────
            var hintLbl = new Label { Text = "[E] 喂食" };
            hintLbl.AddThemeConstantOverride("font_size", 10);
            hintLbl.AddThemeColorOverride("font_color", new Color(0.45f, 0.45f, 0.45f));
            vbox.AddChild(hintLbl);

            AddChild(_panel);
        }

        // ── 每帧：投影检测 ────────────────────────────────────────────

        public override void _Process(double delta)
        {
            _camera ??= GetViewport().GetCamera3D();
            if (_camera == null) { _panel.Visible = false; return; }

            var mouse = GetViewport().GetMousePosition();

            int hoveredId = -1;
            float closestDist = HoverPixelRadius;

            foreach (var id in EcsWorld.Instance.Query<TamingComponent, PositionComponent>())
            {
                var pos = EcsWorld.Instance.GetComponent<PositionComponent>(id)!;
                var screenPos = _camera.UnprojectPosition(pos.Position + new Vector3(0f, 0.5f, 0f));
                float d = mouse.DistanceTo(screenPos);
                if (d < closestDist) { closestDist = d; hoveredId = id; }
            }

            if (hoveredId < 0)
            {
                _panel.Visible = false;
                return;
            }

            RefreshPanel(hoveredId);
            PositionNearMouse(mouse);
            _panel.Visible = true;
        }

        // ── 面板数据填充 ──────────────────────────────────────────────

        private void RefreshPanel(int entityId)
        {
            var taming = EcsWorld.Instance.GetComponent<TamingComponent>(entityId)!;
            var health = EcsWorld.Instance.GetComponent<HealthComponent>(entityId);
            var stats  = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(entityId);

            // 名称 + 等级
            var def = stats != null ? CreatureRegistry.Instance.Get(stats.SpeciesId) : null;
            string name = def?.DisplayName ?? (stats?.SpeciesId ?? "未知");
            _nameLbl.Text = $"{name}  Lv.{stats?.Level ?? 1}";

            // 特性词条（驯服后显示）
            if (stats != null && stats.Traits.Count > 0)
            {
                var traitNames = new System.Collections.Generic.List<string>();
                foreach (var tid in stats.Traits)
                {
                    var td = TraitRegistry.Instance.Get(tid);
                    traitNames.Add(td?.DisplayName ?? tid);
                }
                _traitLbl.Text = "★ " + string.Join("  ", traitNames);
                _traitLbl.Visible = true;
            }
            else
            {
                _traitLbl.Visible = false;
            }

            // HP
            if (health != null)
            {
                _hpBar.Value = health.CurrentHp / health.MaxHp * 100f;
                _hpValLbl.Text = $"{(int)health.CurrentHp}/{(int)health.MaxHp}";
            }

            // 驯养 / 忠诚度
            if (taming.State == TamingState.Tamed)
            {
                _tamKeyLbl.Text = "忠诚";
                SetBarColor(_tamBar, new Color(0.3f, 0.55f, 1f));
                float loyalty = stats?.Loyalty ?? 100f;
                _tamBar.Value = loyalty;
                _tamValLbl.Text = $"{(int)loyalty}%";

                string order = stats?.CurrentOrder switch
                {
                    AIBehaviorOrder.Follow  => "跟随中",
                    AIBehaviorOrder.Harvest => "采集中",
                    AIBehaviorOrder.Guard   => "守卫中",
                    _                       => "待机"
                };

                // 繁殖状态附加提示
                string breedInfo = "";
                var breeding = EcsWorld.Instance.GetComponent<BreedingComponent>(entityId);
                if (breeding != null)
                {
                    if (breeding.IsPregnant)
                    {
                        int pct = (int)(breeding.PregnancyTimer / breeding.PregnancyDuration * 100f);
                        breedInfo = $"\n孕育中 {pct}%";
                    }
                    else if (breeding.BreedCooldown > 0f)
                    {
                        breedInfo = $"\n繁殖冷却 {(int)breeding.BreedCooldown}s";
                    }
                }

                _stateLbl.Text = $"状态：{order}{breedInfo}\n[H] 切换指令";
            }
            else
            {
                _tamKeyLbl.Text = "驯养";
                SetBarColor(_tamBar, new Color(0.3f, 0.8f, 0.3f));
                _tamBar.Value = taming.TrustProgress;
                _tamValLbl.Text = $"{(int)taming.TrustProgress}%";

                string stateStr = taming.State switch
                {
                    TamingState.Wild     => "野生",
                    TamingState.Cautious => "戒备（可喂食）",
                    TamingState.Bonding  => "建立信任中",
                    TamingState.Sedated  => "已麻醉",
                    _                    => ""
                };
                string food = string.IsNullOrEmpty(taming.PreferredFood)
                    ? "" : $"\n偏好：{GetDisplayName(taming.PreferredFood)}";
                _stateLbl.Text = stateStr + food;
            }
        }

        // ── 定位面板（跟随鼠标，避免越界）────────────────────────────

        private void PositionNearMouse(Vector2 mouse)
        {
            _panel.ResetSize();
            var size    = _panel.Size;
            var screen  = GetViewport().GetVisibleRect().Size;

            float x = mouse.X + 18f;
            float y = mouse.Y - size.Y * 0.5f;

            if (x + size.X > screen.X - 4f) x = mouse.X - size.X - 14f;
            y = Mathf.Clamp(y, 4f, screen.Y - size.Y - 4f);

            _panel.Position = new Vector2(x, y);
        }

        // ── 工具 ──────────────────────────────────────────────────────

        private static HBoxContainer MakeBarRow(string keyText,
            out ProgressBar bar, out Label valLbl, Color barColor)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 5);

            var key = new Label { Text = keyText };
            key.AddThemeConstantOverride("font_size", 11);
            key.CustomMinimumSize = new Vector2(36, 0);
            key.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(key);

            bar = new ProgressBar();
            bar.MaxValue = 100;
            bar.Value    = 100;
            bar.ShowPercentage = false;
            bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            bar.CustomMinimumSize = new Vector2(0, 14);
            SetBarColor(bar, barColor);
            row.AddChild(bar);

            valLbl = new Label();
            valLbl.AddThemeConstantOverride("font_size", 11);
            valLbl.CustomMinimumSize    = new Vector2(48, 0);
            valLbl.HorizontalAlignment  = HorizontalAlignment.Right;
            valLbl.VerticalAlignment    = VerticalAlignment.Center;
            row.AddChild(valLbl);

            return row;
        }

        private static void SetBarColor(ProgressBar bar, Color color)
        {
            var fill = new StyleBoxFlat();
            fill.BgColor = color;
            fill.SetCornerRadiusAll(2);
            bar.AddThemeStyleboxOverride("fill", fill);

            var bg = new StyleBoxFlat();
            bg.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.55f);
            bg.SetCornerRadiusAll(2);
            bar.AddThemeStyleboxOverride("background", bg);
        }

        private static string GetDisplayName(string itemId)
        {
            var def = ItemRegistry.Instance.Get(itemId);
            return def?.DisplayName ?? itemId;
        }
    }
}
