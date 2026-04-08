using Godot;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Core.Systems
{
    /// <summary>
    /// 建筑系统 — 目前仅处理建筑受损后的销毁事件。
    /// 整体建筑始终稳定，不做结构完整性检查。
    /// </summary>
    public class BuildingSystem : SystemBase
    {
        public override void Tick(float delta)
        {
            // 整体建筑始终稳定，暂无周期性逻辑
        }
    }
}
