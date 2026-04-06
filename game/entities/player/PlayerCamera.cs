using Godot;

namespace SurvivalGame.Entities.Player
{
    /// <summary>
    /// 正交相机控制器 — 跟随玩家，模拟饥荒风格的斜俯视角。
    /// 挂在独立 Node3D 上，通过代码跟踪玩家位置（不用 RemoteTransform）。
    /// </summary>
    public partial class PlayerCamera : Camera3D
    {
        [Export] public float ZoomSize    = 20f;        // 正交视口尺寸，越小越近
        [Export] public float PitchDeg    = -50f;       // 俯仰角（负数 = 向下看）
        [Export] public float HeightOffset = 0f;        // 相机高度偏移
        [Export] public float LerpSpeed   = 8f;         // 跟随平滑速度

        private Vector3 _offset;
        private Node3D? _target;

        public override void _Ready()
        {
            Projection = ProjectionType.Orthogonal;
            Size = ZoomSize;
            RotationDegrees = new Vector3(PitchDeg, 0f, 0f);

            // 根据仰角计算相机偏移，使焦点落在玩家脚下
            float rad = Mathf.DegToRad(Mathf.Abs(PitchDeg));
            _offset = new Vector3(0f, Mathf.Sin(rad) * 15f + HeightOffset, Mathf.Cos(rad) * 15f);
        }

        public override void _EnterTree()
        {
            // 在场景树中找玩家节点
            _target = GetTree().GetFirstNodeInGroup("player") as Node3D;
        }

        public override void _Process(double delta)
        {
            if (_target == null) return;
            var desired = _target.GlobalPosition + _offset;
            GlobalPosition = GlobalPosition.Lerp(desired, LerpSpeed * (float)delta);
        }

        // 供 UI 调用：调整缩放（鼠标滚轮缩放）
        public void AdjustZoom(float delta)
        {
            ZoomSize = Mathf.Clamp(ZoomSize - delta * 2f, 8f, 40f);
            Size = ZoomSize;
        }
    }
}
