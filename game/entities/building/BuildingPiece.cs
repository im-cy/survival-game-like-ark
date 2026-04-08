using Godot;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Entities.Building
{
    /// <summary>
    /// 已放置的建造件节点。
    /// 在 _Ready 时根据 PieceId 组合网格，注册 ECS 组件。
    /// 茅草房：地板 + 四面墙（南墙留门洞）+ 四棱锥屋顶。
    /// </summary>
    public partial class BuildingPiece : Node3D
    {
        [Export] public string PieceId = "";

        public int EntityId { get; private set; } = -1;

        // ── 材质（整个建造件共用）──────────────────────────────────────
        private static StandardMaterial3D MatFloor() =>
            new() { AlbedoColor = new Color(0.65f, 0.55f, 0.28f) };
        private static StandardMaterial3D MatWall() =>
            new() { AlbedoColor = new Color(0.75f, 0.62f, 0.30f) };
        private static StandardMaterial3D MatRoof() =>
            new() { AlbedoColor = new Color(0.48f, 0.36f, 0.13f) };

        public override void _Ready()
        {
            switch (PieceId)
            {
                case "thatch_house": BuildThatchHouse(); break;
                default:             BuildDefaultBox();  break;
            }

            EntityId = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(EntityId, new PositionComponent { Position = GlobalPosition });
            EcsWorld.Instance.AddComponent(EntityId, new BuildingPieceComponent
            {
                PieceId   = PieceId,
                HpMax     = PieceId == "thatch_house" ? 1000f : 500f,
                HpCurrent = PieceId == "thatch_house" ? 1000f : 500f
            });
        }

        // ── 茅草房完整网格 ────────────────────────────────────────────

        private void BuildThatchHouse()
        {
            // 尺寸常量
            const float W     = 4f;     // 宽度（X）
            const float D     = 4f;     // 深度（Z）
            const float WallH = 2.5f;   // 墙高
            const float Thick = 0.25f;  // 墙厚
            const float FlrH  = 0.2f;   // 地板厚
            const float DoorW = 1.0f;   // 门洞宽
            const float DoorH = 2.0f;   // 门洞高
            const float WallY = FlrH + WallH / 2f;   // 墙体中心 Y

            var mFloor = MatFloor();
            var mWall  = MatWall();
            var mRoof  = MatRoof();

            // 地板
            AddBox(new Vector3(0, FlrH / 2f, 0), new Vector3(W, FlrH, D), mFloor);

            // 北墙（完整）
            AddBox(new Vector3(0, WallY, -D / 2f), new Vector3(W, WallH, Thick), mWall);

            // 东墙 / 西墙
            AddBox(new Vector3( W / 2f, WallY, 0), new Vector3(Thick, WallH, D), mWall);
            AddBox(new Vector3(-W / 2f, WallY, 0), new Vector3(Thick, WallH, D), mWall);

            // 南墙：左右两段 + 门楣
            float sideW    = (W - DoorW) / 2f;          // 1.5m
            float sideX    = (W / 2f - sideW / 2f);     // 1.25m
            float lintelH  = WallH - DoorH;             // 0.5m
            float lintelY  = FlrH + DoorH + lintelH / 2f;  // 2.45m

            AddBox(new Vector3(-sideX, WallY, D / 2f), new Vector3(sideW, WallH, Thick), mWall);
            AddBox(new Vector3( sideX, WallY, D / 2f), new Vector3(sideW, WallH, Thick), mWall);
            AddBox(new Vector3(0, lintelY, D / 2f), new Vector3(DoorW, lintelH, Thick), mWall);

            // 屋顶：四棱锥（CylinderMesh RadialSegments=4，TopRadius=0）
            // BottomRadius = W/2 * sqrt(2) ≈ 2.83 使四角顶点与房角对齐，再加0.2m出檐
            float roofR = W / 2f * Mathf.Sqrt(2f) + 0.2f;
            float roofH = 1.5f;
            float roofY = FlrH + WallH + roofH / 2f;

            var roofNode = new Node3D();
            roofNode.Position = new Vector3(0f, roofY, 0f);
            roofNode.RotationDegrees = new Vector3(0f, 45f, 0f);  // 对齐墙面

            var roofMesh = new MeshInstance3D();
            roofMesh.Mesh = new CylinderMesh
            {
                TopRadius      = 0f,
                BottomRadius   = roofR,
                Height         = roofH,
                RadialSegments = 4,
                Rings          = 1
            };
            roofMesh.MaterialOverride = mRoof;
            roofNode.AddChild(roofMesh);
            AddChild(roofNode);
        }

        private void BuildDefaultBox()
        {
            AddBox(Vector3.Zero, Vector3.One, MatWall());
        }

        // ── 工具 ──────────────────────────────────────────────────────

        private void AddBox(Vector3 pos, Vector3 size, StandardMaterial3D mat)
        {
            var m = new MeshInstance3D
            {
                Mesh             = new BoxMesh { Size = size },
                Position         = pos,
                MaterialOverride = mat
            };
            AddChild(m);
        }

        public override void _ExitTree()
        {
            if (EntityId >= 0) EcsWorld.Instance.DestroyEntity(EntityId);
        }
    }
}
