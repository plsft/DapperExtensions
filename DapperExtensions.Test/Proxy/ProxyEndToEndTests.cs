using DapperExtensions.Mapper;
using DapperExtensions.Predicate;
using DapperExtensions.Proxy;
using DapperExtensions.Sql;
using FluentAssertions;
using NUnit.Framework;
using System.Data;
using Moq;
using Dapper;

namespace DapperExtensions.Test.Proxy
{
    [TestFixture]
    public class ProxyEndToEndTests
    {
        private Mock<IDbConnection> _mockConnection;
        private DapperImplementor _implementor;
        private DapperExtensionsConfiguration _configuration;
        private SqlGeneratorImpl _sqlGenerator;

        public class Customer
        {
            public virtual int Id { get; set; }
            public virtual string FirstName { get; set; }
            public virtual string LastName { get; set; }
            public virtual string Email { get; set; }
            public virtual decimal TotalPurchases { get; set; }
            public virtual DateTime CreatedDate { get; set; }
            public virtual DateTime? LastPurchaseDate { get; set; }
            public virtual bool IsActive { get; set; }
        }

        public class Order
        {
            public virtual int Id { get; set; }
            public virtual int CustomerId { get; set; }
            public virtual DateTime OrderDate { get; set; }
            public virtual decimal Total { get; set; }
            public virtual string Status { get; set; }
        }

        [SetUp]
        public void Setup()
        {
            _mockConnection = new Mock<IDbConnection>();
            _configuration = new DapperExtensionsConfiguration(
                typeof(AutoClassMapper<>),
                new List<Assembly>(),
                new SqlServerDialect()
            );
            _configuration.SetProxyGeneration(true);
            
            _sqlGenerator = new SqlGeneratorImpl(_configuration);
            _implementor = new DapperImplementor(_sqlGenerator);
        }

        [Test]
        public void CompleteScenario_GetUpdateDirtyWorkflow()
        {
            // Arrange - Setup mock to return data
            var customerId = 123;
            var originalCustomer = new Customer
            {
                Id = customerId,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                TotalPurchases = 1000m,
                CreatedDate = new DateTime(2020, 1, 1),
                LastPurchaseDate = new DateTime(2024, 1, 1),
                IsActive = true
            };

            // Mock the Get operation
            _mockConnection.Setup(c => c.Query<dynamic>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CommandType>()))
                .Returns(new[] { originalCustomer });

            // Mock the Update operation
            _mockConnection.Setup(c => c.Execute(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<int?>(),
                It.IsAny<CommandType>()))
                .Returns(1);

            // Act - Get customer (should return as proxy)
            var customer = _implementor.Get<Customer>(_mockConnection.Object, customerId, null, null);

            // Assert - Verify it's a proxy
            _configuration.ProxyFactory.IsProxy(customer).Should().BeTrue();
            customer.FirstName.Should().Be("John");
            customer.Email.Should().Be("john.doe@example.com");

            // Act - Modify only some properties
            customer.Email = "john.doe@newemail.com";
            customer.TotalPurchases += 150m;
            customer.LastPurchaseDate = DateTime.Now;

            // Act - Update with dirty tracking
            var updateResult = _implementor.UpdateDirty(_mockConnection.Object, customer, null, null);

            // Assert
            updateResult.Should().BeTrue();

            // Verify the SQL generated included only the dirty columns
            var lastSql = _implementor.LastExecutedCommand;
            lastSql.Should().NotBeNullOrEmpty();
            
            // The update should only include Email, TotalPurchases, and LastPurchaseDate
            lastSql.Should().Contain("Email");
            lastSql.Should().Contain("TotalPurchases");
            lastSql.Should().Contain("LastPurchaseDate");
            
            // Should NOT update unchanged fields
            lastSql.Should().NotContain("FirstName");
            lastSql.Should().NotContain("LastName");
            lastSql.Should().NotContain("CreatedDate");
            lastSql.Should().NotContain("IsActive");
        }

        [Test]
        public void BulkOperations_WithMixedProxyAndNonProxy()
        {
            // Arrange
            var customers = new List<Customer>();
            
            // Add some proxy customers
            for (int i = 1; i <= 3; i++)
            {
                var proxy = _configuration.ProxyFactory.CreateProxy<Customer>();
                proxy.Id = i;
                proxy.FirstName = $"Customer{i}";
                proxy.Email = $"customer{i}@example.com";
                proxy.IsActive = true;
                
                var tracker = _configuration.ProxyFactory.GetChangeTracker(proxy);
                tracker.MarkAsClean();
                
                // Modify only email
                proxy.Email = $"updated{i}@example.com";
                
                customers.Add(proxy);
            }
            
            // Add some non-proxy customers
            for (int i = 4; i <= 5; i++)
            {
                customers.Add(new Customer
                {
                    Id = i,
                    FirstName = $"Customer{i}",
                    Email = $"customer{i}@example.com",
                    IsActive = false
                });
            }

            var updateCount = 0;
            _mockConnection.Setup(c => c.Execute(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<int?>(),
                It.IsAny<CommandType>()))
                .Returns(() =>
                {
                    updateCount++;
                    return 1;
                });

            // Act
            foreach (var customer in customers)
            {
                _implementor.UpdateDirty(_mockConnection.Object, customer, null, null);
            }

            // Assert
            updateCount.Should().Be(5); // All 5 should be updated
        }

        [Test]
        public async Task AsyncScenario_StreamingUpdatesWithDirtyTracking()
        {
            // Arrange
            var asyncImplementor = new DapperAsyncImplementor(_sqlGenerator);
            
            async IAsyncEnumerable<Customer> GetCustomersToUpdate()
            {
                for (int i = 1; i <= 10; i++)
                {
                    var customer = _configuration.ProxyFactory.CreateProxy<Customer>();
                    customer.Id = i;
                    customer.FirstName = $"Customer{i}";
                    customer.LastName = $"Test{i}";
                    customer.Email = $"customer{i}@example.com";
                    customer.TotalPurchases = i * 100;
                    customer.IsActive = true;
                    
                    var tracker = _configuration.ProxyFactory.GetChangeTracker(customer);
                    tracker.MarkAsClean();
                    
                    // Simulate business logic - only update customers with high purchases
                    if (customer.TotalPurchases > 500)
                    {
                        customer.IsActive = false;
                        customer.Email = $"vip{i}@example.com";
                    }
                    
                    await Task.Yield();
                    yield return customer;
                }
            }

            var updatedCustomers = new List<int>();
            _mockConnection.Setup(c => c.Execute(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<int?>(),
                It.IsAny<CommandType>()))
                .Returns<string, object, IDbTransaction, int?, CommandType>((sql, param, _, __, ___) =>
                {
                    // Extract customer ID from parameters
                    if (param is DynamicParameters dynParams)
                    {
                        var id = dynParams.Get<int>("Id");
                        updatedCustomers.Add(id);
                    }
                    return 1;
                });

            // Act
            var customers = GetCustomersToUpdate();
            var processedCount = 0;
            
            await foreach (var customer in customers)
            {
                if (_configuration.ProxyFactory.IsProxy(customer))
                {
                    var tracker = _configuration.ProxyFactory.GetChangeTracker(customer);
                    if (tracker.IsDirty)
                    {
                        var result = await asyncImplementor.UpdateDirtyAsync(_mockConnection.Object, customer, null, null);
                        if (result) processedCount++;
                    }
                }
            }

            // Assert
            processedCount.Should().Be(5); // Customers 6-10 have TotalPurchases > 500
            updatedCustomers.Should().BeEquivalentTo(new[] { 6, 7, 8, 9, 10 });
        }

        [Test]
        public void TransactionalScenario_WithRollback()
        {
            // Arrange
            var mockTransaction = new Mock<IDbTransaction>();
            var customer = _configuration.ProxyFactory.CreateProxy<Customer>();
            customer.Id = 1;
            customer.Email = "test@example.com";
            
            var tracker = _configuration.ProxyFactory.GetChangeTracker(customer);
            tracker.MarkAsClean();

            // Act - Make changes
            customer.Email = "updated@example.com";
            customer.TotalPurchases = 2000m;

            // Simulate failed update
            _mockConnection.Setup(c => c.Execute(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.Is<IDbTransaction>(t => t == mockTransaction.Object),
                It.IsAny<int?>(),
                It.IsAny<CommandType>()))
                .Throws(new Exception("Database error"));

            // Act & Assert
            Assert.Throws<Exception>(() => 
                _implementor.UpdateDirty(_mockConnection.Object, customer, mockTransaction.Object, null));

            // The tracker should still show the properties as dirty since the update failed
            tracker.IsDirty.Should().BeTrue();
            tracker.GetDirtyProperties().Should().BeEquivalentTo(new[] { "Email", "TotalPurchases" });
        }

        [Test]
        public void Performance_CompareDirtyVsFullUpdate()
        {
            // Arrange
            var customer = new Customer
            {
                Id = 1,
                FirstName = "John",
                LastName = "Doe",
                Email = "john@example.com",
                TotalPurchases = 1000m,
                CreatedDate = DateTime.Now,
                LastPurchaseDate = DateTime.Now,
                IsActive = true
            };

            var proxy = _configuration.ProxyFactory.CreateProxy(customer);
            var tracker = _configuration.ProxyFactory.GetChangeTracker(proxy);
            tracker.MarkAsClean();

            // Change only one property
            proxy.Email = "newemail@example.com";

            string capturedSql = null;
            object capturedParams = null;

            _mockConnection.Setup(c => c.Execute(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<int?>(),
                It.IsAny<CommandType>()))
                .Callback<string, object, IDbTransaction, int?, CommandType>((sql, param, _, __, ___) =>
                {
                    capturedSql = sql;
                    capturedParams = param;
                })
                .Returns(1);

            // Act - Dirty update
            _implementor.UpdateDirty(_mockConnection.Object, proxy, null, null);
            var dirtySql = capturedSql;

            // Act - Full update
            _implementor.Update(_mockConnection.Object, customer, null, null);
            var fullSql = capturedSql;

            // Assert
            dirtySql.Should().NotBeNull();
            fullSql.Should().NotBeNull();
            
            // Dirty update should be shorter and more efficient
            dirtySql.Length.Should().BeLessThan(fullSql.Length);
            
            // Dirty update should only mention Email
            dirtySql.Should().Contain("Email");
            dirtySql.Should().NotContain("FirstName");
            
            // Full update should include all updatable columns
            fullSql.Should().Contain("FirstName");
            fullSql.Should().Contain("LastName");
            fullSql.Should().Contain("Email");
        }
    }
}