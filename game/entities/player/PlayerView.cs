using Godot;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Entities.Player
{
    /// <summary>
    /// 玩家 Sprite3D 动画驱动。
    /// 精灵表标准格式：
    ///   行（Y） = 朝向方向，共 FrameCountY 行
    ///             行 0 = 下/面朝摄像机
    ///             行 1 = 左
    ///             行 2 = 右
    ///             行 3 = 上/背对摄像机
    ///             （行数不足时统一用行 0）
    ///   列（X） = 行走帧，共 FrameCountX 列
    ///             列 0 = 站立帧，列 1..N-1 = 行走循环
    /// 无美术时显示纯色占位方块，逻辑不受影响。
    /// </summary>
    public partial class PlayerView : Sprite3D
    {
        [Export] public int   FrameCountX      = 6;     // 精灵表列数（行走帧数）
        [Export] public int   FrameCountY      = 6;     // 精灵表行数（朝向数）
        [Export] public float FrameTime        = 0.15f; // 每帧持续秒数
        /// <summary>
        /// 是否按朝向切换行。
        /// 仅当精灵表真正按 下/左/右/上 分行时才开启，
        /// 否则保持 false 始终用第 0 行。
        /// </summary>
        [Export] public bool  DirectionalRows  = false;

        private Player? _player;
        private float   _animTimer;
        private int     _currentCol;    // 当前行走帧（列索引）

        public override void _Ready()
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            PixelSize = 0.02f;
            _player   = GetParent<Player>();

            if (Texture == null)
            {
                var mat = new StandardMaterial3D();
                mat.AlbedoColor = new Color(0.2f, 0.5f, 1.0f);
                MaterialOverride = mat;
            }
        }

        public override void _Process(double delta)
        {
            if (_player == null || _player.EntityId < 0) return;

            var pos = EcsWorld.Instance.GetComponent<PositionComponent>(_player.EntityId);
            if (pos == null) return;

            if (Texture == null) return;

            bool moving = pos.Velocity.LengthSquared() > 0.1f;

            // ── 行走帧（列）────────────────────────────────────────────────
            if (moving)
            {
                _animTimer += (float)delta;
                if (_animTimer >= FrameTime)
                {
                    _animTimer = 0f;
                    // 列 0 留给站立帧，行走循环从列 1 开始
                    int walkFrames = Mathf.Max(1, FrameCountX - 1);
                    _currentCol = (_currentCol % walkFrames) + 1;
                }
            }
            else
            {
                _currentCol = 0;    // 站立
                _animTimer  = 0f;
            }

            // ── 朝向行（行）────────────────────────────────────────────────
            int dirRow = DirectionalRows ? AngleToRow(pos.FacingAngle) : 0;

            // ── RegionRect（float 精度，避免整除截断）──────────────────────
            RegionEnabled = true;
            float fw = (float)Texture.GetWidth()  / FrameCountX;
            float fh = (float)Texture.GetHeight() / FrameCountY;
            RegionRect = new Rect2(
                Mathf.RoundToInt(_currentCol * fw),
                Mathf.RoundToInt(dirRow      * fh),
                Mathf.RoundToInt(fw),
                Mathf.RoundToInt(fh));
        }

        /// <summary>
        /// 将朝向角（Atan2(X,Z)）映射到精灵行：
        /// 0=下  1=左  2=右  3=上
        /// 行数不足 4 时统一返回行 0（只显示正面）。
        /// </summary>
        private int AngleToRow(float angle)
        {
            if (FrameCountY < 2) return 0;

            // 归一化到 0..2π
            float a = ((angle % Mathf.Tau) + Mathf.Tau) % Mathf.Tau;

            // 4向判断：每扇形 π/2
            int dir;
            if      (a < Mathf.Pi * 0.25f || a >= Mathf.Pi * 1.75f) dir = 0; // 下
            else if (a < Mathf.Pi * 0.75f)                           dir = 2; // 右
            else if (a < Mathf.Pi * 1.25f)                           dir = 3; // 上
            else                                                      dir = 1; // 左

            return Mathf.Min(dir, FrameCountY - 1);
        }
    }
}
