namespace DapperExtensions.Proxy
{
    public interface IProxyFactory
    {
        T CreateProxy<T>() where T : class;
        T CreateProxy<T>(T entity) where T : class;
        bool IsProxy(object entity);
        IChangeTracker GetChangeTracker(object proxy);
    }
}