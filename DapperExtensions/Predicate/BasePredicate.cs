using DapperExtensions.Sql;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DapperExtensions.Predicate
{
    public interface IBasePredicate : IPredicate
    {
        string? PropertyName { get; set; }
    }

    public abstract class BasePredicate : IBasePredicate
    {
        public abstract string GetSql(ISqlGenerator sqlGenerator, IDictionary<string, object> parameters, bool isDml = false);
        public string? PropertyName { get; set; }

        protected virtual string GetColumnName(Type entityType, ISqlGenerator sqlGenerator, string propertyName, bool isDml = false, bool includePrefix = true)
        {
            var map = sqlGenerator.Configuration.GetMap(entityType);
            if (map == null)
            {
                throw new NullReferenceException($"Map was not found for {entityType}");
            }

            var propertyMap = map.Properties.SingleOrDefault(p => p.Name.Equals(propertyName, StringComparison.InvariantCultureIgnoreCase));
            if (propertyMap == null)
            {
                throw new NullReferenceException($"{propertyName} was not found for {entityType}");
            }

            return sqlGenerator.GetColumnName(map, propertyMap, false, isDml, includePrefix);
        }
    }
}
