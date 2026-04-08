using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;

namespace SurvivalGame.UI
{
    /// <summary>
    /// 背包 &amp; 装备界面 — I 键切换显示/隐藏。
    /// 顶部显示装备槽（武器、胸甲），下方列出背包物品并提供装备/卸下按钮。
    /// 装备操作：从背包取出 1 件移入装备槽；卸下时归还背包。
    /// </summary>
    public partial class InventoryMenu : CanvasLayer
    {
        private Control _root = null!;
        private VBoxContainer _equipSlots = null!;
        private VBoxContainer _itemList   = null!;
        private bool _visible = false;
        private int  _playerEntityId = -1;

        private static readonly Color ColHeader  = new(0.95f, 0.75f, 0.20f);
        private static readonly Color ColNormal  = new(0.90f, 0.90f, 0.90f);
        private static readonly Color ColEquip   = new(0.40f, 0.90f, 0.50f);
        private static readonly Color ColSlotFull= new(0.55f, 0.85f, 1.00f);

        public override void _Ready()
        {
            BuildUI();
            EventBus.Instance.Subscribe("toggle_inventory",   OnToggle);
            EventBus.Instance.Subscribe("item_crafted",       OnInventoryChanged);
            EventBus.Instance.Subscribe("resource_harvested", OnInventoryChanged);
        }

        // ── UI 构建 ───────────────────────────────────────────────────────

        private void BuildUI()
        {
            _root = new PanelContainer();
            _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterLeft);
            _root.OffsetLeft   = 20f;
            _root.OffsetRight  = 380f;
            _root.OffsetTop    = -320f;
            _root.OffsetBottom =  320f;
            _root.Visible = false;

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            _root.AddChild(vbox);

            // 标题
            var title = new Label { Text = "🎒 背包 & 装备" };
            title.AddThemeColorOverride("font_color", ColHeader);
            title.AddThemeConstantOverride("font_size", 16);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(title);

            vbox.AddChild(new HSeparator());

            // ── 装备槽区域 ──────────────────────────────────────────────
            var equipHeader = new Label { Text = "── 装备槽 ──" };
            equipHeader.AddThemeColorOverride("font_color", ColHeader);
            equipHeader.AddThemeConstantOverride("font_size", 12);
            vbox.AddChild(equipHeader);

            _equipSlots = new VBoxContainer();
            _equipSlots.AddThemeConstantOverride("separation", 2);
            vbox.AddChild(_equipSlots);

            vbox.AddChild(new HSeparator());

            // ── 背包物品区域 ─────────────────────────────────────────────
            var invHeader = new Label { Text = "── 背包物品 ──" };
            invHeader.AddThemeColorOverride("font_color", ColHeader);
            invHeader.AddThemeConstantOverride("font_size", 12);
            vbox.AddChild(invHeader);

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            scroll.CustomMinimumSize = new Vector2(320f, 0f);
            vbox.AddChild(scroll);

            _itemList = new VBoxContainer();
            _itemList.AddThemeConstantOverride("separation", 2);
            scroll.AddChild(_itemList);

            // 关闭提示
            var hint = new Label { Text = "I 键关闭" };
            hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
            hint.AddThemeConstantOverride("font_size", 10);
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(hint);

            AddChild(_root);
        }

        // ── 刷新 ─────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            _playerEntityId = -1;
            foreach (var id in EcsWorld.Instance.Query<SurvivalComponent>())
            { _playerEntityId = id; break; }

            var inv   = _playerEntityId >= 0
                ? EcsWorld.Instance.GetComponent<InventoryComponent>(_playerEntityId) : null;
            var equip = _playerEntityId >= 0
                ? EcsWorld.Instance.GetComponent<EquipmentComponent>(_playerEntityId) : null;

            RefreshEquipSlots(equip, inv);
            RefreshItemList(inv, equip);
        }

        private void RefreshEquipSlots(EquipmentComponent? equip, InventoryComponent? inv)
        {
            foreach (Node child in _equipSlots.GetChildren()) child.QueueFree();

            AddSlotRow("⚔ 武器", equip?.WeaponId, true,  equip, inv);
            AddSlotRow("🛡 胸甲", equip?.ChestId,  false, equip, inv);
        }

        private void AddSlotRow(string label, string? equippedId,
            bool isWeapon, EquipmentComponent? equip, InventoryComponent? inv)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);

            var slotLbl = new Label { Text = label };
            slotLbl.CustomMinimumSize = new Vector2(60f, 0);
            slotLbl.AddThemeConstantOverride("font_size", 12);
            row.AddChild(slotLbl);

            var nameLbl = new Label();
            nameLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            nameLbl.AddThemeConstantOverride("font_size", 12);

            if (equippedId != null)
            {
                var def = ItemRegistry.Instance.Get(equippedId);
                nameLbl.Text = def?.DisplayName ?? equippedId;
                nameLbl.AddThemeColorOverride("font_color", ColSlotFull);

                var unequipBtn = new Button { Text = "卸下" };
                unequipBtn.CustomMinimumSize = new Vector2(52, 0);
                bool capturedIsWeapon = isWeapon;
                unequipBtn.Pressed += () => { TryUnequip(capturedIsWeapon); };
                row.AddChild(nameLbl);
                row.AddChild(unequipBtn);
            }
            else
            {
                nameLbl.Text = "（空）";
                nameLbl.AddThemeColorOverride("font_color", new Color(0.50f, 0.50f, 0.50f));
                row.AddChild(nameLbl);
            }

            _equipSlots.AddChild(row);
        }

        private void RefreshItemList(InventoryComponent? inv, EquipmentComponent? equip)
        {
            foreach (Node child in _itemList.GetChildren()) child.QueueFree();

            if (inv == null || inv.Items.Count == 0)
            {
                var empty = new Label { Text = "  （空）" };
                empty.AddThemeColorOverride("font_color", new Color(0.50f, 0.50f, 0.50f));
                empty.AddThemeConstantOverride("font_size", 12);
                _itemList.AddChild(empty);
                return;
            }

            foreach (var item in inv.Items)
            {
                var def = ItemRegistry.Instance.Get(item.ItemId);
                if (def == null) continue;

                bool isEquippable = def.Category == ItemCategory.Tool
                                 || def.Category == ItemCategory.Weapon
                                 || def.Category == ItemCategory.Armor;

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);

                var nameLbl = new Label();
                nameLbl.Text = $"{def.DisplayName} ×{item.Quantity}";
                nameLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                nameLbl.AddThemeColorOverride("font_color", ColNormal);
                nameLbl.AddThemeConstantOverride("font_size", 12);
                row.AddChild(nameLbl);

                if (isEquippable)
                {
                    // 显示装备/已装备状态
                    bool alreadyEquipped = (def.Category == ItemCategory.Armor)
                        ? equip?.ChestId == item.ItemId
                        : equip?.WeaponId == item.ItemId;

                    if (!alreadyEquipped)
                    {
                        var equipBtn = new Button { Text = "装备" };
                        equipBtn.CustomMinimumSize = new Vector2(52, 0);
                        string capturedId = item.ItemId;
                        equipBtn.Pressed += () => TryEquip(capturedId);
                        row.AddChild(equipBtn);
                    }
                    else
                    {
                        var badge = new Label { Text = "✓已装" };
                        badge.AddThemeColorOverride("font_color", ColEquip);
                        badge.AddThemeConstantOverride("font_size", 11);
                        row.AddChild(badge);
                    }
                }

                _itemList.AddChild(row);
            }
        }

        // ── 装备 / 卸下 ──────────────────────────────────────────────────

        private void TryEquip(string itemId)
        {
            if (_playerEntityId < 0) return;
            var inv   = EcsWorld.Instance.GetComponent<InventoryComponent>(_playerEntityId);
            var equip = EcsWorld.Instance.GetComponent<EquipmentComponent>(_playerEntityId);
            var def   = ItemRegistry.Instance.Get(itemId);
            if (inv == null || equip == null || def == null) return;

            bool isWeapon = def.Category == ItemCategory.Tool || def.Category == ItemCategory.Weapon;
            bool isArmor  = def.Category == ItemCategory.Armor;
            if (!isWeapon && !isArmor) return;

            if (isWeapon)
            {
                // 若槽中有旧武器，先归还背包
                if (equip.WeaponId != null)
                {
                    var oldDef = ItemRegistry.Instance.Get(equip.WeaponId);
                    inv.AddItem(equip.WeaponId, 1, oldDef?.Weight ?? 0.5f);
                }
                inv.RemoveItem(itemId, 1);
                equip.WeaponId = itemId;
            }
            else
            {
                if (equip.ChestId != null)
                {
                    var oldDef = ItemRegistry.Instance.Get(equip.ChestId);
                    inv.AddItem(equip.ChestId, 1, oldDef?.Weight ?? 0.5f);
                }
                inv.RemoveItem(itemId, 1);
                equip.ChestId = itemId;
            }

            RefreshAll();
        }

        private void TryUnequip(bool isWeapon)
        {
            if (_playerEntityId < 0) return;
            var inv   = EcsWorld.Instance.GetComponent<InventoryComponent>(_playerEntityId);
            var equip = EcsWorld.Instance.GetComponent<EquipmentComponent>(_playerEntityId);
            if (inv == null || equip == null) return;

            if (isWeapon && equip.WeaponId != null)
            {
                var def = ItemRegistry.Instance.Get(equip.WeaponId);
                inv.AddItem(equip.WeaponId, 1, def?.Weight ?? 0.5f);
                equip.WeaponId = null;
            }
            else if (!isWeapon && equip.ChestId != null)
            {
                var def = ItemRegistry.Instance.Get(equip.ChestId);
                inv.AddItem(equip.ChestId, 1, def?.Weight ?? 0.5f);
                equip.ChestId = null;
            }

            RefreshAll();
        }

        // ── 事件处理 ──────────────────────────────────────────────────────

        private void OnToggle(object? _)
        {
            _visible = !_visible;
            _root.Visible = _visible;
            GD.Print($"[InventoryMenu] 切换可见性 → {_visible}");
            if (_visible)
                RefreshAll();
            else
                _playerEntityId = -1;
        }

        private void OnInventoryChanged(object? _) { if (_visible) RefreshAll(); }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("toggle_inventory",   OnToggle);
            EventBus.Instance.Unsubscribe("item_crafted",       OnInventoryChanged);
            EventBus.Instance.Unsubscribe("resource_harvested", OnInventoryChanged);
        }
    }
}
