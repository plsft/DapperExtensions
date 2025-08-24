namespace DapperExtensions.Proxy
{
    /// <summary>
    /// Example demonstrating how to use the proxy/dirty tracking functionality
    /// </summary>
    public static class ProxyExample
    {
        public class Person
        {
            public virtual int Id { get; set; }
            public virtual string FirstName { get; set; }
            public virtual string LastName { get; set; }
            public virtual DateTime DateCreated { get; set; }
            public virtual bool Active { get; set; }
        }

        public static async Task DemoProxyUsage()
        {
            // Enable proxy generation
            DapperExtensions.Configure()
                .SetProxyGeneration(true);

            using var connection = GetConnection(); // Your connection

            // 1. Get entity as proxy with change tracking
            var person = await connection.GetAsync<Person>(1);
            // person is now a proxy with change tracking enabled

            // 2. Make changes - only these properties will be tracked as dirty
            person.FirstName = "John";
            person.LastName = "Doe";

            // 3. Update only dirty properties (FirstName and LastName)
            await connection.UpdateDirtyAsync(person);

            // 4. Bulk operations with IAsyncEnumerable
            var people = GetPeopleAsync(); // Returns IAsyncEnumerable<Person>
            
            // Update all people, tracking only changed properties
            var updatedCount = await connection.UpdateDirtyAsync(people);
            
            // Batch updates for better performance
            var batchCount = await connection.UpdateDirtyBatchAsync(people, batchSize: 50);
        }

        private static IDbConnection GetConnection()
        {
            // Return your database connection
            throw new NotImplementedException("Implement your connection logic");
        }

        private static async IAsyncEnumerable<Person> GetPeopleAsync()
        {
            // Simulate async enumerable stream
            for (int i = 0; i < 1000; i++)
            {
                yield return new Person 
                { 
                    Id = i, 
                    FirstName = $"Person{i}",
                    LastName = $"Test{i}",
                    Active = true,
                    DateCreated = DateTime.Now
                };
                
                // Simulate async work
                await Task.Delay(10);
            }
        }
    }
}