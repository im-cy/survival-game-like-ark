using System.Collections.Generic;

namespace SurvivalGame.Core.Data
{
    /// <summary>
    /// 建造件定义 — M2：营火、茅草房（整体建筑）。
    /// 设计原则：2.5D俯视游戏中，建造物以"整体"为单位放置，
    ///            而非零散拼接墙片/地板，保证视觉完整性。
    /// </summary>
    public class BuildingPieceDef
    {
        public string Id = "";
        public string DisplayName = "";
        public Dictionary<string, int> Materials = new();
        public bool IsCampfire = false;
        /// <summary>是否提供庇护所效果（挡风避寒）</summary>
        public bool IsShelter = false;
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
                Id = "thatch_house", DisplayName = "茅草房",
                Materials = new Dictionary<string, int> { ["grass"] = 15, ["wood"] = 5 },
                IsShelter = true
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
