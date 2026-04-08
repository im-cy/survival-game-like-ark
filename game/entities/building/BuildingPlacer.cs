using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;

namespace SurvivalGame.Entities.Building
{
    /// <summary>
    /// 建造预览与放置控制器。
    /// 进入建造模式后，跟随鼠标显示半透明预览，左键确认放置，右键取消。
    /// </summary>
    public partial class BuildingPlacer : Node3D
    {
        [Export] public float GridSize = 1f;

        private bool _buildMode = false;
        private string _currentPieceId = "";
        private Node3D? _preview;
        private Camera3D? _camera;

        // 营火和建造件的预制场景路径
        private static readonly Dictionary<string, string> PieceScenes = new()
        {
            ["campfire"]     = "res://scenes/Campfire.tscn",
            ["thatch_floor"] = "res://scenes/BuildingPiece.tscn",
            ["thatch_wall"]  = "res://scenes/BuildingPiece.tscn",
        };

        public override void _Ready()
        {
            EventBus.Instance.Subscribe("enter_build_mode", OnEnterBuildMode);
            EventBus.Instance.Subscribe("exit_build_mode",  _ => ExitBuildMode());
        }

        public override void _Process(double delta)
        {
            if (!_buildMode || _preview == null) return;

            // 延迟获取相机（场景加载后才可用）
            _camera ??= GetViewport().GetCamera3D();
            if (_camera == null) return;

            UpdatePreviewPosition();
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

        // ── 预览位置更新 ──────────────────────────────────────────────

        private void UpdatePreviewPosition()
        {
            var mousePos = GetViewport().GetMousePosition();
            var from = _camera!.ProjectRayOrigin(mousePos);
            var dir  = _camera.ProjectRayNormal(mousePos);

            if (Mathf.Abs(dir.Y) < 0.001f) return;
            float t = -from.Y / dir.Y;
            var hitPos = from + dir * t;

            _preview!.GlobalPosition = SnapToGrid(hitPos);
        }

        private Vector3 SnapToGrid(Vector3 pos) =>
            new(Mathf.Round(pos.X / GridSize) * GridSize, 0f,
                Mathf.Round(pos.Z / GridSize) * GridSize);

        // ── 确认放置 ──────────────────────────────────────────────────

        private void ConfirmPlacement()
        {
            if (_preview == null) return;

            var pieceDef = BuildingRegistry.Get(_currentPieceId);
            if (pieceDef == null) return;

            // 查找玩家实体
            int playerId = -1;
            foreach (var id in EcsWorld.Instance.Query<SurvivalComponent>()) { playerId = id; break; }
            if (playerId < 0) return;

            var inv = EcsWorld.Instance.GetComponent<InventoryComponent>(playerId);
            if (inv == null) return;

            // 材料检查
            foreach (var kvp in pieceDef.Materials)
            {
                if (inv.CountItem(kvp.Key) < kvp.Value)
                {
                    var itemDef = ItemRegistry.Instance.Get(kvp.Key);
                    string name = itemDef?.DisplayName ?? kvp.Key;
                    GD.Print($"[Build] 材料不足：需要 {kvp.Value}x {name}");
                    return;
                }
            }

            // 消耗材料
            foreach (var kvp in pieceDef.Materials)
                inv.RemoveItem(kvp.Key, kvp.Value);

            // 实例化节点
            PlacePiece(_currentPieceId, _preview.GlobalPosition);
        }

        private void PlacePiece(string pieceId, Vector3 position)
        {
            if (!PieceScenes.TryGetValue(pieceId, out var scenePath)) return;

            var packed = GD.Load<PackedScene>(scenePath);
            if (packed == null)
            {
                GD.PrintErr($"[Build] 找不到场景：{scenePath}");
                return;
            }

            var instance = packed.Instantiate<Node3D>();
            instance.GlobalPosition = position;

            // 建造件需要设置 PieceId
            if (instance is BuildingPiece bp)
                bp.PieceId = pieceId;

            GetTree().CurrentScene.AddChild(instance);

            var pieceDef = BuildingRegistry.Get(pieceId)!;
            GD.Print($"[Build] 放置了 {pieceDef.DisplayName} @ {position}");
        }

        // ── 建造模式进入/退出 ──────────────────────────────────────────

        private void OnEnterBuildMode(object? payload)
        {
            if (payload is not string pieceId) return;
            ExitBuildMode();        // 先清理旧预览

            _buildMode = true;
            _currentPieceId = pieceId;
            _preview = CreatePreviewNode(pieceId);
            if (_preview != null) AddChild(_preview);
        }

        private void ExitBuildMode()
        {
            _buildMode = false;
            _preview?.QueueFree();
            _preview = null;
        }

        // ── 预览网格生成 ──────────────────────────────────────────────

        private static Node3D CreatePreviewNode(string pieceId)
        {
            var root = new Node3D();
            var mesh = new MeshInstance3D();

            var mat = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor  = new Color(0.9f, 0.85f, 0.3f, 0.45f)
            };

            switch (pieceId)
            {
                case "campfire":
                    mesh.Mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.5f, Height = 0.4f };
                    mesh.Position = new Vector3(0f, 0.2f, 0f);
                    break;
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
            root.AddChild(mesh);
            return root;
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("enter_build_mode", OnEnterBuildMode);
        }
    }

    public record BuildPlaceData(string PieceId, Vector3 Position);
}
