# high-performance-api-dotnet

Optimising Performance to the max for a .NET API

## Architecture

This project follows a **Vertical Slice Architecture** with the primary objective of achieving **highest performance**.

## Infrastructure

### Database Selection: PostgreSQL vs SQL Server

**PostgreSQL** is the chosen database for this project for the following reasons:

- **Superior Performance**: PostgreSQL consistently outperforms SQL Server in high-throughput scenarios, especially for read-heavy workloads
- **Better Connection Pooling**: Native connection pooling with PgBouncer provides exceptional scalability
- **Efficient Indexes**: Advanced indexing capabilities (BRIN, GiST, GIN) and partial indexes offer better query optimization
- **Lower Latency**: Lightweight protocol and efficient buffer management result in lower query latency
- **Cost-Effective**: Open-source with no licensing costs, allowing infrastructure budget to focus on hardware optimization
- **JSON Performance**: Native JSONB support with indexing outperforms SQL Server's JSON handling
- **Concurrent Connections**: MVCC (Multi-Version Concurrency Control) handles concurrent writes more efficiently than SQL Server's locking mechanisms

### Database Schema

The application uses a production-realistic e-commerce database schema with the following entities:

#### Seed Data Statistics

| Entity | Row Count | Description |
|--------|-----------|-------------|
| **Categories** | 15 | Product categories (Electronics, Clothing, Books, etc.) |
| **Users** | 3,000 | Customer accounts with realistic contact details |
| **Products** | 15,000 | Products with realistic names/descriptions via Bogus |
| **Orders** | 5,000 | Customer orders with various statuses |
| **OrderItems** | ~12,500 | Order line items (1-5 items per order, avg 2.5) |
| **Reviews** | 12,000 | Product reviews (~80% products have reviews) |

**Total Rows:** ~47,500+
**Estimated Database Size:** 60-120 MB

#### Seeding Strategy

**Bogus-based seeding** for realistic data:
- **Smart seeding**: Automatically skips if data exists
- **Batched inserts**: 500-1000 records per batch for performance
- **ChangeTracker clearing**: Prevents memory issues with large datasets
- **Realistic data**: Uses Faker library for names, emails, addresses, product descriptions
- **Optimized queries**: Loads reference data upfront to avoid N+1 queries

#### Entity Relationships

- **User → Orders** (1:N): Each user can place multiple orders
- **User → Reviews** (1:N): Each user can write multiple reviews
- **Category → Products** (1:N): Each category contains multiple products
- **Product → OrderItems** (1:N): Each product can appear in multiple orders
- **Product → Reviews** (1:N): Each product can have multiple reviews
- **Order → OrderItems** (1:N): Each order contains multiple line items
- **OrderItem → Product** (N:1): Each order item references one product

#### Performance Optimizations

- **Indexes**: Strategic indexes on foreign keys, frequently queried columns (Price, Name, OrderDate, Status, Rating)
- **Precision**: Decimal fields use `PRECISION(18,2)` for monetary values
- **Constraints**: Appropriate delete behaviors (Cascade for dependent data, Restrict for referenced data)
- **Default Values**: Database-level defaults for timestamps using `CURRENT_TIMESTAMP`

## Database Setup with Docker

### Prerequisites

- Docker Desktop installed and running
- .NET 9 SDK

### Step 1: Create docker-compose.yml

Create a `docker-compose.yml` file in the project root with PostgreSQL configuration.

### Step 2: Configure Connection Strings

Add database connection settings to `appsettings.json` and `appsettings.Development.json`.

### Step 3: Configure DbContext

Set up EF Core with PostgreSQL provider in the Infrastructure layer, registering the DbContext in dependency injection.

### Step 4: Create Initial Migration

Generate EF Core migration for the domain entities:

```bash
dotnet ef migrations add InitialCreate --project ApexShop.Infrastructure --startup-project ApexShop.API
```

### Step 5: Start Database and Apply Migrations

```bash
# Start PostgreSQL container
docker-compose up -d

# Apply migrations
dotnet ef database update --project ApexShop.Infrastructure --startup-project ApexShop.API
```

### Step 6: Verify Connection

Run the API and verify database connectivity:

```bash
dotnet run --project ApexShop.API
```
