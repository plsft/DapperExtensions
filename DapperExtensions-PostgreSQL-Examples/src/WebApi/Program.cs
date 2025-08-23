using DapperExtensions;
using DapperExtensions.Mapper;
using DapperExtensions.Sql;
using Npgsql;
using Shared;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Configure DapperExtensions for PostgreSQL globally
DapperExtensions.DapperExtensions.Configure(
    typeof(AutoClassMapper<>),
    new List<Assembly>(),
    new PostgreSqlDialect()
);

DapperAsyncExtensions.Configure(
    typeof(AutoClassMapper<>),
    new List<Assembly>(),
    new PostgreSqlDialect()
);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register database connection as Scoped (disposed after each request)
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("PostgreSQL") 
        ?? throw new InvalidOperationException("PostgreSQL connection string not found");
    
    var connection = new NpgsqlConnection(connectionString);
    connection.Open();
    return connection;
});

// Register services
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<OrderService>();

var app = builder.Build();

// Initialize database on startup
await InitializeDatabase(app);

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Product endpoints
var products = app.MapGroup("/api/products")
    .WithTags("Products")
    .WithOpenApi();

products.MapGet("/", async (ProductService service) => 
    Results.Ok(await service.GetAllAsync()));

products.MapGet("/{id:int}", async (int id, ProductService service) =>
{
    var product = await service.GetByIdAsync(id);
    return product is null ? Results.NotFound() : Results.Ok(product);
});

products.MapGet("/search", async (string q, ProductService service) =>
    Results.Ok(await service.SearchAsync(q)));

products.MapGet("/active", async (ProductService service) =>
    Results.Ok(await service.GetActiveProductsAsync()));

products.MapGet("/paged", async (int page, int pageSize, ProductService service) =>
{
    var result = await service.GetPagedAsync(page, pageSize);
    return Results.Ok(new
    {
        result.Items,
        result.TotalCount,
        TotalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize)
    });
});

products.MapPost("/", async (Product product, ProductService service) =>
{
    var id = await service.CreateAsync(product);
    product.Id = id;
    return Results.Created($"/api/products/{id}", product);
});

products.MapPut("/{id:int}", async (int id, Product product, ProductService service) =>
{
    product.Id = id;
    var success = await service.UpdateAsync(product);
    return success ? Results.NoContent() : Results.NotFound();
});

products.MapDelete("/{id:int}", async (int id, ProductService service) =>
{
    var success = await service.DeleteAsync(id);
    return success ? Results.NoContent() : Results.NotFound();
});

// Customer endpoints
var customers = app.MapGroup("/api/customers")
    .WithTags("Customers")
    .WithOpenApi();

customers.MapGet("/", async (CustomerService service) =>
    Results.Ok(await service.GetAllAsync()));

customers.MapGet("/{id:int}", async (int id, CustomerService service) =>
{
    var customer = await service.GetByIdAsync(id);
    return customer is null ? Results.NotFound() : Results.Ok(customer);
});

customers.MapPost("/", async (Customer customer, CustomerService service) =>
{
    var id = await service.CreateAsync(customer);
    customer.Id = id;
    return Results.Created($"/api/customers/{id}", customer);
});

// Order endpoints with transaction example
var orders = app.MapGroup("/api/orders")
    .WithTags("Orders")
    .WithOpenApi();

orders.MapPost("/", async (CreateOrderRequest request, OrderService service) =>
{
    try
    {
        var order = await service.CreateOrderAsync(request);
        return Results.Created($"/api/orders/{order.Id}", order);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

orders.MapGet("/customer/{customerId:int}", async (int customerId, OrderService service) =>
    Results.Ok(await service.GetCustomerOrdersAsync(customerId)));

app.Run();

// Database initialization
async Task InitializeDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    using var db = scope.ServiceProvider.GetRequiredService<IDbConnection>();
    
    await db.ExecuteAsync(@"
        CREATE TABLE IF NOT EXISTS Product (
            Id SERIAL PRIMARY KEY,
            Name VARCHAR(100) NOT NULL,
            Description TEXT,
            Price DECIMAL(10,2) NOT NULL,
            Stock INTEGER NOT NULL,
            CreatedAt TIMESTAMP NOT NULL,
            IsActive BOOLEAN NOT NULL
        );
        
        CREATE TABLE IF NOT EXISTS Customer (
            Id SERIAL PRIMARY KEY,
            FirstName VARCHAR(50) NOT NULL,
            LastName VARCHAR(50) NOT NULL,
            Email VARCHAR(100) UNIQUE NOT NULL,
            RegisteredDate TIMESTAMP NOT NULL
        );
        
        CREATE TABLE IF NOT EXISTS ""Order"" (
            Id SERIAL PRIMARY KEY,
            CustomerId INTEGER REFERENCES Customer(Id),
            OrderDate TIMESTAMP NOT NULL,
            TotalAmount DECIMAL(10,2) NOT NULL,
            Status VARCHAR(20) NOT NULL
        );
        
        CREATE TABLE IF NOT EXISTS OrderItem (
            Id SERIAL PRIMARY KEY,
            OrderId INTEGER REFERENCES ""Order""(Id),
            ProductId INTEGER REFERENCES Product(Id),
            Quantity INTEGER NOT NULL,
            UnitPrice DECIMAL(10,2) NOT NULL
        );
    ");
}

// DTOs
public record CreateOrderRequest(
    int CustomerId,
    List<OrderItemRequest> Items
);

public record OrderItemRequest(
    int ProductId,
    int Quantity
);

// Services
public class ProductService(IDbConnection db)
{
    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await db.GetListAsync<Product>();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await db.GetAsync<Product>(id);
    }

    public async Task<int> CreateAsync(Product product)
    {
        product.CreatedAt = DateTime.UtcNow;
        return await db.InsertAsync(product);
    }

    public async Task<bool> UpdateAsync(Product product)
    {
        return await db.UpdateAsync(product);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var product = await GetByIdAsync(id);
        return product != null && await db.DeleteAsync(product);
    }

    public async Task<IEnumerable<Product>> SearchAsync(string searchTerm)
    {
        var predicate = Predicates.Field<Product>(p => p.Name, 
            Operator.Like, $"%{searchTerm}%");
        return await db.GetListAsync<Product>(predicate);
    }

    public async Task<IEnumerable<Product>> GetActiveProductsAsync()
    {
        var predicate = Predicates.Field<Product>(p => p.IsActive, 
            Operator.Eq, true);
        var sort = new List<ISort> 
        { 
            Predicates.Sort<Product>(p => p.Name) 
        };
        return await db.GetListAsync<Product>(predicate, sort);
    }

    public async Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize)
    {
        var predicate = Predicates.Field<Product>(p => p.IsActive, 
            Operator.Eq, true);
        var sort = new List<ISort> 
        { 
            Predicates.Sort<Product>(p => p.CreatedAt, false) 
        };
        
        var items = await db.GetPageAsync<Product>(predicate, sort, 
            page - 1, pageSize);
        var totalCount = await db.CountAsync<Product>(predicate);
        
        return (items, totalCount);
    }
}

public class CustomerService(IDbConnection db)
{
    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        return await db.GetListAsync<Customer>();
    }

    public async Task<Customer?> GetByIdAsync(int id)
    {
        return await db.GetAsync<Customer>(id);
    }

    public async Task<int> CreateAsync(Customer customer)
    {
        customer.RegisteredDate = DateTime.UtcNow;
        return await db.InsertAsync(customer);
    }

    public async Task<bool> UpdateAsync(Customer customer)
    {
        return await db.UpdateAsync(customer);
    }
}

public class OrderService(IDbConnection db, ProductService productService)
{
    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        using var transaction = db.BeginTransaction();
        try
        {
            // Validate customer exists
            var customer = await db.GetAsync<Customer>(request.CustomerId, transaction);
            if (customer == null)
                throw new InvalidOperationException("Customer not found");

            // Create order
            var order = new Order
            {
                CustomerId = request.CustomerId,
                OrderDate = DateTime.UtcNow,
                Status = "Pending",
                TotalAmount = 0
            };

            order.Id = await db.InsertAsync(order, transaction);

            decimal totalAmount = 0;

            // Add order items
            foreach (var item in request.Items)
            {
                var product = await db.GetAsync<Product>(item.ProductId, transaction);
                if (product == null)
                    throw new InvalidOperationException($"Product {item.ProductId} not found");

                if (product.Stock < item.Quantity)
                    throw new InvalidOperationException($"Insufficient stock for {product.Name}");

                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                };

                await db.InsertAsync(orderItem, transaction);

                // Update stock
                product.Stock -= item.Quantity;
                await db.UpdateAsync(product, transaction);

                totalAmount += product.Price * item.Quantity;
            }

            // Update order total
            order.TotalAmount = totalAmount;
            await db.UpdateAsync(order, transaction);

            transaction.Commit();
            return order;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<Order>> GetCustomerOrdersAsync(int customerId)
    {
        var predicate = Predicates.Field<Order>(o => o.CustomerId, 
            Operator.Eq, customerId);
        var sort = new List<ISort> 
        { 
            Predicates.Sort<Order>(o => o.OrderDate, false) 
        };
        return await db.GetListAsync<Order>(predicate, sort);
    }
}