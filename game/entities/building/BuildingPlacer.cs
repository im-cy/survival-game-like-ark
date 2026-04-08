using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;

namespace SurvivalGame.Entities.Building
{
    /// <summary>
    /// 建造预览与放置控制器。
    /// 进入建造模式后跟随鼠标显示半透明预览，左键确认放置，右键取消。
    ///
    /// 房屋  — 4m 网格对齐，绿色=可放，红色=已被占用。
    /// 功能件 — 1m 网格。
    /// </summary>
    public partial class BuildingPlacer : Node3D
    {
        private const float HouseGrid   = 4f;
        private const float SpecialGrid = 1f;

        private static readonly Color ColValid   = new(0.3f, 0.9f, 0.3f, 0.45f);
        private static readonly Color ColInvalid = new(0.9f, 0.2f, 0.2f, 0.45f);

        private bool _buildMode = false;
        private string _currentPieceId = "";
        private Node3D? _preview;
        private StandardMaterial3D? _previewMat;
        private Camera3D? _camera;
        private SnapResult _currentSnap;

        public override void _Ready()
        {
            EventBus.Instance.Subscribe("enter_build_mode", OnEnterBuildMode);
            EventBus.Instance.Subscribe("exit_build_mode",  _ => ExitBuildMode());
        }

        public override void _Process(double delta)
        {
            if (!_buildMode || _preview == null) return;
            _camera ??= GetViewport().GetCamera3D();
            if (_camera == null) return;
            UpdatePreview();
        }

        public override void _Input(InputEvent @event)
        {
            if (!_buildMode) return;
            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left)  ConfirmPlacement();
                if (mb.ButtonIndex == MouseButton.Right) ExitBuildMode();
            }
        }

        // ── 预览更新 ──────────────────────────────────────────────────

        private void UpdatePreview()
        {
            var mousePos = GetViewport().GetMousePosition();
            var from = _camera!.ProjectRayOrigin(mousePos);
            var dir  = _camera.ProjectRayNormal(mousePos);
            if (Mathf.Abs(dir.Y) < 0.001f) return;

            float t = -from.Y / dir.Y;
            var hitPos = from + dir * t;

            _currentSnap = ComputeSnap(_currentPieceId, hitPos);
            _preview!.GlobalPosition = _currentSnap.Position;

            if (_previewMat != null)
                _previewMat.AlbedoColor = _currentSnap.IsValid ? ColValid : ColInvalid;
        }

        // ── 吸附计算 ──────────────────────────────────────────────────

        private static SnapResult ComputeSnap(string pieceId, Vector3 rawPos)
        {
            var def = BuildingRegistry.Get(pieceId);
            float grid = def?.PieceType == BuildingPieceType.House ? HouseGrid : SpecialGrid;
            var pt = def?.PieceType ?? BuildingPieceType.House;

            float x = Mathf.Round(rawPos.X / grid) * grid;
            float z = Mathf.Round(rawPos.Z / grid) * grid;
            var pos = new Vector3(x, 0f, z);

            bool ok = !IsOccupied(pos, pt);
            return new SnapResult(pos, 0f, true, ok);
        }

        private static bool IsOccupied(Vector3 pos, BuildingPieceType pt)
        {
            const float Eps = 0.25f;
            foreach (var eid in EcsWorld.Instance.Query<BuildingPieceComponent>())
            {
                var bpc = EcsWorld.Instance.GetComponent<BuildingPieceComponent>(eid)!;
                if (bpc.PieceType != pt) continue;
                var pc = EcsWorld.Instance.GetComponent<PositionComponent>(eid);
                if (pc == null) continue;
                if ((pc.Position - pos).LengthSquared() < Eps * Eps) return true;
            }
            return false;
        }

        // ── 确认放置 ──────────────────────────────────────────────────

        private void ConfirmPlacement()
        {
            if (_preview == null || !_currentSnap.IsValid) return;

            var def = BuildingRegistry.Get(_currentPieceId);
            if (def == null) return;

            int playerId = -1;
            foreach (var id in EcsWorld.Instance.Query<SurvivalComponent>()) { playerId = id; break; }
            if (playerId < 0) return;

            var inv = EcsWorld.Instance.GetComponent<InventoryComponent>(playerId);
            if (inv == null) return;

            foreach (var kvp in def.Materials)
            {
                if (inv.CountItem(kvp.Key) >= kvp.Value) continue;
                var itemDef = ItemRegistry.Instance.Get(kvp.Key);
                GD.Print($"[Build] 材料不足：需要 {kvp.Value}x {itemDef?.DisplayName ?? kvp.Key}");
                return;
            }

            foreach (var kvp in def.Materials)
                inv.RemoveItem(kvp.Key, kvp.Value);

            SpawnPiece(_currentPieceId, _currentSnap.Position);
        }

        private void SpawnPiece(string pieceId, Vector3 position)
        {
            var node = new BuildingPiece { PieceId = pieceId };
            GetTree().CurrentScene.AddChild(node);
            node.GlobalPosition = position;

            var def = BuildingRegistry.Get(pieceId)!;
            GD.Print($"[Build] 放置 {def.DisplayName} @ {position:F1}");
        }

        // ── 建造模式 ──────────────────────────────────────────────────

        private void OnEnterBuildMode(object? payload)
        {
            if (payload is not string pieceId) return;
            ExitBuildMode();
            _buildMode      = true;
            _currentPieceId = pieceId;
            _preview        = CreatePreviewNode(pieceId, out _previewMat);
            if (_preview != null) AddChild(_preview);
        }

        private void ExitBuildMode()
        {
            _buildMode = false;
            _preview?.QueueFree();
            _preview    = null;
            _previewMat = null;
        }

        // ── 预览网格 ──────────────────────────────────────────────────

        private static Node3D CreatePreviewNode(string pieceId, out StandardMaterial3D mat)
        {
            mat = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor  = ColValid,
            };

            var root = new Node3D();
            var def  = BuildingRegistry.Get(pieceId);

            switch (pieceId)
            {
                case "thatch_house":
                case "wood_house":
                    // 地板轮廓
                    AddPreviewBox(root, new Vector3(0f, 0.1f,  0f), new Vector3(4f, 0.2f, 4f),   mat);
                    // 四面墙轮廓（薄）
                    AddPreviewBox(root, new Vector3(0f, 1.45f, -2f), new Vector3(4f, 2.5f, 0.15f), mat);
                    AddPreviewBox(root, new Vector3(0f, 1.45f,  2f), new Vector3(4f, 2.5f, 0.15f), mat);
                    AddPreviewBox(root, new Vector3(-2f, 1.45f, 0f), new Vector3(0.15f, 2.5f, 4f), mat);
                    AddPreviewBox(root, new Vector3( 2f, 1.45f, 0f), new Vector3(0.15f, 2.5f, 4f), mat);
                    // 屋顶（四棱锥）
                    var roofNode = new Node3D { Position = new Vector3(0f, 4.0f, 0f) };
                    roofNode.RotationDegrees = new Vector3(0f, 45f, 0f);
                    roofNode.AddChild(new MeshInstance3D
                    {
                        Mesh = new CylinderMesh
                        {
                            TopRadius = 0f, BottomRadius = Mathf.Sqrt(2f) * 2f + 0.2f,
                            Height = 1.5f, RadialSegments = 4, Rings = 1,
                        },
                        MaterialOverride = mat,
                    });
                    root.AddChild(roofNode);
                    break;

                case "campfire":
                    AddPreviewBox(root, new Vector3(0f, 0.2f, 0f), new Vector3(0.8f, 0.4f, 0.8f), mat);
                    break;

                default:
                    AddPreviewBox(root, new Vector3(0f, 0.5f, 0f), Vector3.One, mat);
                    break;
            }

            return root;
        }

        private static void AddPreviewBox(Node3D parent, Vector3 pos, Vector3 size, StandardMaterial3D mat)
        {
            parent.AddChild(new MeshInstance3D
            {
                Mesh             = new BoxMesh { Size = size },
                Position         = pos,
                MaterialOverride = mat,
            });
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("enter_build_mode", OnEnterBuildMode);
        }
    }

    public record struct SnapResult(Vector3 Position, float RotY, bool IsSnapped, bool IsValid);
    public record BuildPlaceData(string PieceId, Vector3 Position);
}
