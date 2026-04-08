using Godot;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Entities.Building
{
    /// <summary>
    /// 已放置的建造件节点（茅草墙、茅草地板等）。
    /// 在 _Ready 时根据 PieceId 生成对应网格并注册 ECS 组件。
    /// </summary>
    public partial class BuildingPiece : Node3D
    {
        [Export] public string PieceId = "";

        public int EntityId { get; private set; } = -1;

        public override void _Ready()
        {
            BuildMesh();

            EntityId = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(EntityId, new PositionComponent { Position = GlobalPosition });
            EcsWorld.Instance.AddComponent(EntityId, new BuildingPieceComponent
            {
                PieceId    = PieceId,
                HpMax      = 500f,
                HpCurrent  = 500f
            });
        }

        private void BuildMesh()
        {
            var mesh = new MeshInstance3D();
            var mat  = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.72f, 0.60f, 0.32f)   // 茅草棕黄色
            };

            switch (PieceId)
            {
                case "thatch_floor":
                    mesh.Mesh = new BoxMesh { Size = new Vector3(2f, 0.15f, 2f) };
                    mesh.Position = new Vector3(0f, 0.075f, 0f);
                    break;

                case "thatch_wall":
                    mesh.Mesh = new BoxMesh { Size = new Vector3(2f, 2.5f, 0.2f) };
                    mesh.Position = new Vector3(0f, 1.25f, 0f);
                    break;

                default:
                    mesh.Mesh = new BoxMesh { Size = Vector3.One };
                    break;
            }

            mesh.MaterialOverride = mat;
            AddChild(mesh);
        }

        public override void _ExitTree()
        {
            if (EntityId >= 0) EcsWorld.Instance.DestroyEntity(EntityId);
        }
    }
}
