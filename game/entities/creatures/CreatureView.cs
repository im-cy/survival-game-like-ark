using Godot;
using SurvivalGame.Core.Data;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Systems;

namespace SurvivalGame.Entities.Creatures
{
    /// <summary>
    /// 生物视图节点 — 每个活跃生物对应一个 Node3D 实例。
    /// 通过 EntityId 从 ECS World 读取数据并驱动 Sprite3D Billboard。
    /// 无贴图时回退为胶囊体占位网格。
    /// </summary>
    public partial class CreatureView : Node3D
    {
        [Export] public int FrameCountX = 8;    // 水平：朝向数
        [Export] public int FrameCountY = 4;    // 垂直：动画帧数
        [Export] public float FrameTime = 0.15f;

        public int EntityId { get; set; } = -1;

        private Sprite3D? _sprite;
        private MeshInstance3D? _placeholder;
        private float _animTimer = 0f;
        private int _currentFrame = 0;

        public override void _Ready()
        {
            // Sprite3D（有贴图时使用）
            _sprite = new Sprite3D();
            _sprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _sprite.RegionEnabled = true;
            _sprite.PixelSize = 0.015f;
            _sprite.Visible = false;
            AddChild(_sprite);

            // 占位胶囊体（无贴图时显示）
            _placeholder = new MeshInstance3D();
            _placeholder.Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.0f };
            _placeholder.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.72f, 0.45f, 0.18f)   // 棕色猪
            };
            _placeholder.Position = new Vector3(0f, 0.5f, 0f);
            _placeholder.Visible = true;
            AddChild(_placeholder);
        }

        public void Setup(int entityId, Texture2D? spriteSheet)
        {
            EntityId = entityId;
            EventBus.Instance.Subscribe("creature_died",    OnCreatureDied);
            EventBus.Instance.Subscribe("creature_evolved", OnCreatureEvolved);

            // 应用物种专属颜色到占位体
            ApplySpeciesColor();

            if (spriteSheet != null && _sprite != null)
            {
                _sprite.Texture = spriteSheet;
                _sprite.Visible = true;
                if (_placeholder != null) _placeholder.Visible = false;
                UpdateRegion(0, 0);
            }
        }

        private void ApplySpeciesColor()
        {
            var stats = EcsWorld.Instance.GetComponent<CreatureStatsComponent>(EntityId);
            if (stats == null || _placeholder == null) return;
            var def = CreatureRegistry.Instance.Get(stats.SpeciesId);
            if (def != null && _placeholder.MaterialOverride is StandardMaterial3D mat)
                mat.AlbedoColor = def.ViewColor;
        }

        private void OnCreatureDied(object? payload)
        {
            if (payload is int id && id == EntityId)
                QueueFree();
        }

        private void OnCreatureEvolved(object? payload)
        {
            if (payload is not EvolutionEventData data || data.EntityId != EntityId) return;
            var def = CreatureRegistry.Instance.Get(data.NewSpeciesId);
            if (def == null) return;

            // 更新体型与颜色
            Scale = Vector3.One * def.ViewScale;
            if (_placeholder?.MaterialOverride is StandardMaterial3D mat)
                mat.AlbedoColor = def.ViewColor;

            GD.Print($"[CreatureView] EntityId={EntityId} 视觉更新为 {def.DisplayName}（×{def.ViewScale}）");
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("creature_died",    OnCreatureDied);
            EventBus.Instance.Unsubscribe("creature_evolved", OnCreatureEvolved);
        }

        public override void _Process(double delta)
        {
            if (EntityId < 0) return;

            var pos = EcsWorld.Instance.GetComponent<PositionComponent>(EntityId);
            if (pos == null) return;

            // 同步 3D 位置
            GlobalPosition = pos.Position;

            bool isMoving = pos.Velocity.LengthSquared() > 0.1f;

            if (_sprite != null && _sprite.Visible)
            {
                // 根据朝向选择精灵列
                int dirCol = AngleToDirectionColumn(pos.FacingAngle);

                // 播放行走动画
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
        }

        private void UpdateRegion(int col, int row)
        {
            if (_sprite?.Texture == null) return;
            int w = (int)_sprite.Texture.GetWidth()  / FrameCountX;
            int h = (int)_sprite.Texture.GetHeight() / FrameCountY;
            _sprite.RegionRect = new Rect2(col * w, row * h, w, h);
        }

        private int AngleToDirectionColumn(float angle)
        {
            float normalized = ((angle / Mathf.Tau) + 1f) % 1f;
            return (int)(normalized * FrameCountX) % FrameCountX;
        }
    }
}
