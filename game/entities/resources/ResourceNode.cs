using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Systems;

namespace SurvivalGame.Entities.Resources
{
    /// <summary>
    /// 资源节点视图（树/石头等）。
    /// 在场景中放置后自动向 ECS 注册 HarvestableComponent，
    /// 监听采集事件并更新视觉状态。
    /// </summary>
    public partial class ResourceNode : Node3D
    {
        [Export] public string ResourceId  = "wood";   // 采集后获得的物品 ID
        [Export] public int    YieldMin    = 1;
        [Export] public int    YieldMax    = 3;
        [Export] public int    HitsTotal   = 3;        // 耗尽前可被采几次
        [Export] public Texture2D? SpriteSheet;         // 留位：提供美术后赋值

        public int EntityId { get; private set; } = -1;

        private Sprite3D      _sprite      = null!;
        private MeshInstance3D _placeholder = null!;

        public override void _Ready()
        {
            _sprite      = GetNode<Sprite3D>("Sprite3D");
            _placeholder = GetNode<MeshInstance3D>("Placeholder");

            // 有美术素材时显示精灵，否则保留占位几何体
            if (SpriteSheet != null)
            {
                _sprite.Texture  = SpriteSheet;
                _sprite.Visible  = true;
                _placeholder.Visible = false;
            }

            // 注册 ECS 实体
            EntityId = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(EntityId, new PositionComponent { Position = GlobalPosition });
            EcsWorld.Instance.AddComponent(EntityId, new HarvestableComponent
            {
                ResourceId    = ResourceId,
                YieldMin      = YieldMin,
                YieldMax      = YieldMax,
                HitsRemaining = HitsTotal
            });

            EventBus.Instance.Subscribe("resource_harvested", OnResourceHarvested);
        }

        private void OnResourceHarvested(object? payload)
        {
            if (payload is not HarvestEventData data || data.NodeEntityId != EntityId) return;
            if (data.Depleted)
            {
                // 耗尽：隐藏视觉（后续可换成枯树/残石动画）
                _sprite.Visible      = false;
                _placeholder.Visible = false;
            }
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("resource_harvested", OnResourceHarvested);
            if (EntityId >= 0)
                EcsWorld.Instance.DestroyEntity(EntityId);
        }
    }
}
