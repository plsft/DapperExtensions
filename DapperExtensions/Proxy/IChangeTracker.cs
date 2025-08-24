namespace DapperExtensions.Proxy
{
    public interface IChangeTracker
    {
        bool IsDirty { get; }
        void MarkAsClean();
        void MarkPropertyDirty(string propertyName);
        bool IsPropertyDirty(string propertyName);
        IEnumerable<string> GetDirtyProperties();
        void StartTracking();
        void StopTracking();
    }
}