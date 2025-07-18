# Daily Comparison Sample

This example demonstrates a simple rate ingestion and daily aggregation using **Kafka.Ksql.Linq** only.
All communication with Kafka and ksqlDB goes through a custom `MyKsqlContext` derived from `KafkaKsqlContext`.

## Usage

1. Start the local Kafka stack:
   ```bash
   docker compose up -d
   ```
2. Run the rate sender which also performs aggregation. It instantiates `MyKsqlContext` directly:
   ```bash
   dotnet run --project RateSender
   ```
   This sends a rate every second (100 messages total) and stores the daily comparison.
3. Display aggregated rows using the same context implementation:
   ```bash
   dotnet run --project ComparisonViewer
   ```

See the repository root README for package installation and local setup details.
