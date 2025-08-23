# DapperExtensions PostgreSQL Examples

Modern .NET 8 examples demonstrating how to use DapperExtensions with PostgreSQL database.

## Overview

This repository contains two example projects:
- **WebApi**: ASP.NET Core 8 Web API with minimal APIs
- **ConsoleApp**: .NET 8 Console application with hosted services

Both examples demonstrate:
- Proper dependency injection with scoped database connections
- CRUD operations using DapperExtensions
- Complex queries with predicates
- Pagination support
- Transaction handling
- Modern C# features and patterns

## Prerequisites

- .NET 8 SDK
- PostgreSQL database server
- Visual Studio 2022 or VS Code (optional)

## Getting Started

1. Clone the repository:
```bash
git clone https://github.com/plsft/dapperextensions-postgresql-examples.git
cd dapperextensions-postgresql-examples
```

2. Update the connection string in `appsettings.json` for both projects:
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=dapperext_demo;Username=postgres;Password=yourpassword"
  }
}
```

3. Run the Web API:
```bash
cd src/WebApi
dotnet run
```

4. Run the Console App:
```bash
cd src/ConsoleApp
dotnet run
```

## Project Structure

```
├── src/
│   ├── WebApi/
│   │   ├── Program.cs
│   │   ├── Models/
│   │   ├── Services/
│   │   └── appsettings.json
│   ├── ConsoleApp/
│   │   ├── Program.cs
│   │   ├── Models/
│   │   ├── Demos/
│   │   └── appsettings.json
│   └── Shared/
│       └── Entities.cs
```

## Key Features

### DapperExtensions Configuration

```csharp
// Configure for PostgreSQL
DapperExtensions.Configure(
    typeof(AutoClassMapper<>),
    new List<Assembly>(),
    new PostgreSqlDialect()
);
```

### Dependency Injection

```csharp
// Scoped connection - disposed after each request
services.AddScoped<IDbConnection>(sp =>
{
    var connectionString = configuration.GetConnectionString("PostgreSQL");
    var connection = new NpgsqlConnection(connectionString);
    connection.Open();
    return connection;
});
```

### CRUD Operations

```csharp
// Insert
var id = await db.InsertAsync(product);

// Get by ID
var product = await db.GetAsync<Product>(id);

// Update
await db.UpdateAsync(product);

// Delete
await db.DeleteAsync(product);

// List with predicate
var predicate = Predicates.Field<Product>(p => p.IsActive, Operator.Eq, true);
var activeProducts = await db.GetListAsync<Product>(predicate);
```

### Complex Queries

```csharp
// Multiple conditions
var predicateGroup = Predicates.Group(GroupOperator.And,
    Predicates.Field<Product>(p => p.IsActive, Operator.Eq, true),
    Predicates.Field<Product>(p => p.Stock, Operator.Gt, 0)
);
var availableProducts = await db.GetListAsync<Product>(predicateGroup);

// Pagination
var sort = new List<ISort> { Predicates.Sort<Product>(p => p.Price, false) };
var page = await db.GetPageAsync<Product>(predicate, sort, pageNumber - 1, pageSize);
```

## API Endpoints (Web API)

- `GET /api/products` - Get all products
- `GET /api/products/{id}` - Get product by ID
- `GET /api/products/search?q={term}` - Search products
- `GET /api/products/paged?page={page}&pageSize={size}` - Get paginated products
- `POST /api/products` - Create new product
- `PUT /api/products/{id}` - Update product
- `DELETE /api/products/{id}` - Delete product

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

Feel free to submit issues and enhancement requests!

## Acknowledgments

- [DapperExtensions](https://github.com/tmsmith/Dapper-Extensions) - The ORM library used
- [Dapper](https://github.com/DapperLib/Dapper) - The micro ORM that DapperExtensions extends
- [Npgsql](https://www.npgsql.org/) - PostgreSQL data provider for .NET