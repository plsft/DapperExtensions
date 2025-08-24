# Proxy and Dirty Tracking Support

This feature adds proxy generation and dirty tracking capabilities to DapperExtensions, similar to Dapper.Contrib's IProxy implementation.

## Features

- **Automatic Proxy Generation**: Entities retrieved from the database can be automatically wrapped in proxies
- **Dirty Tracking**: Only properties that have been modified are included in UPDATE statements
- **IAsyncEnumerable Support**: Efficient bulk operations with async streams
- **Batch Processing**: Process large datasets in configurable batches
- **Zero Configuration**: Works with existing POCOs without modifications

## Configuration

Enable proxy generation globally:

```csharp
DapperExtensions.Configure()
    .SetProxyGeneration(true);
```

Or for async operations:

```csharp
DapperAsyncExtensions.Configure()
    .SetProxyGeneration(true);
```

## Usage

### Basic Dirty Tracking

```csharp
// Entity retrieved as proxy with change tracking
var person = await connection.GetAsync<Person>(1);

// Only these properties will be updated
person.FirstName = "John";
person.LastName = "Doe";

// Generates: UPDATE Person SET FirstName = @FirstName, LastName = @LastName WHERE Id = @Id
await connection.UpdateDirtyAsync(person);
```

### IAsyncEnumerable Support

```csharp
// Process large datasets efficiently
async IAsyncEnumerable<Person> GetPeopleAsync()
{
    await foreach (var person in largeDataSource)
    {
        yield return person;
    }
}

// Update all with dirty tracking
var people = GetPeopleAsync();
var count = await connection.UpdateDirtyAsync(people);

// Batch processing for better performance
var batchCount = await connection.UpdateDirtyBatchAsync(people, batchSize: 100);
```

### Bulk Operations

```csharp
// All bulk operations support IAsyncEnumerable
await connection.InsertAsync(peopleStream);
await connection.UpdateAsync(peopleStream);
await connection.DeleteAsync(peopleStream);

// With cancellation support
await connection.UpdateDirtyAsync(peopleStream, cancellationToken: cts.Token);
```

## Requirements

- Entities must have virtual properties for proxy generation to work
- Castle.Core is used for dynamic proxy generation
- .NET 6.0+ for IAsyncEnumerable support

## Performance Benefits

- **Reduced Database Load**: Only modified columns are updated
- **Smaller SQL Statements**: Fewer parameters mean faster query parsing
- **Network Efficiency**: Less data transferred over the network
- **Async Streaming**: Process large datasets without loading all into memory

## API Reference

### Synchronous Methods

- `UpdateDirty<T>(entity)` - Updates only dirty properties of a proxy entity

### Asynchronous Methods

- `UpdateDirtyAsync<T>(entity)` - Async version of UpdateDirty
- `UpdateDirtyAsync<T>(IAsyncEnumerable<T>)` - Updates multiple entities with dirty tracking
- `UpdateDirtyBatchAsync<T>(IAsyncEnumerable<T>, batchSize)` - Batch updates with configurable size
- `InsertAsync<T>(IAsyncEnumerable<T>)` - Bulk insert with async enumerable
- `DeleteAsync<T>(IAsyncEnumerable<T>)` - Bulk delete with async enumerable
- `BatchAsync<T>(IAsyncEnumerable<T>, batchSize)` - Helper to batch any async enumerable

## Implementation Details

The proxy implementation uses:
- Castle.Core for dynamic proxy generation
- Thread-safe change tracking with ConcurrentDictionary
- Interceptors to track property modifications
- Integration with existing DapperExtensions infrastructure