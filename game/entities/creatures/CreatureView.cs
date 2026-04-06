using Godot;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Entities.Creatures
{
    /// <summary>
    /// 生物视图节点 — 每个活跃生物对应一个 Node3D 实例。
    /// 通过 EntityId 从 ECS World 读取数据并驱动 Sprite3D Billboard。
    /// </summary>
    public partial class CreatureView : Node3D
    {
        [Export] public int FrameCountX = 8;    // 水平：朝向数
        [Export] public int FrameCountY = 4;    // 垂直：动画帧数
        [Export] public float FrameTime = 0.15f;

        public int EntityId { get; set; } = -1;

        private Sprite3D _sprite = null!;
        private float _animTimer = 0f;
        private int _currentFrame = 0;

        public override void _Ready()
        {
            _sprite = new Sprite3D();
            _sprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _sprite.RegionEnabled = true;
            _sprite.PixelSize = 0.02f;  // 调整纸片人大小
            AddChild(_sprite);
        }

        public void Setup(int entityId, Texture2D? spriteSheet)
        {
            EntityId = entityId;
            if (spriteSheet != null)
            {
                _sprite.Texture = spriteSheet;
                UpdateRegion(0, 0);
            }
        }

        public override void _Process(double delta)
        {
            if (EntityId < 0) return;

            var pos = World.Instance.GetComponent<PositionComponent>(EntityId);
            if (pos == null) return;

            // 同步 3D 位置
            GlobalPosition = pos.Position;

            // 根据朝向选择精灵列
            int dirCol = AngleToDirectionColumn(pos.FacingAngle);

            // 播放行走动画
            bool isMoving = pos.Velocity.LengthSquared() > 0.1f;
            if (isMoving)
            {
                _animTimer += (float)delta;
                if (_animTimer >= FrameTime)
                {
                    _animTimer = 0f;
                    _currentFrame = (_currentFrame + 1) % FrameCountY;
                }
            }
            else
            {
                _currentFrame = 0;
            }

            UpdateRegion(dirCol, _currentFrame);
        }

        private void UpdateRegion(int col, int row)
        {
            if (_sprite.Texture == null) return;
            int w = (int)_sprite.Texture.GetWidth()  / FrameCountX;
            int h = (int)_sprite.Texture.GetHeight() / FrameCountY;
            _sprite.RegionRect = new Rect2(col * w, row * h, w, h);
        }

        private int AngleToDirectionColumn(float angle)
        {
            // 将朝向角度映射到 0..FrameCountX-1
            float normalized = ((angle / Mathf.Tau) + 1f) % 1f;
            return (int)(normalized * FrameCountX) % FrameCountX;
        }
    }
}
