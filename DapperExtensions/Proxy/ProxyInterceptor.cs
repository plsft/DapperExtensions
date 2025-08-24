using Castle.DynamicProxy;

namespace DapperExtensions.Proxy
{
    public class ProxyInterceptor : IInterceptor
    {
        private readonly IChangeTracker _changeTracker;
        private readonly Dictionary<string, object> _originalValues = new();

        public ProxyInterceptor(IChangeTracker changeTracker)
        {
            _changeTracker = changeTracker;
        }

        public void Intercept(IInvocation invocation)
        {
            var methodName = invocation.Method.Name;

            if (methodName.StartsWith("set_", StringComparison.Ordinal))
            {
                var propertyName = methodName.Substring(4);
                var newValue = invocation.Arguments[0];

                // Store original value if not already stored
                var getterName = "get_" + propertyName;
                var getter = invocation.TargetType.GetMethod(getterName);
                if (getter != null && !_originalValues.ContainsKey(propertyName))
                {
                    invocation.Proceed();
                    var currentValue = getter.Invoke(invocation.InvocationTarget, null);
                    _originalValues[propertyName] = currentValue;
                    return;
                }

                // Check if value actually changed
                if (_originalValues.TryGetValue(propertyName, out var originalValue))
                {
                    if (!Equals(originalValue, newValue))
                    {
                        _changeTracker.MarkPropertyDirty(propertyName);
                    }
                }
                else
                {
                    _changeTracker.MarkPropertyDirty(propertyName);
                }
            }

            invocation.Proceed();
        }
    }
}