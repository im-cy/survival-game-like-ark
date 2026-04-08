using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;
using SurvivalGame.Core.Systems;

namespace SurvivalGame.UI
{
    /// <summary>
    /// 制作菜单 — C 键切换显示/隐藏。
    /// 分两档显示配方：一阶（游戏开始已解锁）和二阶（击败 Boss 后解锁）。
    /// 未解锁的配方以灰色锁定状态显示。
    /// </summary>
    public partial class CraftMenu : CanvasLayer
    {
        private Control _root = null!;
        private VBoxContainer _recipeList = null!;
        private Label _statusLabel = null!;
        private bool _visible = false;
        private int _playerEntityId = -1;

        private static readonly Color ColUnlocked = new(0.90f, 0.90f, 0.90f);
        private static readonly Color ColLocked   = new(0.45f, 0.45f, 0.45f);
        private static readonly Color ColHeader   = new(0.95f, 0.75f, 0.20f);  // 琥珀色标题
        private static readonly Color ColSuccess  = new(0.30f, 0.90f, 0.40f);
        private static readonly Color ColFail     = new(0.90f, 0.35f, 0.30f);

        public override void _Ready()
        {
            BuildUI();
            EventBus.Instance.Subscribe("toggle_craft_menu",  OnToggle);
            EventBus.Instance.Subscribe("recipes_unlocked",   OnRecipesUnlocked);
            EventBus.Instance.Subscribe("item_crafted",       OnItemCrafted);
            EventBus.Instance.Subscribe("resource_harvested", OnInventoryChanged);
        }

        // ── UI 构建 ───────────────────────────────────────────────────────

        private void BuildUI()
        {
            // 半透明背景面板
            _root = new PanelContainer();
            _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterRight);
            _root.OffsetRight  = -20f;
            _root.OffsetLeft   = -360f;
            _root.OffsetTop    = -300f;
            _root.OffsetBottom =  300f;
            _root.Visible = false;

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            _root.AddChild(vbox);

            // 标题
            var title = new Label { Text = "⚒ 制作" };
            title.AddThemeColorOverride("font_color", ColHeader);
            title.AddThemeConstantOverride("font_size", 16);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(title);

            vbox.AddChild(new HSeparator());

            // 滚动区域（配方列表）
            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            scroll.CustomMinimumSize = new Vector2(310f, 0f);
            vbox.AddChild(scroll);

            _recipeList = new VBoxContainer();
            _recipeList.AddThemeConstantOverride("separation", 2);
            scroll.AddChild(_recipeList);

            // 底部状态提示
            _statusLabel = new Label { Text = "" };
            _statusLabel.AddThemeConstantOverride("font_size", 11);
            _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(_statusLabel);

            // 关闭提示
            var hint = new Label { Text = "C 键关闭" };
            hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
            hint.AddThemeConstantOverride("font_size", 10);
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(hint);

            AddChild(_root);
        }

        // ── 配方列表刷新 ──────────────────────────────────────────────────

        private void RefreshRecipeList()
        {
            // 每次刷新都重新查询玩家实体（与 HUD 保持一致：以 SurvivalComponent 定位玩家）
            _playerEntityId = -1;
            foreach (var id in EcsWorld.Instance.Query<SurvivalComponent>())
            { _playerEntityId = id; break; }

            var pstats = _playerEntityId >= 0
                ? EcsWorld.Instance.GetComponent<PlayerStatsComponent>(_playerEntityId)
                : null;
            var inv = _playerEntityId >= 0
                ? EcsWorld.Instance.GetComponent<InventoryComponent>(_playerEntityId)
                : null;

            // 清空旧列表
            foreach (Node child in _recipeList.GetChildren())
                child.QueueFree();

            // 按 UnlockSource 分组
            var tier1   = new List<RecipeDefinition>();
            var tier2   = new List<RecipeDefinition>();
            foreach (var recipe in RecipeRegistry.Instance.All)
            {
                if (recipe.UnlockSource == "tier1") tier1.Add(recipe);
                else                                tier2.Add(recipe);
            }

            AddSectionHeader("── 一阶：石器时代 ──");
            foreach (var r in tier1)
                AddRecipeRow(r, pstats, inv);

            AddSectionHeader("── 二阶：革质时代 ──");
            if (tier2.Count == 0)
            {
                var placeholder = new Label { Text = "  击败森林守卫者后解锁" };
                placeholder.AddThemeColorOverride("font_color", ColLocked);
                placeholder.AddThemeConstantOverride("font_size", 11);
                _recipeList.AddChild(placeholder);
            }
            else
            {
                foreach (var r in tier2)
                    AddRecipeRow(r, pstats, inv);
            }
        }

        private void AddSectionHeader(string text)
        {
            var lbl = new Label { Text = text };
            lbl.AddThemeColorOverride("font_color", ColHeader);
            lbl.AddThemeConstantOverride("font_size", 12);
            _recipeList.AddChild(lbl);
        }

        private void AddRecipeRow(RecipeDefinition recipe, PlayerStatsComponent? pstats, InventoryComponent? inv)
        {
            bool unlocked = pstats != null && pstats.UnlockedRecipes.Contains(recipe.Id);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);

            // 锁图标
            var lockLbl = new Label { Text = unlocked ? "  " : "🔒" };
            lockLbl.CustomMinimumSize = new Vector2(22, 0);
            row.AddChild(lockLbl);

            // 配方名称 + 材料需求（每种材料单独着色）
            var itemDef = ItemRegistry.Instance.Get(recipe.ResultItemId);
            string displayName = itemDef?.DisplayName ?? recipe.ResultItemId;
            bool canCraft = unlocked && inv != null;

            // 先计算每种材料是否满足，顺便算 canCraft
            var ingInfos = new System.Collections.Generic.List<(string label, bool met)>();
            foreach (var ing in recipe.Ingredients)
            {
                var ingDef = ItemRegistry.Instance.Get(ing.ItemId);
                string ingName = ingDef?.DisplayName ?? ing.ItemId;
                int have = inv?.CountItem(ing.ItemId) ?? 0;
                bool met = have >= ing.Quantity;
                ingInfos.Add(($"{ingName}×{ing.Quantity}({have})", met));
                if (!met) canCraft = false;
            }

            // 名称前缀 Label
            string qty = recipe.ResultQuantity > 1 ? $"×{recipe.ResultQuantity}" : "";
            var nameContainer = new HBoxContainer();
            nameContainer.AddThemeConstantOverride("separation", 4);
            nameContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var prefixLbl = new Label { Text = $"{displayName}{qty} — " };
            prefixLbl.AddThemeColorOverride("font_color", unlocked ? ColUnlocked : ColLocked);
            prefixLbl.AddThemeConstantOverride("font_size", 12);
            nameContainer.AddChild(prefixLbl);

            // 每种材料单独一个 Label
            for (int i = 0; i < ingInfos.Count; i++)
            {
                var (ingLabel, met) = ingInfos[i];
                string sep = i < ingInfos.Count - 1 ? "  " : "";
                var ingLbl = new Label { Text = ingLabel + sep };
                Color ingColor = !unlocked ? ColLocked : (met ? ColUnlocked : ColFail);
                ingLbl.AddThemeColorOverride("font_color", ingColor);
                ingLbl.AddThemeConstantOverride("font_size", 12);
                nameContainer.AddChild(ingLbl);
            }

            row.AddChild(nameContainer);

            // 制作按钮（仅已解锁）
            if (unlocked)
            {
                var btn = new Button { Text = "制作" };
                btn.Disabled = !canCraft;
                btn.CustomMinimumSize = new Vector2(52, 0);
                string recipeId = recipe.Id;  // capture for lambda
                btn.Pressed += () => OnCraftPressed(recipeId);
                row.AddChild(btn);
            }

            _recipeList.AddChild(row);
        }

        // ── 事件处理 ──────────────────────────────────────────────────────

        private void OnToggle(object? _)
        {
            _visible = !_visible;
            _root.Visible = _visible;
            if (_visible)
                RefreshRecipeList();
            else
                _playerEntityId = -1;   // 关闭时清除缓存，下次重新查找
        }

        private void OnRecipesUnlocked(object? _)   { if (_visible) RefreshRecipeList(); }
        private void OnInventoryChanged(object? _)  { if (_visible) RefreshRecipeList(); }

        private void OnItemCrafted(object? payload)
        {
            string itemId = payload is string s ? s : "";
            var def = ItemRegistry.Instance.Get(itemId);
            ShowStatus($"已制作：{def?.DisplayName ?? itemId}", ColSuccess);
            if (_visible) RefreshRecipeList();
        }

        private void OnCraftPressed(string recipeId)
        {
            // 制作前重新确认玩家实体 ID
            if (_playerEntityId < 0)
                foreach (var id in EcsWorld.Instance.Query<SurvivalComponent>())
                { _playerEntityId = id; break; }
            if (_playerEntityId < 0) return;

            var result = GameManager.Instance?.Crafting?.TryCraft(_playerEntityId, recipeId);
            switch (result)
            {
                case CraftResult.Success:
                    break;  // OnItemCrafted 会处理提示
                case CraftResult.InsufficientMaterials:
                    ShowStatus("材料不足！", ColFail); break;
                case CraftResult.NotUnlocked:
                    ShowStatus("配方未解锁", ColFail); break;
                default:
                    ShowStatus("制作失败", ColFail); break;
            }
        }

        private async void ShowStatus(string msg, Color color)
        {
            _statusLabel.Text = msg;
            _statusLabel.AddThemeColorOverride("font_color", color);
            await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
            if (IsInsideTree()) _statusLabel.Text = "";
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("toggle_craft_menu",  OnToggle);
            EventBus.Instance.Unsubscribe("recipes_unlocked",   OnRecipesUnlocked);
            EventBus.Instance.Unsubscribe("item_crafted",        OnItemCrafted);
            EventBus.Instance.Unsubscribe("resource_harvested",  OnInventoryChanged);
        }
    }
}
