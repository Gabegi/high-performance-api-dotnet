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
