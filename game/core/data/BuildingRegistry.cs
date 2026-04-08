using System.Collections.Generic;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Core.Data
{
    /// <summary>
    /// 建造件定义。
    /// 整体建筑两档（茅草房 / 木质房）+ 功能件（营火）。
    /// </summary>
    public class BuildingPieceDef
    {
        public string Id = "";
        public string DisplayName = "";
        public Dictionary<string, int> Materials = new();
        public float HpMax = 500f;
        public BuildingTier Tier = BuildingTier.Thatch;
        public BuildingPieceType PieceType = BuildingPieceType.House;
        public bool IsCampfire = false;
        public bool IsShelter  = false;
    }

    public static class BuildingRegistry
    {
        public static readonly List<BuildingPieceDef> Pieces = new()
        {
            // ── 茅草档（Tier 1）────────────────────────────────────────
            new BuildingPieceDef
            {
                Id = "thatch_house", DisplayName = "茅草房",
                Materials = new() { ["grass"] = 15, ["wood"] = 5 },
                HpMax = 1000f,
                Tier = BuildingTier.Thatch, PieceType = BuildingPieceType.House,
                IsShelter = true,
            },

            // ── 木质档（Tier 2）────────────────────────────────────────
            new BuildingPieceDef
            {
                Id = "wood_house", DisplayName = "木质房",
                Materials = new() { ["wood"] = 20 },
                HpMax = 2500f,
                Tier = BuildingTier.Wood, PieceType = BuildingPieceType.House,
                IsShelter = true,
            },

            // ── 功能件 ────────────────────────────────────────────────
            new BuildingPieceDef
            {
                Id = "campfire", DisplayName = "营火",
                Materials = new() { ["wood"] = 5 },
                HpMax = 200f,
                Tier = BuildingTier.Thatch, PieceType = BuildingPieceType.Special,
                IsCampfire = true,
            },
        };

        public static BuildingPieceDef? Get(string id) =>
            Pieces.Find(p => p.Id == id);

        public static IEnumerable<BuildingPieceDef> MenuPieces() => Pieces;

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
