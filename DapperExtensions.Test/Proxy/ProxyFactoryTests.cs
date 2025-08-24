using DapperExtensions.Proxy;
using FluentAssertions;
using NUnit.Framework;

namespace DapperExtensions.Test.Proxy
{
    [TestFixture]
    public class ProxyFactoryTests
    {
        private ProxyFactory _proxyFactory;

        public class TestEntity
        {
            public virtual int Id { get; set; }
            public virtual string Name { get; set; }
            public virtual decimal Price { get; set; }
            public virtual DateTime CreatedDate { get; set; }
        }

        public class EntityWithNonVirtualProperties
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [SetUp]
        public void Setup()
        {
            _proxyFactory = new ProxyFactory();
        }

        [Test]
        public void CreateProxy_WithParameterlessConstructor_CreatesProxyInstance()
        {
            // Act
            var proxy = _proxyFactory.CreateProxy<TestEntity>();

            // Assert
            proxy.Should().NotBeNull();
            proxy.Should().BeAssignableTo<TestEntity>();
            _proxyFactory.IsProxy(proxy).Should().BeTrue();
        }

        [Test]
        public void CreateProxy_WithExistingEntity_CopiesAllProperties()
        {
            // Arrange
            var original = new TestEntity
            {
                Id = 123,
                Name = "Test Product",
                Price = 99.99m,
                CreatedDate = new DateTime(2024, 1, 1)
            };

            // Act
            var proxy = _proxyFactory.CreateProxy(original);

            // Assert
            proxy.Id.Should().Be(original.Id);
            proxy.Name.Should().Be(original.Name);
            proxy.Price.Should().Be(original.Price);
            proxy.CreatedDate.Should().Be(original.CreatedDate);
        }

        [Test]
        public void CreateProxy_WithNullEntity_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _proxyFactory.CreateProxy<TestEntity>(null));
        }

        [Test]
        public void IsProxy_WithProxyObject_ReturnsTrue()
        {
            // Arrange
            var proxy = _proxyFactory.CreateProxy<TestEntity>();

            // Act & Assert
            _proxyFactory.IsProxy(proxy).Should().BeTrue();
        }

        [Test]
        public void IsProxy_WithRegularObject_ReturnsFalse()
        {
            // Arrange
            var entity = new TestEntity();

            // Act & Assert
            _proxyFactory.IsProxy(entity).Should().BeFalse();
        }

        [Test]
        public void IsProxy_WithNull_ReturnsFalse()
        {
            // Act & Assert
            _proxyFactory.IsProxy(null).Should().BeFalse();
        }

        [Test]
        public void GetChangeTracker_WithProxy_ReturnsChangeTracker()
        {
            // Arrange
            var proxy = _proxyFactory.CreateProxy<TestEntity>();

            // Act
            var changeTracker = _proxyFactory.GetChangeTracker(proxy);

            // Assert
            changeTracker.Should().NotBeNull();
            changeTracker.Should().BeAssignableTo<IChangeTracker>();
        }

        [Test]
        public void GetChangeTracker_WithNonProxy_ThrowsArgumentException()
        {
            // Arrange
            var entity = new TestEntity();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _proxyFactory.GetChangeTracker(entity));
        }

        [Test]
        public void GetChangeTracker_WithNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _proxyFactory.GetChangeTracker(null));
        }

        [Test]
        public void CreateProxy_TracksPropertyChanges()
        {
            // Arrange
            var proxy = _proxyFactory.CreateProxy<TestEntity>();
            var changeTracker = _proxyFactory.GetChangeTracker(proxy);

            // Act
            proxy.Name = "New Name";
            proxy.Price = 150.00m;

            // Assert
            changeTracker.IsDirty.Should().BeTrue();
            changeTracker.GetDirtyProperties().Should().BeEquivalentTo(new[] { "Name", "Price" });
        }

        [Test]
        public void CreateProxy_WithExistingEntity_StartsWithCleanState()
        {
            // Arrange
            var original = new TestEntity
            {
                Id = 1,
                Name = "Original",
                Price = 100.00m
            };

            // Act
            var proxy = _proxyFactory.CreateProxy(original);
            var changeTracker = _proxyFactory.GetChangeTracker(proxy);

            // Assert
            changeTracker.IsDirty.Should().BeFalse();
            changeTracker.GetDirtyProperties().Should().BeEmpty();
        }

        [Test]
        public void CreateProxy_WhenPropertySetToSameValue_DoesNotMarkAsDirty()
        {
            // Arrange
            var original = new TestEntity { Name = "Test" };
            var proxy = _proxyFactory.CreateProxy(original);
            var changeTracker = _proxyFactory.GetChangeTracker(proxy);

            // Act
            proxy.Name = "Test"; // Same value

            // Assert
            changeTracker.IsDirty.Should().BeFalse();
        }

        [Test]
        public void CreateProxy_SupportsMultipleProxiesOfSameType()
        {
            // Act
            var proxy1 = _proxyFactory.CreateProxy<TestEntity>();
            var proxy2 = _proxyFactory.CreateProxy<TestEntity>();

            proxy1.Name = "Proxy1";
            proxy2.Name = "Proxy2";

            var tracker1 = _proxyFactory.GetChangeTracker(proxy1);
            var tracker2 = _proxyFactory.GetChangeTracker(proxy2);

            // Assert
            tracker1.GetDirtyProperties().Should().BeEquivalentTo(new[] { "Name" });
            tracker2.GetDirtyProperties().Should().BeEquivalentTo(new[] { "Name" });
            proxy1.Name.Should().Be("Proxy1");
            proxy2.Name.Should().Be("Proxy2");
        }

        [Test]
        public void CreateProxy_WithIProxyImplementation_SetsIsDirtyProperty()
        {
            // Arrange
            var proxy = _proxyFactory.CreateProxy<TestEntityWithIProxy>();
            var changeTracker = _proxyFactory.GetChangeTracker(proxy);

            // Act
            proxy.Name = "Modified";

            // Assert
            if (proxy is IProxy proxyInterface)
            {
                // The IsDirty property should reflect the change tracker state
                changeTracker.IsDirty.Should().BeTrue();
            }
        }

        public class TestEntityWithIProxy : IProxy
        {
            public virtual int Id { get; set; }
            public virtual string Name { get; set; }
            public bool IsDirty { get; set; }
        }
    }
}