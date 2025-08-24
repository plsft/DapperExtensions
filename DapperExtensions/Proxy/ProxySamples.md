# Proxy and Dirty Tracking Samples

## Table of Contents
1. [Basic Setup](#basic-setup)
2. [Simple Dirty Tracking](#simple-dirty-tracking)
3. [Working with Complex Entities](#working-with-complex-entities)
4. [Bulk Operations with IAsyncEnumerable](#bulk-operations-with-iasyncenumerable)
5. [Advanced Scenarios](#advanced-scenarios)
6. [Performance Comparison](#performance-comparison)

## Basic Setup

### Entity Definition
```csharp
public class Product
{
    public virtual int Id { get; set; }
    public virtual string Name { get; set; }
    public virtual string Description { get; set; }
    public virtual decimal Price { get; set; }
    public virtual int StockQuantity { get; set; }
    public virtual DateTime LastModified { get; set; }
    public virtual bool IsActive { get; set; }
}

public class Customer
{
    public virtual int Id { get; set; }
    public virtual string FirstName { get; set; }
    public virtual string LastName { get; set; }
    public virtual string Email { get; set; }
    public virtual DateTime CreatedDate { get; set; }
    public virtual DateTime? LastPurchaseDate { get; set; }
    public virtual decimal TotalPurchases { get; set; }
}
```

### Configuration
```csharp
// Global configuration
public static void ConfigureDapperExtensions()
{
    var config = new DapperExtensionsConfiguration(
        typeof(AutoClassMapper<>),
        new List<Assembly> { typeof(Product).Assembly },
        new SqlServerDialect()
    );
    
    // Enable proxy generation
    config.SetProxyGeneration(true);
    
    DapperExtensions.Configure(config);
    DapperAsyncExtensions.Configure(config);
}
```

## Simple Dirty Tracking

### Basic Update Example
```csharp
public async Task UpdateProductPrice(int productId, decimal newPrice)
{
    using var connection = new SqlConnection(connectionString);
    
    // Get product as proxy
    var product = await connection.GetAsync<Product>(productId);
    
    // Only price will be updated
    product.Price = newPrice;
    product.LastModified = DateTime.UtcNow;
    
    // Generates: UPDATE Product SET Price = @Price, LastModified = @LastModified WHERE Id = @Id
    await connection.UpdateDirtyAsync(product);
}
```

### Conditional Updates
```csharp
public async Task ApplyDiscountIfEligible(int productId, decimal discountPercent)
{
    using var connection = new SqlConnection(connectionString);
    
    var product = await connection.GetAsync<Product>(productId);
    
    // Check if eligible for discount
    if (product.Price > 100 && product.StockQuantity > 50)
    {
        // Only update if conditions are met
        product.Price = product.Price * (1 - discountPercent / 100);
        
        // Only Price is updated, not StockQuantity or other fields
        await connection.UpdateDirtyAsync(product);
    }
}
```

### No Changes Scenario
```csharp
public async Task<bool> TryUpdateProduct(int productId, string newName)
{
    using var connection = new SqlConnection(connectionString);
    
    var product = await connection.GetAsync<Product>(productId);
    
    // Check if update is needed
    if (product.Name == newName)
    {
        // No changes made - UpdateDirty will detect this
        return await connection.UpdateDirtyAsync(product); // Returns false, no SQL executed
    }
    
    product.Name = newName;
    product.LastModified = DateTime.UtcNow;
    
    return await connection.UpdateDirtyAsync(product); // Returns true
}
```

## Working with Complex Entities

### Multiple Property Updates
```csharp
public async Task UpdateCustomerProfile(CustomerUpdateDto dto)
{
    using var connection = new SqlConnection(connectionString);
    
    var customer = await connection.GetAsync<Customer>(dto.CustomerId);
    
    // Update only properties that have values in DTO
    if (!string.IsNullOrEmpty(dto.FirstName))
        customer.FirstName = dto.FirstName;
        
    if (!string.IsNullOrEmpty(dto.LastName))
        customer.LastName = dto.LastName;
        
    if (!string.IsNullOrEmpty(dto.Email))
        customer.Email = dto.Email;
    
    // Only modified properties are updated
    await connection.UpdateDirtyAsync(customer);
}
```

### Working with Change Tracker
```csharp
public async Task<string[]> AuditCustomerChanges(int customerId, CustomerUpdateDto dto)
{
    using var connection = new SqlConnection(connectionString);
    var config = DapperExtensions.Configuration;
    
    var customer = await connection.GetAsync<Customer>(customerId);
    
    // Get the change tracker
    var changeTracker = config.ProxyFactory.GetChangeTracker(customer);
    
    // Make changes
    customer.FirstName = dto.FirstName;
    customer.Email = dto.Email;
    
    // Get list of changed properties before update
    var changedProperties = changeTracker.GetDirtyProperties().ToArray();
    
    // Log audit trail
    foreach (var prop in changedProperties)
    {
        Console.WriteLine($"Property '{prop}' was modified");
    }
    
    await connection.UpdateDirtyAsync(customer);
    
    return changedProperties;
}
```

## Bulk Operations with IAsyncEnumerable

### Streaming Updates
```csharp
public async Task<int> UpdatePricesFromSupplierFeed()
{
    using var connection = new SqlConnection(connectionString);
    
    // Stream products that need price updates
    async IAsyncEnumerable<Product> GetProductsToUpdate()
    {
        await foreach (var priceUpdate in GetSupplierPriceFeed())
        {
            var product = await connection.GetAsync<Product>(priceUpdate.ProductId);
            if (product != null && product.Price != priceUpdate.NewPrice)
            {
                product.Price = priceUpdate.NewPrice;
                product.LastModified = DateTime.UtcNow;
                yield return product;
            }
        }
    }
    
    // Update all products with only changed properties
    var updatedCount = await connection.UpdateDirtyAsync(GetProductsToUpdate());
    return updatedCount;
}
```

### Batch Processing Large Datasets
```csharp
public async Task ProcessLargeCustomerUpdate(CancellationToken cancellationToken)
{
    using var connection = new SqlConnection(connectionString);
    
    // Process customers in batches
    async IAsyncEnumerable<Customer> GetCustomersToProcess()
    {
        var customerIds = await GetInactiveCustomerIds();
        
        foreach (var batch in customerIds.Chunk(100))
        {
            var customers = await connection.GetListAsync<Customer>(
                Predicates.Field<Customer>(c => c.Id, Operator.Eq, batch)
            );
            
            foreach (var customer in customers)
            {
                // Apply business logic
                if (customer.LastPurchaseDate < DateTime.UtcNow.AddYears(-1))
                {
                    customer.IsActive = false;
                    yield return customer;
                }
            }
            
            // Allow cancellation between batches
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
    
    // Process in batches of 50 with cancellation support
    var processedCount = await connection.UpdateDirtyBatchAsync(
        GetCustomersToProcess(), 
        batchSize: 50,
        cancellationToken: cancellationToken
    );
}
```

### Parallel Processing with IAsyncEnumerable
```csharp
public async Task ParallelProductUpdate()
{
    using var connection = new SqlConnection(connectionString);
    
    // Create multiple streams
    async IAsyncEnumerable<Product> GetProductStream(int categoryId)
    {
        var products = await connection.GetListAsync<Product>(
            Predicates.Field<Product>(p => p.CategoryId, Operator.Eq, categoryId)
        );
        
        foreach (var product in products)
        {
            // Apply category-specific logic
            product.Price = CalculateNewPrice(product, categoryId);
            yield return product;
        }
    }
    
    // Process multiple categories in parallel
    var tasks = new[]
    {
        connection.UpdateDirtyAsync(GetProductStream(1)),
        connection.UpdateDirtyAsync(GetProductStream(2)),
        connection.UpdateDirtyAsync(GetProductStream(3))
    };
    
    var results = await Task.WhenAll(tasks);
    var totalUpdated = results.Sum();
}
```

## Advanced Scenarios

### Mixed Proxy and Non-Proxy Entities
```csharp
public async Task MixedEntityUpdate()
{
    using var connection = new SqlConnection(connectionString);
    
    // Get entity as proxy
    var proxyProduct = await connection.GetAsync<Product>(1);
    
    // Create new entity (not a proxy)
    var newProduct = new Product
    {
        Name = "New Product",
        Price = 99.99m,
        StockQuantity = 100
    };
    
    // Insert new product
    var newId = await connection.InsertAsync(newProduct);
    
    // Update proxy entity
    proxyProduct.StockQuantity += 10;
    await connection.UpdateDirtyAsync(proxyProduct); // Only updates StockQuantity
    
    // Update non-proxy entity  
    newProduct.Price = 89.99m;
    await connection.UpdateDirtyAsync(newProduct); // Falls back to full update
}
```

### Manual Proxy Creation
```csharp
public async Task ManualProxyManagement()
{
    var config = DapperExtensions.Configuration;
    var proxyFactory = config.ProxyFactory;
    
    // Create proxy manually
    var product = proxyFactory.CreateProxy<Product>();
    product.Id = 1;
    product.Name = "Test Product";
    product.Price = 50.00m;
    
    // Get change tracker
    var tracker = proxyFactory.GetChangeTracker(product);
    
    // Mark as clean (simulate loaded from DB)
    tracker.MarkAsClean();
    
    // Now make changes
    product.Price = 60.00m;
    
    // Check what's dirty
    var dirtyProps = tracker.GetDirtyProperties(); // Returns ["Price"]
    
    using var connection = new SqlConnection(connectionString);
    await connection.UpdateDirtyAsync(product);
}
```

### Transactions with Dirty Tracking
```csharp
public async Task TransactionalUpdate()
{
    using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    
    using var transaction = connection.BeginTransaction();
    try
    {
        // Get multiple entities
        var product = await connection.GetAsync<Product>(1, transaction);
        var customer = await connection.GetAsync<Customer>(1, transaction);
        
        // Update both
        product.StockQuantity -= 1;
        customer.TotalPurchases += product.Price;
        customer.LastPurchaseDate = DateTime.UtcNow;
        
        // Both updates use dirty tracking
        await connection.UpdateDirtyAsync(product, transaction);
        await connection.UpdateDirtyAsync(customer, transaction);
        
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

## Performance Comparison

### Traditional Update vs Dirty Tracking
```csharp
public async Task PerformanceComparison()
{
    using var connection = new SqlConnection(connectionString);
    var stopwatch = new Stopwatch();
    
    // Traditional update - all columns
    var product1 = await connection.GetAsync<Product>(1);
    stopwatch.Start();
    product1.Price = 99.99m;
    await connection.UpdateAsync(product1);
    stopwatch.Stop();
    Console.WriteLine($"Full update: {stopwatch.ElapsedMilliseconds}ms");
    
    // Dirty tracking - only changed columns
    var product2 = await connection.GetAsync<Product>(2);
    stopwatch.Restart();
    product2.Price = 99.99m;
    await connection.UpdateDirtyAsync(product2);
    stopwatch.Stop();
    Console.WriteLine($"Dirty update: {stopwatch.ElapsedMilliseconds}ms");
    
    // Bulk comparison
    async IAsyncEnumerable<Product> GetProducts()
    {
        for (int i = 0; i < 1000; i++)
        {
            var p = await connection.GetAsync<Product>(i);
            p.Price *= 1.1m; // 10% increase
            yield return p;
        }
    }
    
    stopwatch.Restart();
    await connection.UpdateDirtyBatchAsync(GetProducts(), batchSize: 100);
    stopwatch.Stop();
    Console.WriteLine($"Batch dirty update (1000 items): {stopwatch.ElapsedMilliseconds}ms");
}
```

### Memory Efficiency with Streaming
```csharp
public async Task MemoryEfficientProcessing()
{
    using var connection = new SqlConnection(connectionString);
    
    // Memory efficient - processes one at a time
    async IAsyncEnumerable<Product> ProcessProducts()
    {
        const int pageSize = 100;
        int page = 1;
        bool hasMore = true;
        
        while (hasMore)
        {
            var products = await connection.GetPageAsync<Product>(
                null, null, page++, pageSize
            );
            
            hasMore = products.Any();
            
            foreach (var product in products)
            {
                // Process and yield immediately
                product.LastModified = DateTime.UtcNow;
                yield return product;
            }
        }
    }
    
    // Process without loading all into memory
    var count = await connection.UpdateDirtyAsync(ProcessProducts());
    Console.WriteLine($"Processed {count} products efficiently");
}
```