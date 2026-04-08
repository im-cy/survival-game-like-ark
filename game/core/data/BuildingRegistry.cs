using System.Collections.Generic;

namespace SurvivalGame.Core.Data
{
    /// <summary>
    /// 建造件定义 — M2 硬编码：营火、茅草地板、茅草墙。
    /// </summary>
    public class BuildingPieceDef
    {
        public string Id = "";
        public string DisplayName = "";
        public Dictionary<string, int> Materials = new();
        public bool IsCampfire = false;
    }

    public static class BuildingRegistry
    {
        public static readonly List<BuildingPieceDef> Pieces = new()
        {
            new BuildingPieceDef
            {
                Id = "campfire", DisplayName = "营火",
                Materials = new Dictionary<string, int> { ["wood"] = 5 },
                IsCampfire = true
            },
            new BuildingPieceDef
            {
                Id = "thatch_floor", DisplayName = "茅草地板",
                Materials = new Dictionary<string, int> { ["grass"] = 5 }
            },
            new BuildingPieceDef
            {
                Id = "thatch_wall", DisplayName = "茅草墙",
                Materials = new Dictionary<string, int> { ["grass"] = 8, ["wood"] = 2 }
            },
        };

        public static BuildingPieceDef? Get(string id) =>
            Pieces.Find(p => p.Id == id);

        /// <summary>返回所需材料的可读字符串，如 "木材×5"</summary>
        public static string GetCostText(BuildingPieceDef piece)
        {
            var sb = new System.Text.StringBuilder();
            var ir = ItemRegistry.Instance;
            foreach (var kvp in piece.Materials)
            {
                var def = ir.Get(kvp.Key);
                string name = def?.DisplayName ?? kvp.Key;
                if (sb.Length > 0) sb.Append("  ");
                sb.Append($"{name}×{kvp.Value}");
            }
            return sb.ToString();
        }
    }
}
