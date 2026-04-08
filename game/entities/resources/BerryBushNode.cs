using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Systems;

namespace SurvivalGame.Entities.Resources
{
    /// <summary>
    /// 浆果丛节点 — 硬编码 berry 资源，不依赖 tscn 属性覆盖。
    /// </summary>
    public partial class BerryBushNode : Node3D
    {
        public int EntityId { get; private set; } = -1;

        private Sprite3D      _sprite      = null!;
        private MeshInstance3D _placeholder = null!;

        public override void _Ready()
        {
            _sprite      = GetNode<Sprite3D>("Sprite3D");
            _placeholder = GetNode<MeshInstance3D>("Placeholder");

            if (_sprite.Texture != null)
            {
                _sprite.Visible      = true;
                _placeholder.Visible = false;
            }

            EntityId = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(EntityId, new PositionComponent { Position = GlobalPosition });
            EcsWorld.Instance.AddComponent(EntityId, new HarvestableComponent
            {
                ResourceId    = "berry",
                YieldMin      = 2,
                YieldMax      = 4,
                HitsRemaining = 5
            });

            EventBus.Instance.Subscribe("resource_harvested", OnResourceHarvested);
        }

        private void OnResourceHarvested(object? payload)
        {
            if (payload is not HarvestEventData data || data.NodeEntityId != EntityId) return;
            if (data.Depleted)
            {
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
