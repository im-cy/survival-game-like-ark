# 俯视2.5D生存游戏 — 完整设计方案

> 技术栈：Godot 4 · 3D场景 + 正交相机 · Sprite3D Billboard 纸片人

---

# PART 1：玩法设计文档 (GDD)

---

## 1.1 游戏定位

**核心体验：** 在一片未知大陆上，玩家从一无所有开始，驯服各种生物作为伙伴，建立家园，探索世界，对抗自然与神秘力量。

**三大设计支柱：**
- **发现** — 每次游玩都有新的地图、生物和秘密
- **羁绊** — 驯服的生物有个性、成长、情感，不只是工具
- **建造** — 基地是玩家意志的体现，从茅草屋到钢铁要塞

---

## 1.2 核心循环

```
【第一层 - 分钟级循环】
  采集（石头/木材/食物）→ 制作工具 → 用工具采集更多

【第二层 - 小时级循环】
  驯服生物 → 协助采集/战斗 → 解锁新区域 → 击败Boss

【第三层 - 长期循环】
  科技树解锁 → 建造升级 → 繁殖优良品种 → 挑战终局Boss
```

---

## 1.3 世界设计

### 生物群系（Biome）

| 群系 | 视觉风格 | 特色资源 | 代表生物 |
|---|---|---|---|
| **温带森林** | 起始区，绿色地形 | 木材、浆果、燧石 | 野猪、鹿、松鼠 |
| **草原** | 开阔平坦，风吹草动 | 纤维、骨头、皮革 | 野马、牛、猛禽 |
| **沙漠** | 高温压力，日夜温差大 | 沙石、仙人掌、矿脉 | 蝎子、骆驼、沙虫 |
| **雪原** | 持续寒冷压力 | 皮毛、冰晶、珍稀矿 | 猛犸象、雪狼、冰熊 |
| **熔岩地带** | 极热压力，危险高 | 熔岩石、稀有金属 | 火蜥蜴、岩石巨人 |
| **深林腹地** | 黑暗、隐藏Boss入口 | 古代木材、神秘植物 | 古树精、夜行兽 |

### 地图生成

- 世界以 **种子（Seed）** 生成，64×64 个 Chunk，每个 Chunk 32×32 米
- 玩家出生在温带森林中心区域
- 各群系通过 Voronoi 图 + 温度/湿度噪声自然过渡
- 特殊地点（遗迹、洞穴、Boss祭坛）按密度规则程序化分布

---

## 1.4 生存系统

### 属性面板

```
玩家核心属性：
  ❤ 生命值      —— 归零死亡，可通过食物/床铺恢复
  🍖 饥饿值      —— 被动消耗，归零后扣血
  💧 口渴值      —— 消耗比饥饿略快，归零后扣血更快
  🌡 体温        —— 高/低于阈值持续扣血
  ⚡ 耐力        —— 奔跑/战斗/游泳消耗，耗尽后惩罚
  📦 负重        —— 超重后移速递减，100%时无法移动
```

### 体温系统

```
影响因素：
  + 热：生物群系基础温度、白天时间、营火范围、厚实衣物
  - 冷：生物群系基础温度、夜间、下雨/雪、入水

平衡方式：
  玩家通过 衣物层数 + 营火 + 庇护所 来对抗极端环境
  驯服的生物可以作为特殊buff来源（如骆驼提供沙漠降温buff）
```

### 死亡与惩罚

- 死亡后保留**背包外装备**，背包物品掉落在原地（60秒内可取回）
- 有驯服生物守护背包时，掉落物不会消失
- 可制作**记忆石**，记录一次安全点，死亡后传送到该点（消耗品）

---

## 1.5 驯养系统（核心玩法）

### 驯养方式三类

**① 喂食驯养（被动型）**
- 手持对应食物，缓慢靠近，等待进食动画
- 每次喂食提升信任度，达到100%完成驯养
- 被攻击/快速移动会降低信任度，失败需重来

**② 击晕驯养（技术型）**
- 用麻醉箭/麻醉陷阱击晕生物
- 在昏迷状态下持续投喂偏好食物
- 麻醉值会随时间降低，需补充麻醉药维持昏迷

**③ 挑战驯养（特殊型）**
- 部分稀有生物需完成特定小游戏
- Boss级生物需先击败再驯养

### 驯养后成长系统

```
每只驯服生物有：
  - 等级（1-100），每级获得1点属性点
  - 属性：生命、攻击、速度、负重、采集效率
  - 忠诚度（需定期喂食维持，归零后离队）
  - 特性词条（随机2条）：如"勤劳"（采集+20%）、"护主"（玩家受伤时攻击+50%）

驯服后指令系统：
  - 跟随 / 原地等待 / 守卫范围
  - 自动采集（指定资源类型）
  - 骑乘（大型生物）
```

### 生物繁殖

- 两只驯服同种生物（♂+♀），建造**繁殖窝**后可配对
- 后代随机继承双亲各50%的属性，有小概率变异（+5%某属性）
- 幼崽需手动喂食成长，培育出属性更强的下一代

### 生物分级

| 等级 | 代表生物 | 驯养难度 | 核心用途 |
|---|---|---|---|
| F | 鸡、兔子 | 极易 | 被动产蛋/毛 |
| D | 野猪、鹿 | 简单 | 负重、警戒 |
| C | 狼、鹰 | 中等 | 战斗伙伴 |
| B | 野马、熊 | 较难 | 骑乘、重型战斗 |
| A | 骆驼、猛犸 | 困难 | 大型运输、特殊能力 |
| S | 飞龙、冰熊王 | 极难 | 飞行/强力战斗 |
| BOSS | 远古神兽 | 击败后特殊流程 | 解锁终局内容 |

---

## 1.6 基地建造系统

### 建材分档

| 档位 | 材料 | 生命值 | 解锁条件 |
|---|---|---|---|
| 茅草 | 茅草+树枝 | 500 | 初始解锁 |
| 木材 | 原木+纤维 | 2000 | 击败第一Boss |
| 石砌 | 石块+木材 | 8000 | 击败第二Boss |
| 金属 | 钢铁+石块 | 25000 | 击败第三Boss |
| 合金 | 稀有合金+精炼材料 | 80000 | 终局解锁 |

### 建造件类型

```
整体建筑（2.5D 俯视，以"完整房屋"为放置单位，保证视觉完整性）：
  茅草房、木屋、石砌小屋、金属堡垒、合金要塞

功能件（单独放置的独立物件）：
  营火、工作台、熔炉、储物柜、床、灯柱

防御件：围栏、栅栏门、瞭望塔、陷阱

生物设施：驯养栏、喂食槽、繁殖巢、坐骑栓绑柱
```

### 结构完整性

- 每个建造件有**支撑点数**，悬空超过限制会坍塌
- 地基提供最高支撑，柱子可向上传递支撑
- 吸附点系统（Snap Points）：墙角、地板边缘自动对齐，是建造系统最核心的UX

---

## 1.7 制作系统

### 进度解锁方式

```
配方解锁来源（三类）：
  1. 升级点（每级获得2点，用于购买基础配方）
  2. 蓝图（探索世界获得，解锁高级配方）
  3. Boss击杀（解锁该档位材料相关所有配方）
```

### 制作站分级

| 制作站 | 可制作内容 |
|---|---|
| 徒手 | 基础工具、茅草建筑、简单食物 |
| 工作台 | 木制工具、木材建筑、基础武器 |
| 铁砧 | 金属工具、武器装备、机械零件 |
| 化工台 | 药剂、麻醉品、爆炸物 |
| 远古熔炉 | 合金、高级装备（需击败Boss解锁） |

---

## 1.8 Boss 与进度系统

| Boss | 所在群系 | 击败解锁 |
|---|---|---|
| 森林守护者 | 温带森林 | 木材建筑、铁器时代 |
| 草原领主 | 草原 | 骑乘系统、皮革装备 |
| 沙漠蜂后 | 沙漠 | 化工台、沙漠生物驯养 |
| 雪原霸主 | 雪原 | 金属建筑、极寒装备 |
| 熔岩巨人 | 熔岩地带 | 合金锻造、火系生物 |
| 远古神兽 | 深林腹地 | 终局内容、飞行坐骑 |

---

# PART 2：代码架构设计

---

## 2.1 技术选型

```
引擎：    Godot 4.x
语言：    C#（性能敏感系统）+ GDScript（快速原型/UI）
渲染：    3D场景 + 正交相机
角色：    Sprite3D Billboard（始终朝向相机的2D纸片人）
物理：    Godot 3D PhysicsServer（地形碰撞）+ Area3D（交互检测）
地图：    GridMap（3D瓦片地图）+ 程序化生成
多人：    预留接口，初期单机，后期接入 MultiplayerAPI
```

---

## 2.2 项目目录结构

```
res://
├── core/                      # 核心系统（纯逻辑，无Node依赖）
│   ├── ecs/
│   │   ├── World.cs           # 实体管理器
│   │   ├── ComponentStore.cs  # 组件存储
│   │   └── SystemBase.cs      # 系统基类
│   ├── systems/
│   │   ├── SurvivalSystem.cs  # 饥饿/口渴/体温
│   │   ├── TamingSystem.cs    # 驯养逻辑
│   │   ├── CraftingSystem.cs  # 制作逻辑
│   │   ├── BuildingSystem.cs  # 建造逻辑
│   │   └── AISystem.cs        # 生物AI
│   └── data/
│       ├── ItemRegistry.cs    # 物品定义注册表
│       ├── RecipeRegistry.cs  # 配方注册表
│       └── CreatureRegistry.cs# 生物定义注册表
│
├── world/                     # 世界生成与管理
│   ├── WorldManager.cs        # 世界总管理器
│   ├── ChunkManager.cs        # 分块加载/卸载
│   ├── ChunkGenerator.cs      # 程序化生成
│   └── BiomeMap.cs            # 群系分布图
│
├── entities/                  # Godot节点层（视图）
│   ├── player/
│   │   ├── Player.cs          # 玩家控制器
│   │   └── PlayerCamera.cs    # 正交相机控制
│   ├── creatures/
│   │   ├── CreatureView.cs    # 生物视图基类（Sprite3D）
│   │   └── CreatureSpawner.cs
│   └── building/
│       └── BuildingPlacer.cs  # 建造预览与放置
│
├── ui/                        # 全部UI（CanvasLayer）
│   ├── HUD.tscn
│   ├── Inventory.tscn
│   ├── CraftingMenu.tscn
│   └── TamingUI.tscn
│
├── resources/                 # 数据定义文件（.tres）
│   ├── items/
│   ├── recipes/
│   ├── creatures/
│   └── biomes/
│
└── assets/                    # 美术资产
    ├── sprites/               # 生物/玩家精灵表
    ├── models/                # 地形3D模型
    └── ui/
```

---

## 2.3 渲染架构：3D场景 + 正交相机 + Sprite3D

### 相机设置

```csharp
// PlayerCamera.cs
public partial class PlayerCamera : Camera3D
{
    [Export] public float ZoomLevel = 20f;
    [Export] public Vector3 Offset = new Vector3(0, 15, 10);

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal;
        Size = ZoomLevel;
        // 斜45度俯视，类似饥荒视角
        RotationDegrees = new Vector3(-50, 0, 0);
    }

    public override void _Process(double delta)
    {
        var target = GetParent<Node3D>().GlobalPosition;
        GlobalPosition = target + Offset;
        LookAt(target, Vector3.Up);
    }
}
```

### 生物/玩家 Billboard 精灵

```csharp
// CreatureView.cs
public partial class CreatureView : Node3D
{
    [Export] public Texture2D SpriteSheet;
    [Export] public int FrameCountX = 8;  // 8方向
    [Export] public int FrameCountY = 12; // 12个动画帧

    private Sprite3D _sprite;

    public override void _Ready()
    {
        _sprite = new Sprite3D();
        _sprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _sprite.Texture = SpriteSheet;
        _sprite.RegionEnabled = true;
        AddChild(_sprite);
    }

    // 根据移动方向选择对应的精灵朝向列
    public void UpdateFacingDirection(Vector3 velocity)
    {
        if (velocity.LengthSquared() < 0.01f) return;
        var angle = Mathf.Atan2(velocity.X, velocity.Z);
        var dirIndex = (int)(((angle / Mathf.Tau) + 1.0f) * FrameCountX) % FrameCountX;
        SetSpriteColumn(dirIndex);
    }
}
```

---

## 2.4 ECS-Lite 架构

### 核心设计原则：数据与视图完全分离

```
【数据层 ECS】                    【视图层 Godot Nodes】
EntityID (int) ←───────────────→ Node3D (CreatureView)
    │                                     │
ComponentStore                      _entityId 字段
    ├── HealthComponent[]               同步数据变化
    ├── HungerComponent[]               渲染动画
    ├── TamingComponent[]               播放音效
    └── AIComponent[]
```

### World.cs — 实体管理器

```csharp
public class World
{
    private static World _instance;
    public static World Instance => _instance ??= new World();

    private int _nextEntityId = 1;
    private readonly Dictionary<Type, object> _componentStores = new();

    public int CreateEntity() => _nextEntityId++;

    public void AddComponent<T>(int entityId, T component) where T : class
    {
        GetStore<T>()[entityId] = component;
    }

    public T GetComponent<T>(int entityId) where T : class
    {
        return GetStore<T>().TryGetValue(entityId, out var c) ? c : null;
    }

    public bool HasComponent<T>(int entityId) where T : class
        => GetStore<T>().ContainsKey(entityId);

    public IEnumerable<int> Query<T>() where T : class
        => GetStore<T>().Keys;

    public IEnumerable<int> Query<T1, T2>() where T1 : class where T2 : class
        => GetStore<T1>().Keys.Intersect(GetStore<T2>().Keys);

    private Dictionary<int, T> GetStore<T>() where T : class
    {
        if (!_componentStores.TryGetValue(typeof(T), out var store))
        {
            store = new Dictionary<int, T>();
            _componentStores[typeof(T)] = store;
        }
        return (Dictionary<int, T>)store;
    }
}
```

### 核心组件定义

```csharp
public class HealthComponent
{
    public float MaxHp = 100f;
    public float CurrentHp = 100f;
    public bool IsAlive => CurrentHp > 0;
}

public class SurvivalComponent  // 仅玩家持有
{
    public float Hunger = 100f;       // 被动 -1/分钟
    public float Thirst = 100f;       // 被动 -1.5/分钟
    public float Temperature = 37f;   // 正常范围 35-39
    public float Stamina = 100f;
    public float Weight = 0f;
    public float MaxWeight = 50f;
}

public class TamingComponent
{
    public TamingState State = TamingState.Wild;
    public float TrustProgress = 0f;          // 0-100
    public string PreferredFood;
    public TamingMethod Method;
    public float SedationTimer = 0f;          // 击晕计时
    public float TamingEffectiveness = 100f;  // 受伤会降低
}

public class CreatureStatsComponent
{
    public int Level = 1;
    public int OwnerId = -1;                  // -1=野生
    public float Loyalty = 100f;
    public AIBehavior CurrentOrder = AIBehavior.Wander;
    public List<string> Traits = new();
}

public class InventoryComponent
{
    public List<ItemInstance> Items = new();
    public float MaxWeight = 50f;
}

public class PositionComponent
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float FacingAngle;
}

public class AIComponent
{
    public AITier Tier = AITier.Dormant;
    public FSMState CurrentState;
    public int TargetEntityId = -1;
    public Vector3 Destination;
    public float StateTimer = 0f;
}
```

---

## 2.5 核心系统实现

### SurvivalSystem.cs

```csharp
public class SurvivalSystem : SystemBase
{
    private const float HungerDrainRate = 1f / 60f;
    private const float ThirstDrainRate = 1.5f / 60f;
    private const float DamageTempExtreme = 2f;

    public override void Tick(float delta)
    {
        foreach (var entityId in World.Instance.Query<SurvivalComponent, HealthComponent>())
        {
            var survival = World.Instance.GetComponent<SurvivalComponent>(entityId);
            var health = World.Instance.GetComponent<HealthComponent>(entityId);

            survival.Hunger = Mathf.Max(0, survival.Hunger - HungerDrainRate * delta);
            survival.Thirst = Mathf.Max(0, survival.Thirst - ThirstDrainRate * delta);

            if (survival.Hunger <= 0) health.CurrentHp -= 1f * delta;
            if (survival.Thirst <= 0) health.CurrentHp -= 2f * delta;

            if (survival.Temperature < 30f || survival.Temperature > 42f)
                health.CurrentHp -= DamageTempExtreme * delta;

            UpdateTemperature(entityId, survival, delta);
        }
    }

    private void UpdateTemperature(int entityId, SurvivalComponent survival, float delta)
    {
        var pos = World.Instance.GetComponent<PositionComponent>(entityId);
        float biomeTemp = WorldManager.Instance.GetBiomeTemperature(pos.Position);
        float timeTemp = DayNightSystem.Instance.GetTemperatureModifier();
        float clothingTemp = GetClothingInsulation(entityId);

        float targetTemp = biomeTemp + timeTemp + clothingTemp;
        survival.Temperature = Mathf.Lerp(survival.Temperature, targetTemp, 0.1f * delta);
    }
}
```

### TamingSystem.cs

```csharp
public class TamingSystem : SystemBase
{
    public bool TryFeedCreature(int playerId, int creatureId)
    {
        var taming = World.Instance.GetComponent<TamingComponent>(creatureId);
        var playerInv = World.Instance.GetComponent<InventoryComponent>(playerId);

        if (taming.State == TamingState.Hostile) return false;

        var food = playerInv.FindItem(taming.PreferredFood);
        if (food == null) return false;

        playerInv.RemoveItem(food, 1);

        float trustGain = food.ItemId == taming.PreferredFood ? 15f : 5f;
        taming.TrustProgress = Mathf.Min(100f, taming.TrustProgress + trustGain);

        if (taming.State == TamingState.Wild)
            taming.State = TamingState.Bonding;

        if (taming.TrustProgress >= 100f)
            CompleteTaming(playerId, creatureId);

        return true;
    }

    private void CompleteTaming(int playerId, int creatureId)
    {
        var taming = World.Instance.GetComponent<TamingComponent>(creatureId);
        var stats = World.Instance.GetComponent<CreatureStatsComponent>(creatureId);
        var health = World.Instance.GetComponent<HealthComponent>(creatureId);

        taming.State = TamingState.Tamed;
        stats.OwnerId = playerId;

        // 驯养效率影响最终属性
        float bonus = taming.TamingEffectiveness / 100f;
        health.MaxHp *= bonus;

        EventBus.Instance.Emit("creature_tamed", new { playerId, creatureId });
    }

    public override void Tick(float delta)
    {
        foreach (var entityId in World.Instance.Query<TamingComponent>())
        {
            var taming = World.Instance.GetComponent<TamingComponent>(entityId);

            // 麻醉自然衰减
            if (taming.State == TamingState.Sedated)
            {
                taming.SedationTimer -= delta;
                if (taming.SedationTimer <= 0)
                {
                    taming.State = TamingState.Hostile;
                    taming.TrustProgress = 0;
                }
            }

            // 驯服生物忠诚度衰减
            if (taming.State == TamingState.Tamed)
            {
                var stats = World.Instance.GetComponent<CreatureStatsComponent>(entityId);
                stats.Loyalty = Mathf.Max(0, stats.Loyalty - 0.5f * delta / 60f);
                if (stats.Loyalty <= 0)
                    AbandonCreature(entityId);
            }
        }
    }
}
```

### CraftingSystem.cs

```csharp
public class CraftingSystem
{
    public CraftResult TryCraft(int entityId, string recipeId)
    {
        var recipe = RecipeRegistry.Instance.Get(recipeId);
        var inventory = World.Instance.GetComponent<InventoryComponent>(entityId);

        foreach (var req in recipe.Ingredients)
            if (inventory.CountItem(req.ItemId) < req.Quantity)
                return CraftResult.InsufficientMaterials;

        if (recipe.RequiredStation != null && !IsNearStation(entityId, recipe.RequiredStation))
            return CraftResult.NeedCraftingStation;

        // 原子事务：失败则回滚
        var transaction = new InventoryTransaction(inventory);
        foreach (var req in recipe.Ingredients)
            transaction.Remove(req.ItemId, req.Quantity);
        transaction.Add(recipe.ResultItemId, recipe.ResultQuantity);

        return transaction.Commit() ? CraftResult.Success : CraftResult.Failed;
    }
}
```

---

## 2.6 生物 AI 系统（三层 + HFSM）

```csharp
public class AISystem : SystemBase
{
    private const float ActiveRange = 30f;
    private const float PassiveTickInterval = 1.5f;
    private float _passiveTick = 0f;

    public override void Tick(float delta)
    {
        var playerPos = GetPlayerPosition();
        _passiveTick += delta;

        foreach (var entityId in World.Instance.Query<AIComponent, PositionComponent>())
        {
            var ai = World.Instance.GetComponent<AIComponent>(entityId);
            var pos = World.Instance.GetComponent<PositionComponent>(entityId);

            float dist = pos.Position.DistanceTo(playerPos);

            // 动态切换 AI 层级
            ai.Tier = dist < ActiveRange ? AITier.Active
                    : dist < 80f ? AITier.Passive
                    : AITier.Dormant;

            switch (ai.Tier)
            {
                case AITier.Active:
                    TickActiveFSM(entityId, ai, delta);
                    break;
                case AITier.Passive:
                    if (_passiveTick >= PassiveTickInterval)
                        TickPassive(entityId, ai, PassiveTickInterval);
                    break;
                // Dormant: 仅在 Chunk 加载时进行时间补偿
            }
        }

        if (_passiveTick >= PassiveTickInterval) _passiveTick = 0f;
    }

    private void TickActiveFSM(int entityId, AIComponent ai, float delta)
    {
        var taming = World.Instance.GetComponent<TamingComponent>(entityId);

        switch (ai.CurrentState)
        {
            case FSMState.Wander:
                HandleWander(entityId, ai, delta);
                if (IsPlayerInDetectionRange(entityId))
                    ai.CurrentState = taming.State == TamingState.Tamed
                        ? FSMState.Follow : FSMState.Alert;
                break;
            case FSMState.Alert:    HandleAlert(entityId, ai, delta);   break;
            case FSMState.Hostile:  HandleCombat(entityId, ai, delta);  break;
            case FSMState.Cautious: HandleCautious(entityId, ai, delta);break;
            case FSMState.Follow:   HandleFollow(entityId, ai, delta);  break;
            case FSMState.Guard:    HandleGuard(entityId, ai, delta);   break;
            case FSMState.Harvest:  HandleHarvest(entityId, ai, delta); break;
        }
    }
}
```

---

## 2.7 世界分块系统

```csharp
// ChunkManager.cs
public partial class ChunkManager : Node
{
    private const int ChunkSize = 32;
    private const int ActiveRadius = 3;

    private Dictionary<Vector2I, ChunkData> _loadedChunks = new();
    private Vector2I _lastPlayerChunk;

    public override void _Process(double delta)
    {
        var playerChunk = WorldToChunk(GetPlayerPosition());
        if (playerChunk == _lastPlayerChunk) return;

        _lastPlayerChunk = playerChunk;
        UpdateLoadedChunks(playerChunk);
    }

    private void UpdateLoadedChunks(Vector2I center)
    {
        var needed = new HashSet<Vector2I>();
        for (int x = -ActiveRadius; x <= ActiveRadius; x++)
        for (int z = -ActiveRadius; z <= ActiveRadius; z++)
            needed.Add(center + new Vector2I(x, z));

        foreach (var coord in needed)
            if (!_loadedChunks.ContainsKey(coord))
                LoadChunkAsync(coord);

        var toUnload = _loadedChunks.Keys.Where(c => !needed.Contains(c)).ToList();
        foreach (var coord in toUnload)
            UnloadChunk(coord);
    }

    private async void LoadChunkAsync(Vector2I coord)
    {
        var saved = await SaveSystem.LoadChunkAsync(coord);
        ChunkData chunk = saved ?? ChunkGenerator.Instance.Generate(coord);

        _loadedChunks[coord] = chunk;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ApplyChunkToScene(chunk);
    }

    private void UnloadChunk(Vector2I coord)
    {
        var chunk = _loadedChunks[coord];
        SaveSystem.SaveChunkAsync(coord, chunk);
        RemoveChunkFromScene(chunk);
        _loadedChunks.Remove(coord);
    }
}
```

### 程序化地形生成

```csharp
// ChunkGenerator.cs
public class ChunkGenerator
{
    private FastNoiseLite _heightNoise = new();
    private FastNoiseLite _moistureNoise = new();
    private FastNoiseLite _temperatureNoise = new();

    public ChunkData Generate(Vector2I chunkCoord)
    {
        var chunk = new ChunkData(chunkCoord);
        var worldOffset = new Vector2(chunkCoord.X * 32, chunkCoord.Y * 32);

        for (int x = 0; x < 32; x++)
        for (int z = 0; z < 32; z++)
        {
            float wx = worldOffset.X + x;
            float wz = worldOffset.Y + z;

            float height = _heightNoise.GetNoise2D(wx * 0.01f, wz * 0.01f);
            float moisture = _moistureNoise.GetNoise2D(wx * 0.005f, wz * 0.005f);
            float temp = _temperatureNoise.GetNoise2D(wx * 0.003f, wz * 0.003f);

            BiomeType biome = ClassifyBiome(height, moisture, temp);
            TileType tile = BiomeToTile(biome, height);
            chunk.SetTile(x, z, tile, biome);

            SpawnObjects(chunk, x, z, biome, wx, wz);
        }

        return chunk;
    }

    private BiomeType ClassifyBiome(float h, float m, float t)
    {
        if (h < -0.3f) return BiomeType.Water;
        if (t > 0.5f && m < 0.2f) return BiomeType.Desert;
        if (t < -0.5f) return BiomeType.Snow;
        if (h > 0.6f) return BiomeType.LavaZone;
        if (m > 0.4f) return BiomeType.Forest;
        return BiomeType.Grassland;
    }
}
```

---

## 2.8 建造系统

```csharp
public class BuildingSystem
{
    public PlaceResult TryPlace(int playerId, string pieceId, Vector3 position, Quaternion rotation)
    {
        var piece = BuildingRegistry.Instance.Get(pieceId);
        var playerInv = World.Instance.GetComponent<InventoryComponent>(playerId);

        foreach (var mat in piece.Materials)
            if (playerInv.CountItem(mat.Key) < mat.Value)
                return PlaceResult.InsufficientMaterials;

        if (HasCollision(position, piece.Bounds))
            return PlaceResult.Blocked;

        if (!HasSupport(position, piece))
            return PlaceResult.NoSupport;

        foreach (var mat in piece.Materials)
            playerInv.RemoveItem(mat.Key, mat.Value);

        PlacePiece(piece, position, rotation);
        return PlaceResult.Success;
    }

    private bool HasSupport(Vector3 position, BuildingPieceDef piece)
    {
        if (piece.Type == PieceType.Foundation) return true;
        var neighbors = GetAdjacentPieces(position);
        return neighbors.Any(n => n.SupportValue >= piece.RequiredSupport);
    }

    // 吸附点系统：找到最近的兼容吸附点
    public SnapResult FindSnapPoint(Vector3 cursorPos, string pieceId)
    {
        var nearby = GetNearbyPieces(cursorPos, 3f);
        SnapPoint best = null;
        float bestDist = float.MaxValue;

        foreach (var placed in nearby)
        foreach (var snap in placed.GetSnapPoints())
        {
            if (!snap.CompatibleWith(pieceId)) continue;
            float d = snap.WorldPosition.DistanceTo(cursorPos);
            if (d < bestDist) { bestDist = d; best = snap; }
        }

        return best != null ? new SnapResult(best) : SnapResult.Free(cursorPos);
    }
}
```

---

## 2.9 数据资源定义

```csharp
// CreatureDefinition.cs
[GlobalClass]
public partial class CreatureDefinition : Resource
{
    [Export] public string Id;
    [Export] public string DisplayName;
    [Export] public Texture2D SpriteSheet;
    [Export] public CreatureTier Tier;
    [Export] public TamingMethod TamingMethod;
    [Export] public string PreferredFood;
    [Export] public float BaseHp = 200f;
    [Export] public float BaseAttack = 15f;
    [Export] public float BaseSpeed = 5f;
    [Export] public float BaseWeight = 100f;
    [Export] public string[] PossibleTraits;
    [Export] public string[] SpawnBiomes;
    [Export] public bool CanRide = false;
    [Export] public bool CanFly = false;
    [Export] public string HarvestResourceType;
}

// ItemDefinition.cs
[GlobalClass]
public partial class ItemDefinition : Resource
{
    [Export] public string Id;
    [Export] public string DisplayName;
    [Export] public Texture2D Icon;
    [Export] public ItemCategory Category;
    [Export] public int MaxStackSize = 50;
    [Export] public float Weight = 0.5f;
    [Export] public float DurabilityMax = -1; // -1 = 无耐久
}

// RecipeDefinition.cs
[GlobalClass]
public partial class RecipeDefinition : Resource
{
    [Export] public string Id;
    [Export] public string ResultItemId;
    [Export] public int ResultQuantity = 1;
    [Export] public Godot.Collections.Array<IngredientEntry> Ingredients;
    [Export] public string RequiredStation;       // null = 徒手
    [Export] public int RequiredPlayerLevel = 1;
    [Export] public string UnlockCondition;       // "default" / "boss_kill:forest" / "blueprint"
}
```

---

## 2.10 GameManager — 系统总调度

```csharp
public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    private List<SystemBase> _systems = new();

    public override void _Ready()
    {
        Instance = this;

        _systems.Add(new SurvivalSystem());
        _systems.Add(new TamingSystem());
        _systems.Add(new AISystem());
        _systems.Add(new BuildingSystem());
        _systems.Add(new CraftingSystem());
        _systems.Add(new DayNightSystem());

        WorldManager.Instance.Initialize();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        foreach (var system in _systems)
            system.Tick(dt);
    }

    public void SaveGame()
    {
        var saveData = new SaveData
        {
            ChunkStates = ChunkManager.Instance.SerializeLoadedChunks(),
            EntityStates = World.Instance.SerializeAll(),
            PlayerState = SerializePlayer()
        };
        SaveSystem.Write("save_slot_1", saveData);
    }
}
```

---

## 2.11 开发里程碑规划

| 里程碑 | 目标 | 核心任务 |
|---|---|---|
| **M1** | 能动能采 | 正交相机、Sprite3D人物、基础移动、手动采集一种资源 |
| **M2** | 能活下去 | 饥渴体温系统、营火、茅草房（整体建筑，含庇护所保温） |
| **M3** | 能驯生物 | 一只生物完整驯养流程（野生→跟随→自动采集） |
| **M4** | 能建基地 | 吸附系统、2档建材、结构完整性 |
| **M5** | 有世界 | Chunk分块、程序生成2个群系、昼夜循环 |
| **M6** | 有目标 | 第一个Boss、科技树前两档、制作菜单 |
| **M7** | 有深度 | 生物繁殖、驯养特性词条、完整6个群系 |
| **M8** | 可发布 | 存读档、UI完整、音效、性能优化、Steam页面 |
