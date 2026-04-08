using Godot;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Entities.Building
{
    /// <summary>
    /// 营火实体节点。
    /// 注册 ECS CampfireComponent，监听熄灭事件以更新视觉。
    /// </summary>
    public partial class Campfire : Node3D
    {
        public int EntityId { get; private set; } = -1;

        private OmniLight3D? _light;
        private MeshInstance3D? _flameMesh;

        public override void _Ready()
        {
            EntityId = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(EntityId, new PositionComponent { Position = GlobalPosition });
            EcsWorld.Instance.AddComponent(EntityId, new CampfireComponent());

            _light = GetNodeOrNull<OmniLight3D>("OmniLight3D");
            _flameMesh = GetNodeOrNull<MeshInstance3D>("Flame");

            BuildVisuals();

            EventBus.Instance.Subscribe("campfire_extinguished", OnExtinguished);
            GD.Print($"[Campfire] 点燃 EntityId={EntityId}，温暖半径 5m");
        }

        private void BuildVisuals()
        {
            // 柴堆（圆柱）
            var logs = GetNodeOrNull<MeshInstance3D>("Logs");
            if (logs != null)
            {
                logs.Mesh = new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.45f, Height = 0.35f };
                logs.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.20f, 0.08f) };
            }

            // 火焰（球体，发光）
            if (_flameMesh != null)
            {
                _flameMesh.Mesh = new SphereMesh { Radius = 0.22f, Height = 0.44f };
                var flameMat = new StandardMaterial3D
                {
                    AlbedoColor     = new Color(1f, 0.45f, 0.05f),
                    EmissionEnabled = true,
                    Emission        = new Color(1f, 0.3f, 0f) * 1.5f
                };
                _flameMesh.MaterialOverride = flameMat;
            }
        }

        private void OnExtinguished(object? payload)
        {
            if (payload is not int id || id != EntityId) return;
            if (_light != null) _light.Visible = false;
            if (_flameMesh != null) _flameMesh.Visible = false;
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("campfire_extinguished", OnExtinguished);
            if (EntityId >= 0) EcsWorld.Instance.DestroyEntity(EntityId);
        }
    }
}
