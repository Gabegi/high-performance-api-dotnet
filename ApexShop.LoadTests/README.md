# ApexShop Load Tests

Load and performance testing for the ApexShop API using NBomber.

## Prerequisites

1. **Docker Desktop** must be running
2. **PostgreSQL database** must be running:
   ```bash
   # From project root
   docker-compose up -d
   ```
3. **Database must be seeded** with test data:
   - The API automatically seeds data on first run
   - Requires `secrets/db_password.txt` file in project root
   - Run API at least once: `dotnet run --project ApexShop.API`
4. **API must be running** during load tests:
   ```bash
   cd ApexShop.API
   dotnet run
   ```
   Default URL: `http://localhost:5193`

## Overview

### CRUD Scenarios
- GetProducts (10 RPS for 30s)
- GetProductById (10 RPS for 30s)
- CreateProduct (5 RPS for 30s)
- GetCategories (10 RPS for 30s)
- GetOrders (10 RPS for 30s)

### Realistic Scenarios
- BrowseAndAddReview (5 RPS for 60s)
- CreateOrderWorkflow (3 RPS for 60s)
- UserRegistrationAndBrowse (2 RPS for 60s)

### Stress Scenarios
- HighLoadGetProducts (ramps to 50 RPS)
- SpikeTest (spikes to 100 RPS)
- ConstantLoad (10 concurrent users)
- MixedOperationsStress (mixed operations, ramps to 30 RPS)

## Running

1. Start API: `dotnet run` (from ApexShop.API)
2. Run tests: `dotnet run --configuration Release`
3. Press `1` to start

Reports generated in `Reports/` folder.

## Configuration

Base URL: `http://localhost:5193` (configured in each scenario file)
