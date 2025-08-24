using System.Collections.Concurrent;

namespace DapperExtensions.Proxy
{
    public class ChangeTracker : IChangeTracker
    {
        private readonly ConcurrentDictionary<string, bool> _dirtyProperties = new();
        private bool _isTracking = true;

        public bool IsDirty => _dirtyProperties.Any(p => p.Value);

        public void MarkAsClean()
        {
            _dirtyProperties.Clear();
        }

        public void MarkPropertyDirty(string propertyName)
        {
            if (_isTracking && !string.IsNullOrEmpty(propertyName))
            {
                _dirtyProperties[propertyName] = true;
            }
        }

        public bool IsPropertyDirty(string propertyName)
        {
            return _dirtyProperties.TryGetValue(propertyName, out var isDirty) && isDirty;
        }

        public IEnumerable<string> GetDirtyProperties()
        {
            return _dirtyProperties.Where(p => p.Value).Select(p => p.Key);
        }

        public void StartTracking()
        {
            _isTracking = true;
        }

        public void StopTracking()
        {
            _isTracking = false;
        }
    }
}