# Daily Comparison Sample

This example demonstrates a simple rate ingestion and daily aggregation using EF Core and SQL Server.

## Usage

1. Build and start the containers:
   ```bash
   docker compose up --build
   ```
2. Execute `RateSender` multiple times to store rates and generate daily aggregates.
3. Run the `viewer` service to print aggregated rows:
   ```bash
   docker compose run viewer
   ```

The SQL Server database persists data in the container.
