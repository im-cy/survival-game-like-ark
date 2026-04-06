using System;
using System.Collections.Generic;
using System.Linq;

namespace SurvivalGame.Core.ECS
{
    /// <summary>
    /// 实体管理器 — ECS 数据层核心，负责创建实体、存储/查询组件。
    /// 与 Godot Node 树完全解耦，纯 C# 逻辑。
    /// </summary>
    public class World
    {
        private static World? _instance;
        public static World Instance => _instance ??= new World();

        private int _nextEntityId = 1;
        private readonly Dictionary<Type, object> _stores = new();
        private readonly HashSet<int> _entities = new();

        // ── 实体生命周期 ────────────────────────────────────────────────

        public int CreateEntity()
        {
            int id = _nextEntityId++;
            _entities.Add(id);
            return id;
        }

        public void DestroyEntity(int entityId)
        {
            _entities.Remove(entityId);
            foreach (var store in _stores.Values)
            {
                // 每个 store 都是 Dictionary<int, T>，用反射移除
                var remove = store.GetType().GetMethod("Remove", new[] { typeof(int) });
                remove?.Invoke(store, new object[] { entityId });
            }
        }

        public bool EntityExists(int entityId) => _entities.Contains(entityId);

        // ── 组件操作 ────────────────────────────────────────────────────

        public void AddComponent<T>(int entityId, T component) where T : class
            => GetStore<T>()[entityId] = component;

        public T? GetComponent<T>(int entityId) where T : class
            => GetStore<T>().TryGetValue(entityId, out var c) ? c : null;

        public bool HasComponent<T>(int entityId) where T : class
            => GetStore<T>().ContainsKey(entityId);

        public void RemoveComponent<T>(int entityId) where T : class
            => GetStore<T>().Remove(entityId);

        // ── 查询 ────────────────────────────────────────────────────────

        public IEnumerable<int> Query<T>() where T : class
            => GetStore<T>().Keys.ToArray();

        public IEnumerable<int> Query<T1, T2>() where T1 : class where T2 : class
            => GetStore<T1>().Keys.Intersect(GetStore<T2>().Keys).ToArray();

        public IEnumerable<int> Query<T1, T2, T3>()
            where T1 : class where T2 : class where T3 : class
            => GetStore<T1>().Keys
                .Intersect(GetStore<T2>().Keys)
                .Intersect(GetStore<T3>().Keys)
                .ToArray();

        // ── 序列化（存档用） ────────────────────────────────────────────

        public IEnumerable<int> GetAllEntities() => _entities.ToArray();

        // ── 内部 ────────────────────────────────────────────────────────

        private Dictionary<int, T> GetStore<T>() where T : class
        {
            if (!_stores.TryGetValue(typeof(T), out var raw))
            {
                raw = new Dictionary<int, T>();
                _stores[typeof(T)] = raw;
            }
            return (Dictionary<int, T>)raw;
        }

        /// 测试/重载时重置单例
        public static void Reset() => _instance = null;
    }
}
