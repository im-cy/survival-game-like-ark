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
                DetectionRange = def.DetectionRange,
                AttackRange    = def.AttackRange,
                AttackPower    = def.BaseAttack,
                MoveSpeed      = def.BaseSpeed,
            });
            // Boss 不参与繁殖；普通生物出生时带繁殖组件（待驯服后才会激活）
            if (def.Tier != CreatureTier.Boss)
                EcsWorld.Instance.AddComponent(entityId, new BreedingComponent());

            // 直接创建视图节点（不依赖 PackedScene export）
            var view = new CreatureView();
            AddChild(view);
            view.Setup(entityId, def.SpriteSheet);

            // 应用体型缩放（Boss 比普通生物大）
            if (def.ViewScale != 1f)
                view.Scale = Vector3.One * def.ViewScale;

            GD.Print($"[CreatureSpawner] 生成 {def.DisplayName}（ID={entityId}）@ {worldPos}");
            return entityId;
        }

        public override void _ExitTree() => Instance = null;
    }
}
