using Castle.DynamicProxy;
using System.Collections.Concurrent;

namespace DapperExtensions.Proxy
{
    public class ProxyFactory : IProxyFactory
    {
        private readonly ProxyGenerator _proxyGenerator = new();
        private readonly ConcurrentDictionary<object, IChangeTracker> _changeTrackers = new();

        public T CreateProxy<T>() where T : class
        {
            var changeTracker = new ChangeTracker();
            var interceptor = new ProxyInterceptor(changeTracker);
            
            var proxy = _proxyGenerator.CreateClassProxy<T>(
                new ProxyGenerationOptions { Hook = new ProxyGenerationHook() },
                interceptor);

            _changeTrackers[proxy] = changeTracker;
            
            if (proxy is IProxy proxyInterface)
            {
                proxyInterface.IsDirty = false;
            }

            return proxy;
        }

        public T CreateProxy<T>(T entity) where T : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var changeTracker = new ChangeTracker();
            var interceptor = new ProxyInterceptor(changeTracker);
            
            var proxy = _proxyGenerator.CreateClassProxyWithTarget<T>(
                entity,
                new ProxyGenerationOptions { Hook = new ProxyGenerationHook() },
                interceptor);

            _changeTrackers[proxy] = changeTracker;
            
            if (proxy is IProxy proxyInterface)
            {
                proxyInterface.IsDirty = false;
            }

            // Copy values from original entity
            changeTracker.StopTracking();
            CopyProperties(entity, proxy);
            changeTracker.StartTracking();
            changeTracker.MarkAsClean();

            return proxy;
        }

        public bool IsProxy(object entity)
        {
            return entity != null && ProxyUtil.IsProxy(entity);
        }

        public IChangeTracker GetChangeTracker(object proxy)
        {
            if (proxy == null)
                throw new ArgumentNullException(nameof(proxy));

            if (!IsProxy(proxy))
                throw new ArgumentException("Object is not a proxy", nameof(proxy));

            return _changeTrackers.TryGetValue(proxy, out var tracker) 
                ? tracker 
                : throw new InvalidOperationException("Change tracker not found for proxy");
        }

        private static void CopyProperties<T>(T source, T target) where T : class
        {
            var properties = typeof(T).GetProperties()
                .Where(p => p.CanRead && p.CanWrite);

            foreach (var property in properties)
            {
                var value = property.GetValue(source);
                property.SetValue(target, value);
            }
        }

        private class ProxyGenerationHook : IProxyGenerationHook
        {
            public void MethodsInspected() { }

            public void NonProxyableMemberNotification(Type type, MemberInfo memberInfo) { }

            public bool ShouldInterceptMethod(Type type, MethodInfo methodInfo)
            {
                return methodInfo.IsSpecialName && 
                       (methodInfo.Name.StartsWith("set_", StringComparison.Ordinal) ||
                        methodInfo.Name.StartsWith("get_", StringComparison.Ordinal));
            }
        }
    }
}