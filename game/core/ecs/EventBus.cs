using System;
using System.Collections.Generic;

namespace SurvivalGame.Core.ECS
{
    /// <summary>
    /// 全局事件总线，用于系统间解耦通信。
    /// 用法：EventBus.Instance.Subscribe("creature_tamed", OnTamed);
    ///       EventBus.Instance.Emit("creature_tamed", payload);
    /// </summary>
    public class EventBus
    {
        private static EventBus? _instance;
        public static EventBus Instance => _instance ??= new EventBus();

        private readonly Dictionary<string, List<Action<object?>>> _listeners = new();

        public void Subscribe(string eventName, Action<object?> handler)
        {
            if (!_listeners.TryGetValue(eventName, out var list))
            {
                list = new List<Action<object?>>();
                _listeners[eventName] = list;
            }
            list.Add(handler);
        }

        public void Unsubscribe(string eventName, Action<object?> handler)
        {
            if (_listeners.TryGetValue(eventName, out var list))
                list.Remove(handler);
        }

        public void Emit(string eventName, object? payload = null)
        {
            if (!_listeners.TryGetValue(eventName, out var list)) return;
            foreach (var handler in list.ToArray())
                handler(payload);
        }

        public static void Reset() => _instance = null;
    }
}
