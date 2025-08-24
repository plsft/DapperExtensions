using DapperExtensions.Mapper;
using DapperExtensions.Proxy;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using System.Data;

namespace DapperExtensions.Test.Proxy
{
    [TestFixture]
    public class ProxyIntegrationTests
    {
        private Mock<IDbConnection> _mockConnection;
        private DapperImplementor _implementor;
        private DapperExtensionsConfiguration _configuration;

        public class Product
        {
            public virtual int Id { get; set; }
            public virtual string Name { get; set; }
            public virtual string Description { get; set; }
            public virtual decimal Price { get; set; }
            public virtual int StockQuantity { get; set; }
            public virtual DateTime LastModified { get; set; }
        }

        [SetUp]
        public void Setup()
        {
            _mockConnection = new Mock<IDbConnection>();
            _configuration = new DapperExtensionsConfiguration();
            _configuration.SetProxyGeneration(true);
            
            var sqlGenerator = new SqlGeneratorImpl(_configuration);
            _implementor = new DapperImplementor(sqlGenerator);
        }

        [Test]
        public void UpdateDirty_WithNoChanges_ReturnsFalse()
        {
            // Arrange
            var product = _configuration.ProxyFactory.CreateProxy<Product>();
            product.Id = 1;
            product.Name = "Test Product";
            
            var changeTracker = _configuration.ProxyFactory.GetChangeTracker(product);
            changeTracker.MarkAsClean();

            // Act
            var result = _implementor.UpdateDirty(_mockConnection.Object, product, null, null);

            // Assert
            result.Should().BeFalse();
            _mockConnection.Verify(c => c.Execute(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<int?>(), It.IsAny<CommandType>()), Times.Never);
        }

        [Test]
        public void UpdateDirty_WithChangedProperties_UpdatesOnlyDirtyColumns()
        {
            // Arrange
            var product = _configuration.ProxyFactory.CreateProxy<Product>();
            product.Id = 1;
            product.Name = "Original Name";
            product.Description = "Original Description";
            product.Price = 100m;
            product.StockQuantity = 50;
            
            var changeTracker = _configuration.ProxyFactory.GetChangeTracker(product);
            changeTracker.MarkAsClean();

            // Make changes
            product.Name = "Updated Name";
            product.Price = 150m;

            _mockConnection.Setup(c => c.Execute(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<int?>(), It.IsAny<CommandType>()))
                .Returns(1);

            // Act
            var result = _implementor.UpdateDirty(_mockConnection.Object, product, null, null);

            // Assert
            result.Should().BeTrue();
            changeTracker.GetDirtyProperties().Should().BeEmpty(); // Should be marked clean after update
            
            // Verify that Execute was called
            _mockConnection.Verify(c => c.Execute(
                It.IsAny<string>(), 
                It.IsAny<object>(), 
                It.IsAny<IDbTransaction>(), 
                It.IsAny<int?>(), 
                It.IsAny<CommandType>()), 
                Times.Once);
        }

        [Test]
        public void UpdateDirty_WithNonProxyEntity_FallsBackToRegularUpdate()
        {
            // Arrange
            var product = new Product
            {
                Id = 1,
                Name = "Test Product",
                Price = 100m
            };

            _mockConnection.Setup(c => c.Execute(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<int?>(), It.IsAny<CommandType>()))
                .Returns(1);

            // Act
            var result = _implementor.UpdateDirty(_mockConnection.Object, product, null, null);

            // Assert
            result.Should().BeTrue();
            _mockConnection.Verify(c => c.Execute(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<int?>(), It.IsAny<CommandType>()), Times.Once);
        }

        [Test]
        public void UpdateDirty_WithIgnoredProperties_DoesNotUpdateThem()
        {
            // Arrange
            var product = _configuration.ProxyFactory.CreateProxy<ProductWithIgnored>();
            product.Id = 1;
            product.Name = "Test";
            product.InternalCode = "CODE123"; // This should be ignored
            
            var changeTracker = _configuration.ProxyFactory.GetChangeTracker(product);
            changeTracker.MarkAsClean();

            product.Name = "Updated";
            product.InternalCode = "CODE456";

            _mockConnection.Setup(c => c.Execute(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<int?>(), It.IsAny<CommandType>()))
                .Returns(1);

            // Act
            var result = _implementor.UpdateDirty(_mockConnection.Object, product, null, null);

            // Assert
            result.Should().BeTrue();
        }

        public class ProductWithIgnored
        {
            public virtual int Id { get; set; }
            public virtual string Name { get; set; }
            [Ignored]
            public virtual string InternalCode { get; set; }
        }

        [Test]
        public void MappColumns_WithProxyGenerationEnabled_ReturnsProxies()
        {
            // Arrange
            _configuration.SetProxyGeneration(true);
            var dynamicResults = new List<dynamic>
            {
                new Product { Id = 1, Name = "Product1" },
                new Product { Id = 2, Name = "Product2" }
            };

            // Act
            var results = _implementor.MappColumns<Product>(dynamicResults);

            // Assert
            results.Should().HaveCount(2);
            foreach (var result in results)
            {
                _configuration.ProxyFactory.IsProxy(result).Should().BeTrue();
            }
        }

        [Test]
        public void MappColumns_WithProxyGenerationDisabled_ReturnsRegularEntities()
        {
            // Arrange
            _configuration.SetProxyGeneration(false);
            var dynamicResults = new List<dynamic>
            {
                new Product { Id = 1, Name = "Product1" },
                new Product { Id = 2, Name = "Product2" }
            };

            // Act
            var results = _implementor.MappColumns<Product>(dynamicResults);

            // Assert
            results.Should().HaveCount(2);
            foreach (var result in results)
            {
                _configuration.ProxyFactory.IsProxy(result).Should().BeFalse();
            }
        }
    }
}