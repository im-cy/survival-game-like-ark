using Godot;
using System.Collections.Generic;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;

namespace SurvivalGame.Entities.Building
{
    /// <summary>
    /// 已放置的建造节点。
    ///
    /// 功能：
    ///   1. 为每面墙 / 地板添加 StaticBody3D 碰撞，玩家只能经由门洞出入。
    ///   2. 房门可交互：Player 调用 TryInteractDoor(playerPos) 触发开/关动画。
    ///   3. 玩家在室内时，外墙 + 屋顶变半透明，离开后恢复不透明。
    /// </summary>
    public partial class BuildingPiece : Node3D
    {
        [Export] public string PieceId = "";

        public int EntityId { get; private set; } = -1;

        private const float DoorInteractRange = 2.5f;
        private const float HouseHalfSize     = 2.0f;   // house is 4×4m
        private const float HouseMaxY         = 4.0f;   // includes roof height

        // 门
        private Node3D?           _doorPivot;
        private CollisionShape3D? _doorCollision;
        private bool              _doorOpen = false;
        private Tween?            _doorTween;

        // 透视
        private readonly List<MeshInstance3D> _exteriorMeshes = new();
        private bool _exteriorTransparent = false;

        // ── 材质 ─────────────────────────────────────────────────────

        private static StandardMaterial3D MatThatch()      => new() { AlbedoColor = new Color(0.75f, 0.62f, 0.30f) };
        private static StandardMaterial3D MatThatchFloor() => new() { AlbedoColor = new Color(0.65f, 0.55f, 0.28f) };
        private static StandardMaterial3D MatThatchRoof()  => new() { AlbedoColor = new Color(0.48f, 0.36f, 0.13f) };
        private static StandardMaterial3D MatWood()        => new() { AlbedoColor = new Color(0.55f, 0.38f, 0.18f) };
        private static StandardMaterial3D MatWoodFloor()   => new() { AlbedoColor = new Color(0.48f, 0.32f, 0.14f) };
        private static StandardMaterial3D MatWoodRoof()    => new() { AlbedoColor = new Color(0.38f, 0.24f, 0.09f) };

        // ─────────────────────────────────────────────────────────────

        public override void _Ready()
        {
            EventBus.Instance.Subscribe("building_destroyed", OnBuildingDestroyed);
            AddToGroup("building_pieces");

            switch (PieceId)
            {
                case "thatch_house": BuildHouse(isWood: false); break;
                case "wood_house":   BuildHouse(isWood: true);  break;
                default:             BuildCampfire();            break;
            }

            var def = BuildingRegistry.Get(PieceId);
            float hp = def?.HpMax ?? 200f;
            EntityId = EcsWorld.Instance.CreateEntity();
            EcsWorld.Instance.AddComponent(EntityId, new PositionComponent { Position = GlobalPosition });
            EcsWorld.Instance.AddComponent(EntityId, new BuildingPieceComponent
            {
                PieceId   = PieceId,
                HpMax     = hp,
                HpCurrent = hp,
                Tier      = def?.Tier     ?? BuildingTier.Thatch,
                PieceType = def?.PieceType ?? BuildingPieceType.House,
                IsStable  = true,
            });
        }

        public override void _Process(double delta)
        {
            if (_exteriorMeshes.Count == 0) return;
            SetExteriorTransparency(IsPlayerInside());
        }

        // ── 门交互（Player 调用）──────────────────────────────────────

        /// <summary>
        /// 如果 playerPos 在门交互范围内则开/关门，返回 true 表示已处理。
        /// </summary>
        public bool TryInteractDoor(Vector3 playerPos)
        {
            if (_doorPivot == null) return false;
            var doorWorld = GlobalPosition + new Vector3(0f, 1f, HouseHalfSize);
            if (playerPos.DistanceTo(doorWorld) > DoorInteractRange) return false;
            ToggleDoor();
            return true;
        }

        // ── 房屋建造 ─────────────────────────────────────────────────

        private void BuildHouse(bool isWood)
        {
            const float W     = 4f;
            const float D     = 4f;
            const float WallH = 2.5f;
            const float FlrH  = 0.2f;
            const float DoorW = 1.0f;
            const float DoorH = 2.0f;
            float thick = isWood ? 0.3f : 0.25f;
            float wallY = FlrH + WallH / 2f;

            var mFloor = isWood ? MatWoodFloor() : MatThatchFloor();
            var mWall  = isWood ? MatWood()      : MatThatch();
            var mRoof  = isWood ? MatWoodRoof()  : MatThatchRoof();

            // 地板
            AddStructBox(new Vector3(0f, FlrH / 2f, 0f), new Vector3(W, FlrH, D), mFloor, exterior: false);

            // 北墙（完整）
            AddStructBox(new Vector3(0f, wallY, -D / 2f), new Vector3(W, WallH, thick), mWall, exterior: true);

            // 东 / 西墙（完整）
            AddStructBox(new Vector3( W / 2f, wallY, 0f), new Vector3(thick, WallH, D), mWall, exterior: true);
            AddStructBox(new Vector3(-W / 2f, wallY, 0f), new Vector3(thick, WallH, D), mWall, exterior: true);

            // 南墙（左段 + 右段 + 门楣）
            float sideW   = (W - DoorW) / 2f;
            float sideX   = W / 2f - sideW / 2f;
            float lintelH = WallH - DoorH;
            float lintelY = FlrH + DoorH + lintelH / 2f;
            AddStructBox(new Vector3(-sideX, wallY,   D / 2f), new Vector3(sideW, WallH, thick),   mWall, exterior: true);
            AddStructBox(new Vector3( sideX, wallY,   D / 2f), new Vector3(sideW, WallH, thick),   mWall, exterior: true);
            AddStructBox(new Vector3(0f,     lintelY, D / 2f), new Vector3(DoorW, lintelH, thick), mWall, exterior: true);

            // 屋顶
            BuildPyramidRoof(FlrH + WallH, W, mRoof);

            // 门
            BuildDoor(FlrH, DoorW, DoorH, thick, D / 2f, mWall);
        }

        // ── 门 ───────────────────────────────────────────────────────

        private void BuildDoor(float flrH, float doorW, float doorH, float thick, float doorZ,
                                StandardMaterial3D wallMat)
        {
            // 铰链在门洞右侧（从外往里看为 +X 方向）
            float hingeX = doorW / 2f;
            float doorCY = flrH + doorH / 2f;

            _doorPivot = new Node3D { Position = new Vector3(hingeX, doorCY, doorZ) };

            // 门板（相对铰链偏 -doorW/2 = 中心在 -doorW/2 处）
            var doorMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(
                    wallMat.AlbedoColor.R * 0.75f,
                    wallMat.AlbedoColor.G * 0.75f,
                    wallMat.AlbedoColor.B * 0.75f)
            };
            _doorPivot.AddChild(new MeshInstance3D
            {
                Mesh             = new BoxMesh { Size = new Vector3(doorW, doorH, thick * 0.6f) },
                Position         = new Vector3(-doorW / 2f, 0f, 0f),
                MaterialOverride = doorMat,
            });

            // 门的碰撞
            var doorBody = new StaticBody3D { Position = new Vector3(-doorW / 2f, 0f, 0f) };
            _doorCollision = new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(doorW, doorH, thick * 0.6f) },
            };
            doorBody.AddChild(_doorCollision);
            _doorPivot.AddChild(doorBody);

            AddChild(_doorPivot);
        }

        private void ToggleDoor()
        {
            _doorOpen = !_doorOpen;

            // 碰撞立即切换（SetDeferred 保证物理帧安全）
            if (_doorCollision != null)
                _doorCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, _doorOpen);

            // 动画：绕铰链（Y 轴）旋转
            _doorTween?.Kill();
            _doorTween = CreateTween();
            _doorTween
                .TweenProperty(_doorPivot, "rotation_degrees:y", _doorOpen ? 90f : 0f, 0.25f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
        }

        // ── 屋顶 ─────────────────────────────────────────────────────

        private void BuildPyramidRoof(float baseY, float footprint, StandardMaterial3D mat)
        {
            float roofR = footprint / 2f * Mathf.Sqrt(2f) + 0.2f;
            float roofH = 1.5f;
            var pivot = new Node3D
            {
                Position         = new Vector3(0f, baseY + roofH / 2f, 0f),
                RotationDegrees  = new Vector3(0f, 45f, 0f),
            };
            var roofMesh = new MeshInstance3D
            {
                Mesh = new CylinderMesh
                {
                    TopRadius = 0f, BottomRadius = roofR,
                    Height = roofH, RadialSegments = 4, Rings = 1,
                },
                MaterialOverride = mat,
            };
            pivot.AddChild(roofMesh);
            AddChild(pivot);
            _exteriorMeshes.Add(roofMesh);   // 屋顶参与透视
        }

        // ── 营火 ─────────────────────────────────────────────────────

        private void BuildCampfire()
        {
            var baseMat = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.22f, 0.10f) };
            var logMat  = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.28f, 0.12f) };
            AddStructBox(new Vector3(0f, 0.15f, 0f), new Vector3(0.8f, 0.3f, 0.8f),   baseMat, exterior: false);
            AddStructBox(new Vector3(0f, 0.35f, 0f), new Vector3(0.6f, 0.12f, 0.15f), logMat,  exterior: false);
            AddStructBox(new Vector3(0f, 0.35f, 0f), new Vector3(0.15f, 0.12f, 0.6f), logMat,  exterior: false);
        }

        // ── 室内透视 ─────────────────────────────────────────────────

        private bool IsPlayerInside()
        {
            foreach (var id in EcsWorld.Instance.Query<SurvivalComponent>())
            {
                var pc = EcsWorld.Instance.GetComponent<PositionComponent>(id);
                if (pc == null) continue;
                var local = pc.Position - GlobalPosition;
                return local.X > -HouseHalfSize && local.X < HouseHalfSize
                    && local.Z > -HouseHalfSize && local.Z < HouseHalfSize
                    && local.Y >= 0f && local.Y < HouseMaxY;
            }
            return false;
        }

        private void SetExteriorTransparency(bool transparent)
        {
            if (_exteriorTransparent == transparent) return;
            _exteriorTransparent = transparent;

            foreach (var mi in _exteriorMeshes)
            {
                if (mi.MaterialOverride is not StandardMaterial3D mat) continue;
                if (transparent)
                {
                    mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    mat.AlbedoColor  = new Color(mat.AlbedoColor.R, mat.AlbedoColor.G, mat.AlbedoColor.B, 0.2f);
                }
                else
                {
                    mat.Transparency = BaseMaterial3D.TransparencyEnum.Disabled;
                    mat.AlbedoColor  = new Color(mat.AlbedoColor.R, mat.AlbedoColor.G, mat.AlbedoColor.B, 1f);
                }
            }
        }

        // ── 工具 ──────────────────────────────────────────────────────

        /// <summary>添加带网格和碰撞的盒体。</summary>
        private void AddStructBox(Vector3 pos, Vector3 size, StandardMaterial3D mat, bool exterior)
        {
            // 网格
            var mi = new MeshInstance3D
            {
                Mesh             = new BoxMesh { Size = size },
                Position         = pos,
                MaterialOverride = mat,
            };
            AddChild(mi);
            if (exterior) _exteriorMeshes.Add(mi);

            // 碰撞
            var body = new StaticBody3D { Position = pos };
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            AddChild(body);
        }

        // ── 事件 ──────────────────────────────────────────────────────

        private void OnBuildingDestroyed(object? payload)
        {
            if (payload is int id && id == EntityId)
                CallDeferred(Node.MethodName.QueueFree);
        }

        public override void _ExitTree()
        {
            EventBus.Instance.Unsubscribe("building_destroyed", OnBuildingDestroyed);
            if (EntityId >= 0) EcsWorld.Instance.DestroyEntity(EntityId);
        }
    }
}
