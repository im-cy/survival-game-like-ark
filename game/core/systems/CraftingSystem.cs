using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;

namespace SurvivalGame.Core.Systems
{
    public class CraftingSystem : SystemBase
    {
        public override void Tick(float delta) { }  // 无被动逻辑，仅响应玩家操作

        public CraftResult TryCraft(int entityId, string recipeId)
        {
            var recipe = RecipeRegistry.Instance.Get(recipeId);
            if (recipe == null) return CraftResult.UnknownRecipe;

            var inv    = EcsWorld.Instance.GetComponent<InventoryComponent>(entityId);
            var pstats = EcsWorld.Instance.GetComponent<PlayerStatsComponent>(entityId);
            if (inv == null) return CraftResult.Failed;

            // 解锁检查
            if (pstats != null && !pstats.UnlockedRecipes.Contains(recipeId))
                return CraftResult.NotUnlocked;

            // 材料检查
            foreach (var req in recipe.Ingredients)
                if (inv.CountItem(req.ItemId) < req.Quantity)
                    return CraftResult.InsufficientMaterials;

            // 制作站检查
            if (recipe.RequiredStation != "" && !IsNearStation(entityId, recipe.RequiredStation))
                return CraftResult.NeedCraftingStation;

            // 原子消耗 + 产出
            foreach (var req in recipe.Ingredients)
                inv.RemoveItem(req.ItemId, req.Quantity);

            var itemDef = ItemRegistry.Instance.Get(recipe.ResultItemId);
            float weight = itemDef?.Weight ?? 0.1f;
            inv.AddItem(recipe.ResultItemId, recipe.ResultQuantity, weight);

            EventBus.Instance.Emit("item_crafted", recipe.ResultItemId);
            return CraftResult.Success;
        }

        private bool IsNearStation(int entityId, string stationType)
        {
            // TODO: 检查玩家附近是否有对应制作站 Node
            return true; // 占位，后续实现
        }
    }

    public enum CraftResult { Success, InsufficientMaterials, NeedCraftingStation, NotUnlocked, UnknownRecipe, Failed }
}
