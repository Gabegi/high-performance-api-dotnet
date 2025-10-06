# ApexShop Load Tests

Load and performance testing for the ApexShop API using NBomber.

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
