using DapperExtensions.Proxy;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using System.Data;
using System.Runtime.CompilerServices;

namespace DapperExtensions.Test.Proxy
{
    [TestFixture]
    public class AsyncEnumerableTests
    {
        private Mock<IDbConnection> _mockConnection;
        private DapperExtensionsConfiguration _configuration;

        public class TestEntity
        {
            public virtual int Id { get; set; }
            public virtual string Name { get; set; }
            public virtual decimal Value { get; set; }
        }

        [SetUp]
        public void Setup()
        {
            _mockConnection = new Mock<IDbConnection>();
            _configuration = new DapperExtensionsConfiguration();
            _configuration.SetProxyGeneration(true);
            DapperAsyncExtensions.Configure(_configuration);
        }

        private static async IAsyncEnumerable<TestEntity> GenerateEntitiesAsync(int count, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield(); // Simulate async work
                yield return new TestEntity { Id = i, Name = $"Entity{i}", Value = i * 10 };
            }
        }

        private static async IAsyncEnumerable<TestEntity> GenerateProxiesAsync(ProxyFactory factory, int count)
        {
            for (int i = 0; i < count; i++)
            {
                await Task.Yield();
                var proxy = factory.CreateProxy<TestEntity>();
                proxy.Id = i;
                proxy.Name = $"Proxy{i}";
                proxy.Value = i * 10;
                yield return proxy;
            }
        }

        [Test]
        public async Task BatchAsync_WithVariousSizes_CreatesCorrectBatches()
        {
            // Arrange
            var entities = GenerateEntitiesAsync(25);

            // Act
            var batches = await entities.BatchAsync(10).ToListAsync();

            // Assert
            batches.Should().HaveCount(3);
            batches[0].Should().HaveCount(10);
            batches[1].Should().HaveCount(10);
            batches[2].Should().HaveCount(5);
        }

        [Test]
        public async Task BatchAsync_WithCancellation_StopsProcessing()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var entities = GenerateEntitiesAsync(100, cts.Token);
            var processedBatches = 0;

            // Act
            try
            {
                await foreach (var batch in entities.BatchAsync(10, cts.Token))
                {
                    processedBatches++;
                    if (processedBatches == 2)
                    {
                        cts.Cancel();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Assert
            processedBatches.Should().Be(2);
        }

        [Test]
        public async Task UpdateAsync_WithAsyncEnumerable_UpdatesAllEntities()
        {
            // Arrange
            var entities = GenerateEntitiesAsync(5);
            var updateCount = 0;

            _mockConnection.Setup(c => c.UpdateAsync(It.IsAny<TestEntity>(), It.IsAny<IDbTransaction>(), It.IsAny<int?>(), It.IsAny<bool>()))
                .ReturnsAsync(() =>
                {
                    updateCount++;
                    return true;
                });

            // Act
            var result = await _mockConnection.Object.UpdateAsync(entities);

            // Assert
            result.Should().Be(5);
            updateCount.Should().Be(5);
        }

        [Test]
        public async Task UpdateDirtyAsync_WithProxyEntities_UpdatesOnlyDirtyOnes()
        {
            // Arrange
            var factory = _configuration.ProxyFactory;
            var proxies = GenerateProxiesAsync(factory, 3);
            var updateCount = 0;

            _mockConnection.Setup(c => c.UpdateDirtyAsync(It.IsAny<TestEntity>(), It.IsAny<IDbTransaction>(), It.IsAny<int?>(), It.IsAny<bool>()))
                .ReturnsAsync((TestEntity entity, IDbTransaction _, int? __, bool ___) =>
                {
                    if (factory.IsProxy(entity))
                    {
                        var tracker = factory.GetChangeTracker(entity);
                        if (tracker.IsDirty)
                        {
                            updateCount++;
                            return true;
                        }
                    }
                    return false;
                });

            // Act - modify proxies during enumeration
            var modifiedCount = 0;
            async IAsyncEnumerable<TestEntity> ModifyProxies()
            {
                await foreach (var proxy in proxies)
                {
                    if (proxy.Id % 2 == 0) // Only modify even IDs
                    {
                        proxy.Name = "Modified";
                        modifiedCount++;
                    }
                    yield return proxy;
                }
            }

            var result = await _mockConnection.Object.UpdateDirtyAsync(ModifyProxies());

            // Assert
            modifiedCount.Should().Be(2); // IDs 0 and 2
            result.Should().Be(2);
        }

        [Test]
        public async Task InsertAsync_WithAsyncEnumerable_InsertsAllEntities()
        {
            // Arrange
            var entities = GenerateEntitiesAsync(3);
            var insertCount = 0;

            _mockConnection.Setup(c => c.InsertAsync(It.IsAny<TestEntity>(), It.IsAny<IDbTransaction>(), It.IsAny<int?>()))
                .ReturnsAsync(() =>
                {
                    insertCount++;
                    return insertCount; // Return ID
                });

            // Act
            var result = await _mockConnection.Object.InsertAsync(entities);

            // Assert
            result.Should().Be(3);
            insertCount.Should().Be(3);
        }

        [Test]
        public async Task DeleteAsync_WithAsyncEnumerable_DeletesAllEntities()
        {
            // Arrange
            var entities = GenerateEntitiesAsync(4);
            var deleteCount = 0;

            _mockConnection.Setup(c => c.DeleteAsync(It.IsAny<TestEntity>(), It.IsAny<IDbTransaction>(), It.IsAny<int?>()))
                .ReturnsAsync(() =>
                {
                    deleteCount++;
                    return true;
                });

            // Act
            var result = await _mockConnection.Object.DeleteAsync(entities);

            // Assert
            result.Should().Be(4);
            deleteCount.Should().Be(4);
        }

        [Test]
        public async Task UpdateDirtyBatchAsync_ProcessesInBatches()
        {
            // Arrange
            var entities = GenerateEntitiesAsync(25);
            var batchCount = 0;
            var totalUpdated = 0;

            _mockConnection.Setup(c => c.UpdateDirtyAsync(It.IsAny<TestEntity>(), It.IsAny<IDbTransaction>(), It.IsAny<int?>(), It.IsAny<bool>()))
                .ReturnsAsync(() =>
                {
                    totalUpdated++;
                    return true;
                });

            // Act
            var result = await _mockConnection.Object.UpdateDirtyBatchAsync(entities, batchSize: 10);

            // Assert
            result.Should().Be(25);
            totalUpdated.Should().Be(25);
        }

        [Test]
        public async Task AsyncEnumerable_WithEmptyStream_HandlesGracefully()
        {
            // Arrange
            async IAsyncEnumerable<TestEntity> EmptyStream()
            {
                await Task.CompletedTask;
                yield break;
            }

            // Act
            var updateResult = await _mockConnection.Object.UpdateAsync(EmptyStream());
            var insertResult = await _mockConnection.Object.InsertAsync(EmptyStream());
            var deleteResult = await _mockConnection.Object.DeleteAsync(EmptyStream());

            // Assert
            updateResult.Should().Be(0);
            insertResult.Should().Be(0);
            deleteResult.Should().Be(0);
        }

        [Test]
        public async Task AsyncEnumerable_WithException_PropagatesError()
        {
            // Arrange
            async IAsyncEnumerable<TestEntity> FaultyStream()
            {
                yield return new TestEntity { Id = 1 };
                yield return new TestEntity { Id = 2 };
                throw new InvalidOperationException("Stream error");
            }

            _mockConnection.Setup(c => c.UpdateAsync(It.IsAny<TestEntity>(), It.IsAny<IDbTransaction>(), It.IsAny<int?>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

            // Act & Assert
            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _mockConnection.Object.UpdateAsync(FaultyStream());
            });

            exception.Message.Should().Be("Stream error");
        }
    }
}