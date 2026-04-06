using Godot;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Entities.Player
{
    /// <summary>
    /// 玩家 Sprite3D 动画驱动。
    /// 挂在 Player.tscn 的 Sprite3D 子节点上，
    /// 从父节点 Player 读取 ECS PositionComponent 驱动朝向与行走动画。
    ///
    /// 精灵表格式（提供美术时）：
    ///   列 = 朝向（0=下 1=左下 2=左 3=左上 … 共 FrameCountX 列）
    ///   行 = 行走帧（0=站立，1-N=行走循环）
    ///
    /// 无美术时显示纯色占位方块，游戏逻辑不受影响。
    /// </summary>
    public partial class PlayerView : Sprite3D
    {
        [Export] public int   FrameCountX = 4;      // 朝向列数（4向或8向）
        [Export] public int   FrameCountY = 4;      // 动画行数（含站立帧）
        [Export] public float FrameTime   = 0.12f;  // 每帧持续秒数

        private Player? _player;
        private float   _animTimer;
        private int     _currentRow;

        public override void _Ready()
        {
            Billboard  = BaseMaterial3D.BillboardModeEnum.Enabled;
            PixelSize  = 0.02f;
            _player    = GetParent<Player>();

            // 无贴图时用纯色方块占位（玩家可见）
            if (Texture == null)
            {
                var mat = new StandardMaterial3D();
                mat.AlbedoColor = new Color(0.2f, 0.5f, 1.0f);   // 蓝色代表玩家
                MaterialOverride = mat;
                // 不开 RegionEnabled，整个 Sprite 显示纯色
            }
        }

        public override void _Process(double delta)
        {
            if (_player == null || _player.EntityId < 0) return;

            var pos = EcsWorld.Instance.GetComponent<PositionComponent>(_player.EntityId);
            if (pos == null) return;

            if (Texture == null) return;   // 无贴图时跳过帧切换

            bool moving = pos.Velocity.LengthSquared() > 0.1f;
            int  col    = AngleToColumn(pos.FacingAngle);

            if (moving)
            {
                _animTimer += (float)delta;
                if (_animTimer >= FrameTime)
                {
                    _animTimer  = 0f;
                    _currentRow = (_currentRow + 1) % FrameCountY;
                    if (_currentRow == 0) _currentRow = 1;  // 跳过站立帧0
                }
            }
            else
            {
                _currentRow = 0;
                _animTimer  = 0f;
            }

            RegionEnabled = true;
            int fw = Texture.GetWidth()  / FrameCountX;
            int fh = Texture.GetHeight() / FrameCountY;
            RegionRect = new Rect2(col * fw, _currentRow * fh, fw, fh);
        }

        /// <summary>将 ECS 朝向角映射到精灵列索引。</summary>
        private int AngleToColumn(float angle)
        {
            float normalized = ((angle / Mathf.Tau) + 1f) % 1f;
            return (int)(normalized * FrameCountX) % FrameCountX;
        }
    }
}
