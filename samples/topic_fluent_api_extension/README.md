# Topic Fluent API Extension Samples

This directory contains runnable examples showing how to configure Kafka topics
using the fluent API from `Kafka.Ksql.Linq`. Four sample levels are provided:

- **Beginner** – specify only a topic name and partition count (`Example1_Basic`).
- **Intermediate** – add replication and the custom `IsManaged` flag (`Example2_ManagedMode`).
- **Advanced** – demonstrate custom retention and cleanup policy extensions (`Example3_AdvancedOptions`).
- **Decimal Precision** – replace the old `DecimalPrecision` attribute with `WithDecimalPrecision` (`Example4_DecimalPrecision`).

## Running the Samples

1. Ensure the root project has been restored.
2. From this directory run:

```bash
dotnet test
```

This builds the test project and executes a simple unit test verifying the
`IsManaged` extension. During the test `Example2_ManagedMode.Run()` is executed
which prints the configured managed flag to the console.
