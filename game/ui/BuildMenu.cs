using Godot;
using SurvivalGame.Core.Data;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.UI
{
    /// <summary>
    /// 建造菜单（B 键切换）— 茅草 / 木质 / 功能件三段式展示。
    /// 点击"放置"后进入建造预览模式，左键确认，右键取消。
    /// </summary>
    public partial class BuildMenu : CanvasLayer
    {
        private PanelContainer _panel = null!;
        private bool _isOpen = false;
        private int _playerEntityId = -1;

        public override void _Ready()
        {
            BuildUI();
            EventBus.Instance.Subscribe("toggle_build_menu", OnToggleBuildMenu);
        }

        private void OnToggleBuildMenu(object? _) => Toggle();

        // ── UI 构建 ────────────────────────────────────────────────────

        private void BuildUI()
        {
            _panel = new PanelContainer();
            _panel.OffsetLeft = 10f;
            _panel.OffsetTop  = 120f;
            _panel.CustomMinimumSize = new Vector2(340, 0);
            _panel.Visible = false;
            AddChild(_panel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            _panel.AddChild(vbox);

            vbox.AddChild(new Label { Text = "建造  [B] 关闭" });
            vbox.AddChild(new HSeparator());

            BuildingTier? lastTier  = null;
            bool          shownSpec = false;

            foreach (var piece in BuildingRegistry.MenuPieces())
            {
                if (piece.PieceType == BuildingPieceType.Special)
                {
                    if (!shownSpec)
                    {
                        AddSectionHeader(vbox, "── 功能件 ──");
                        shownSpec = true;
                    }
                }
                else if (piece.Tier != lastTier)
                {
                    string header = piece.Tier == BuildingTier.Thatch ? "── 茅草 ──" : "── 木质 ──";
                    AddSectionHeader(vbox, header);
                    lastTier = piece.Tier;
                }

                AddPieceRow(vbox, piece);
            }

            var hint = new Label { Text = "左键放置  右键取消" };
            hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            vbox.AddChild(hint);
        }

        private static void AddSectionHeader(VBoxContainer vbox, string text)
        {
            var lbl = new Label { Text = text };
            lbl.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 0.4f));
            vbox.AddChild(lbl);
        }

        private void AddPieceRow(VBoxContainer vbox, BuildingPieceDef piece)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var nameLbl = new Label { Text = piece.DisplayName };
            nameLbl.CustomMinimumSize = new Vector2(88, 24);
            nameLbl.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(nameLbl);

            var costLbl = new Label { Text = BuildingRegistry.GetCostText(piece) };
            costLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            costLbl.VerticalAlignment   = VerticalAlignment.Center;
            costLbl.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            row.AddChild(costLbl);

            var capturedId = piece.Id;
            var btn = new Button { Text = "放置" };
            btn.CustomMinimumSize = new Vector2(56, 24);
            btn.Pressed += () => OnPieceSelected(capturedId);
            row.AddChild(btn);

            vbox.AddChild(row);
        }

        // ── 交互 ──────────────────────────────────────────────────────

        public void Toggle()
        {
            _isOpen = !_isOpen;
            _panel.Visible = _isOpen;
        }

        private void OnPieceSelected(string pieceId)
        {
            _isOpen = false;
            _panel.Visible = false;
            EventBus.Instance.Emit("enter_build_mode", pieceId);
        }

        public override void _Process(double delta)
        {
            if (!_isOpen) return;
            if (_playerEntityId < 0)
            {
                foreach (var id in EcsWorld.Instance.Query<SurvivalComponent>())
                { _playerEntityId = id; break; }
            }
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("toggle_build_menu", OnToggleBuildMenu);
        }
    }
}
