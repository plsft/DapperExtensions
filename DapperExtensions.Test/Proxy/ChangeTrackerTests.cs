using DapperExtensions.Proxy;
using FluentAssertions;
using NUnit.Framework;

namespace DapperExtensions.Test.Proxy
{
    [TestFixture]
    public class ChangeTrackerTests
    {
        private ChangeTracker _changeTracker;

        [SetUp]
        public void Setup()
        {
            _changeTracker = new ChangeTracker();
        }

        [Test]
        public void IsDirty_WhenNoChanges_ReturnsFalse()
        {
            // Assert
            _changeTracker.IsDirty.Should().BeFalse();
        }

        [Test]
        public void IsDirty_WhenPropertyMarkedDirty_ReturnsTrue()
        {
            // Act
            _changeTracker.MarkPropertyDirty("Name");

            // Assert
            _changeTracker.IsDirty.Should().BeTrue();
        }

        [Test]
        public void MarkPropertyDirty_WithValidPropertyName_AddsToTracking()
        {
            // Act
            _changeTracker.MarkPropertyDirty("FirstName");
            _changeTracker.MarkPropertyDirty("LastName");

            // Assert
            _changeTracker.GetDirtyProperties().Should().BeEquivalentTo(new[] { "FirstName", "LastName" });
        }

        [Test]
        public void MarkPropertyDirty_WithNullPropertyName_DoesNotAddToTracking()
        {
            // Act
            _changeTracker.MarkPropertyDirty(null);

            // Assert
            _changeTracker.IsDirty.Should().BeFalse();
            _changeTracker.GetDirtyProperties().Should().BeEmpty();
        }

        [Test]
        public void MarkPropertyDirty_WithEmptyPropertyName_DoesNotAddToTracking()
        {
            // Act
            _changeTracker.MarkPropertyDirty(string.Empty);

            // Assert
            _changeTracker.IsDirty.Should().BeFalse();
            _changeTracker.GetDirtyProperties().Should().BeEmpty();
        }

        [Test]
        public void MarkPropertyDirty_WhenTrackingStopped_DoesNotAddToTracking()
        {
            // Arrange
            _changeTracker.StopTracking();

            // Act
            _changeTracker.MarkPropertyDirty("Name");

            // Assert
            _changeTracker.IsDirty.Should().BeFalse();
            _changeTracker.GetDirtyProperties().Should().BeEmpty();
        }

        [Test]
        public void MarkAsClean_RemovesAllDirtyProperties()
        {
            // Arrange
            _changeTracker.MarkPropertyDirty("FirstName");
            _changeTracker.MarkPropertyDirty("LastName");
            _changeTracker.MarkPropertyDirty("Email");

            // Act
            _changeTracker.MarkAsClean();

            // Assert
            _changeTracker.IsDirty.Should().BeFalse();
            _changeTracker.GetDirtyProperties().Should().BeEmpty();
        }

        [Test]
        public void IsPropertyDirty_WhenPropertyIsDirty_ReturnsTrue()
        {
            // Arrange
            _changeTracker.MarkPropertyDirty("Name");

            // Act & Assert
            _changeTracker.IsPropertyDirty("Name").Should().BeTrue();
        }

        [Test]
        public void IsPropertyDirty_WhenPropertyIsNotDirty_ReturnsFalse()
        {
            // Arrange
            _changeTracker.MarkPropertyDirty("Name");

            // Act & Assert
            _changeTracker.IsPropertyDirty("Email").Should().BeFalse();
        }

        [Test]
        public void GetDirtyProperties_ReturnsOnlyDirtyProperties()
        {
            // Arrange
            _changeTracker.MarkPropertyDirty("FirstName");
            _changeTracker.MarkPropertyDirty("LastName");
            _changeTracker.MarkPropertyDirty("FirstName"); // Mark same property again

            // Act
            var dirtyProperties = _changeTracker.GetDirtyProperties();

            // Assert
            dirtyProperties.Should().BeEquivalentTo(new[] { "FirstName", "LastName" });
        }

        [Test]
        public void StartTracking_AfterStopTracking_ResumesTracking()
        {
            // Arrange
            _changeTracker.MarkPropertyDirty("InitialProperty");
            _changeTracker.StopTracking();
            _changeTracker.MarkPropertyDirty("IgnoredProperty");

            // Act
            _changeTracker.StartTracking();
            _changeTracker.MarkPropertyDirty("TrackedProperty");

            // Assert
            _changeTracker.GetDirtyProperties().Should().BeEquivalentTo(new[] { "InitialProperty", "TrackedProperty" });
        }

        [Test]
        public void ChangeTracker_IsThreadSafe()
        {
            // Arrange
            var tasks = new List<Task>();
            var properties = Enumerable.Range(0, 1000).Select(i => $"Property{i}").ToList();

            // Act
            foreach (var property in properties)
            {
                tasks.Add(Task.Run(() => _changeTracker.MarkPropertyDirty(property)));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            _changeTracker.GetDirtyProperties().Count().Should().Be(properties.Count);
        }
    }
}