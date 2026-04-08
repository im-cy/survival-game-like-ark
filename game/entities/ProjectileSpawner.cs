using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;

namespace SurvivalGame.Entities
{
    /// <summary>
    /// 弹射物视觉管理器 — 监听 projectile_fired / projectile_expired 事件，
    /// 为每枚箭矢创建简单的 3D 网格节点并每帧同步位置。
    /// </summary>
    public partial class ProjectileSpawner : Node3D
    {
        private readonly Dictionary<int, Node3D> _views = new();

        public override void _Ready()
        {
            EventBus.Instance.Subscribe("projectile_fired",   OnFired);
            EventBus.Instance.Subscribe("projectile_expired", OnExpired);
        }

        private void OnFired(object? payload)
        {
            if (payload is not int projId) return;

            var pos  = EcsWorld.Instance.GetComponent<PositionComponent>(projId);
            var proj = EcsWorld.Instance.GetComponent<ProjectileComponent>(projId);
            if (pos == null || proj == null) return;

            var view = new Node3D();
            view.Position = pos.Position + Vector3.Up * 0.5f;

            // 箭矢：棕色细长胶囊（默认竖立，旋转 90° 改为水平）
            var mesh     = new MeshInstance3D();
            var capsule  = new CapsuleMesh { Radius = 0.05f, Height = 0.45f };
            mesh.Mesh    = capsule;
            var mat      = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.35f, 0.10f) };
            mesh.MaterialOverride = mat;
            mesh.RotationDegrees  = new Vector3(90f, 0f, 0f);   // 使胶囊沿 Z 轴延伸
            view.AddChild(mesh);

            // 朝向飞行方向
            if (proj.Direction.LengthSquared() > 0.01f)
                view.Rotation = new Vector3(0f, Mathf.Atan2(proj.Direction.X, proj.Direction.Z), 0f);

            AddChild(view);
            _views[projId] = view;
        }

        public override void _Process(double delta)
        {
            foreach (var (projId, view) in _views)
            {
                if (!EcsWorld.Instance.EntityExists(projId)) continue;
                var pos = EcsWorld.Instance.GetComponent<PositionComponent>(projId);
                if (pos == null) continue;
                view.Position = pos.Position + Vector3.Up * 0.5f;
            }
        }

        private void OnExpired(object? payload)
        {
            if (payload is not int projId) return;
            if (_views.TryGetValue(projId, out var view))
            {
                view.QueueFree();
                _views.Remove(projId);
            }
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("projectile_fired",   OnFired);
            EventBus.Instance.Unsubscribe("projectile_expired", OnExpired);
        }
    }
}
