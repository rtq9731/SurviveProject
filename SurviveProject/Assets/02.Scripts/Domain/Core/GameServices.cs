using System;
using System.Collections.Generic;

namespace Survive.Core
{
    /// <summary>
    /// 시스템 간 직접 참조를 없애기 위한 최소 레지스트리.
    /// DI 프레임워크를 도입하지 않는다 — 등록·조회·해제만 한다.
    /// </summary>
    public static class GameServices
    {
        static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _services[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var found)) return (T)found;
            throw new InvalidOperationException(
                $"서비스가 등록되지 않았습니다: {typeof(T).Name}. GameBootstrap이 씬에 있는지 확인하세요.");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var found))
            {
                service = (T)found;
                return true;
            }
            service = null;
            return false;
        }

        public static void Unregister<T>() where T : class => _services.Remove(typeof(T));

        public static void Clear() => _services.Clear();
    }
}
