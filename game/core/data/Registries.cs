using Godot;
using System.Collections.Generic;

namespace SurvivalGame.Core.Data
{
    /// <summary>
    /// 物品注册表 — 运行时所有 ItemDefinition 的索引。
    /// 在 Main 场景启动时，扫描 res://resources/items/ 自动注册。
    /// </summary>
    public class ItemRegistry
    {
        public static ItemRegistry Instance { get; } = new();
        private readonly Dictionary<string, ItemDefinition> _items = new();

        public void Register(ItemDefinition def) => _items[def.Id] = def;

        public ItemDefinition? Get(string id)
            => _items.TryGetValue(id, out var d) ? d : null;

        public IEnumerable<ItemDefinition> All => _items.Values;

        public void LoadFromDirectory(string path = "res://resources/items/")
        {
            using var dir = DirAccess.Open(path);
            if (dir == null) return;
            dir.ListDirBegin();
            string file;
            while ((file = dir.GetNext()) != "")
            {
                if (!file.EndsWith(".tres") && !file.EndsWith(".res")) continue;
                var res = GD.Load<ItemDefinition>(path + file);
                if (res != null) Register(res);
            }
        }
    }

    /// <summary>配方注册表</summary>
    public class RecipeRegistry
    {
        public static RecipeRegistry Instance { get; } = new();
        private readonly Dictionary<string, RecipeDefinition> _recipes = new();

        public void Register(RecipeDefinition def) => _recipes[def.Id] = def;

        public RecipeDefinition? Get(string id)
            => _recipes.TryGetValue(id, out var d) ? d : null;

        public IEnumerable<RecipeDefinition> All => _recipes.Values;

        public void LoadFromDirectory(string path = "res://resources/recipes/")
        {
            using var dir = DirAccess.Open(path);
            if (dir == null) return;
            dir.ListDirBegin();
            string file;
            while ((file = dir.GetNext()) != "")
            {
                if (!file.EndsWith(".tres") && !file.EndsWith(".res")) continue;
                var res = GD.Load<RecipeDefinition>(path + file);
                if (res != null) Register(res);
            }
        }
    }

    /// <summary>生物注册表</summary>
    public class CreatureRegistry
    {
        public static CreatureRegistry Instance { get; } = new();
        private readonly Dictionary<string, CreatureDefinition> _creatures = new();

        public void Register(CreatureDefinition def) => _creatures[def.Id] = def;

        public CreatureDefinition? Get(string id)
            => _creatures.TryGetValue(id, out var d) ? d : null;

        public IEnumerable<CreatureDefinition> All => _creatures.Values;

        public void LoadFromDirectory(string path = "res://resources/creatures/")
        {
            using var dir = DirAccess.Open(path);
            if (dir == null) return;
            dir.ListDirBegin();
            string file;
            while ((file = dir.GetNext()) != "")
            {
                if (!file.EndsWith(".tres") && !file.EndsWith(".res")) continue;
                var res = GD.Load<CreatureDefinition>(path + file);
                if (res != null) Register(res);
            }
        }
    }
}
