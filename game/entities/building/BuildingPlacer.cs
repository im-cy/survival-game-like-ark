using Godot;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Entities.Building
{
    /// <summary>
    /// 建造预览与放置控制器。
    /// 进入建造模式后，跟随鼠标显示半透明预览，点击确认放置。
    /// </summary>
    public partial class BuildingPlacer : Node3D
    {
        [Export] public float SnapDistance = 2f;    // 吸附检测距离
        [Export] public float GridSize = 1f;        // 无吸附时对齐到网格

        private bool _buildMode = false;
        private string _currentPieceId = "";
        private Node3D? _preview;
        private Camera3D? _camera;

        public override void _Ready()
        {
            _camera = GetTree().GetFirstNodeInGroup("main_camera") as Camera3D;
            EventBus.Instance.Subscribe("enter_build_mode", OnEnterBuildMode);
            EventBus.Instance.Subscribe("exit_build_mode",  _ => ExitBuildMode());
        }

        public override void _Process(double delta)
        {
            if (!_buildMode || _preview == null || _camera == null) return;
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

        private void UpdatePreviewPosition()
        {
            var mousePos = GetViewport().GetMousePosition();
            var from = _camera!.ProjectRayOrigin(mousePos);
            var dir  = _camera.ProjectRayNormal(mousePos);

            // 射线与 Y=0 平面求交
            if (Mathf.Abs(dir.Y) < 0.001f) return;
            float t = -from.Y / dir.Y;
            var hitPos = from + dir * t;

            // 对齐到网格
            hitPos = SnapToGrid(hitPos);
            _preview!.GlobalPosition = hitPos;
        }

        private Vector3 SnapToGrid(Vector3 pos)
        {
            return new Vector3(
                Mathf.Round(pos.X / GridSize) * GridSize,
                0f,
                Mathf.Round(pos.Z / GridSize) * GridSize);
        }

        private void ConfirmPlacement()
        {
            if (_preview == null) return;
            // TODO: 调用 BuildingSystem.TryPlace 验证并消耗材料
            EventBus.Instance.Emit("building_placed", new BuildPlaceData(_currentPieceId, _preview.GlobalPosition));
        }

        private void OnEnterBuildMode(object? payload)
        {
            if (payload is not string pieceId) return;
            _buildMode = true;
            _currentPieceId = pieceId;
            // TODO: 实例化对应预览 mesh
        }

        private void ExitBuildMode()
        {
            _buildMode = false;
            _preview?.QueueFree();
            _preview = null;
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("enter_build_mode", OnEnterBuildMode);
        }
    }

    public record BuildPlaceData(string PieceId, Vector3 Position);
}
