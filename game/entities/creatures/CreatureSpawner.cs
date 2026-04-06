using Godot;
using SurvivalGame.Core.ECS;
using SurvivalGame.Core.Data;

namespace SurvivalGame.Entities.Creatures
{
    /// <summary>
    /// 生物生成器 — 根据 CreatureDefinition 创建 ECS 实体并实例化视图节点。
    /// </summary>
    public partial class CreatureSpawner : Node
    {
        public static CreatureSpawner? Instance { get; private set; }

        [Export] public PackedScene CreatureViewScene = null!;

        public override void _Ready() => Instance = this;

        /// <summary>在指定世界坐标生成一只生物</summary>
        public int SpawnCreature(string speciesId, Vector3 worldPos)
        {
            var def = CreatureRegistry.Instance.Get(speciesId);
            if (def == null)
            {
                GD.PrintErr($"[CreatureSpawner] 未知生物: {speciesId}");
                return -1;
            }

            // 创建 ECS 实体
            int entityId = EcsWorld.Instance.CreateEntity();

            EcsWorld.Instance.AddComponent(entityId, new PositionComponent { Position = worldPos });
            EcsWorld.Instance.AddComponent(entityId, new HealthComponent { MaxHp = def.BaseHp, CurrentHp = def.BaseHp });
            EcsWorld.Instance.AddComponent(entityId, new TamingComponent
            {
                PreferredFood = def.PreferredFood,
                Method        = def.TamingMethod,
            });
            EcsWorld.Instance.AddComponent(entityId, new CreatureStatsComponent
            {
                SpeciesId = speciesId,
                CanRide   = def.CanRide,
                CanFly    = def.CanFly,
                HarvestResourceType = def.HarvestResourceType,
            });
            EcsWorld.Instance.AddComponent(entityId, new AIComponent
            {
                DetectionRange = 12f,
                AttackRange    = 2f,
            });

            // 实例化视图节点
            if (CreatureViewScene != null)
            {
                var view = CreatureViewScene.Instantiate<CreatureView>();
                view.Setup(entityId, def.SpriteSheet);
                AddChild(view);
            }

            return entityId;
        }

        public override void _ExitTree() => Instance = null;
    }
}
