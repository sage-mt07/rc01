| File | Line | Severity | Message |
|------|------|----------|---------|
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 34 | LogInformation | Starting database import: {ConnectionString} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 45 | LogDebug | Executing query: {Query} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 67 | LogInformation | Imported {Count} windows from database |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 79 | LogInformation | Database import completed: {Count} windows imported |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 83 | LogError | Database import failed |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 94 | LogInformation | Starting CSV import: {FilePath} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 109 | LogWarning | CSV file is empty: {FilePath} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 135 | LogInformation | Imported {Count} windows from CSV |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 142 | LogWarning | Failed to parse CSV line {LineNumber}: {Line} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 159 | LogInformation | CSV import completed: {Count} windows imported from {TotalLines} lines |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 164 | LogError | CSV import failed: {FilePath} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 175 | LogInformation | Starting JSON import: {FilePath} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 203 | LogWarning | No valid window data found in JSON file: {FilePath} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 210 | LogInformation | JSON import completed: {Count} windows imported |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 214 | LogError | JSON import failed: {FilePath} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 225 | LogInformation | Starting directory import: {Directory} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 238 | LogWarning | No matching files found in directory: {Directory} with pattern: {Pattern} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 251 | LogInformation | Processing file {Current}/{Total}: {FileName} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 270 | LogWarning | Unsupported file format: {FilePath} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 275 | LogInformation | File processed successfully: {FileName} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 280 | LogError | Failed to process file: {FilePath} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 289 | LogInformation | Directory import completed: {Success} success, {Failed} failed, {Total} total files |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 364 | LogWarning | Failed to map database row to window |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 393 | LogInformation | Sent batch {Current}/{Total} ({Count} windows) |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 453 | LogWarning | Invalid timestamp format at line {Line}: {Timestamp} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 483 | LogWarning | Failed to map CSV row to window at line {Line} |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 524 | LogInformation | WindowDataImporter disposed |
| physicalTests/TestEnvironment.cs | 130 | LogError | Failed to drop objects |
| physicalTests/TestEnvironment.cs | 168 | LogWarning | Failed to delete schema {Subject}: {StatusCode} |
| physicalTests/TestEnvironment.cs | 174 | LogError | Failed to delete schema {Subject} |
| physicalTests/TestEnvironment.cs | 205 | LogError | Service check failed |
| physicalTests/TestEnvironment.cs | 228 | LogError | Failed to create DLQ topic: {Reason} |
| src/Cache/Core/ReadCachedEntitySet.cs | 33 | LogWarning | Table cache not available for {Entity} |
| src/Cache/Core/RocksDbTableCache.cs | 27 | LogInformation | Table cache for {Type} is RUNNING |
| src/Cache/Core/RocksDbTableCache.cs | 46 | LogInformation | Table cache for {Type} disposed |
| src/Cache/Core/TableCacheRegistry.cs | 33 | LogInformation | Initialized cache for {Entity} |
| src/Core/Extensions/LoggerFactoryExtensions.cs | 62 | LogDebug |  |
| src/Core/Extensions/LoggerFactoryExtensions.cs | 86 | LogDebug |  |
| src/Core/Extensions/LoggerFactoryExtensions.cs | 110 | LogInformation |  |
| src/Core/Extensions/LoggerFactoryExtensions.cs | 133 | LogWarning |  |
| src/Core/Extensions/LoggerFactoryExtensions.cs | 157 | LogError |  |
| src/Core/Models/KeyExtractor.cs | 86 | LogError | Failed to convert key value '{Value}' to {Type} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 30 | LogDebug | KafkaAdminService initialized with BootstrapServers: {BootstrapServers} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 47 | LogDebug | DLQ topic already exists: {DlqTopicName} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 53 | LogInformation | DLQ topic created successfully: {DlqTopicName} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 57 | LogError | Failed to ensure DLQ topic exists: {DlqTopicName} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 68 | LogDebug | Topic already exists: {TopicName} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 86 | LogInformation | Topic created: {TopicName} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 93 | LogDebug | Topic already exists (race): {TopicName} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 130 | LogWarning | Failed to check topic existence: {TopicName} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 149 | LogDebug | DB topic already exists: {Topic} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 163 | LogInformation | DB topic created: {Topic} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 170 | LogDebug | DB topic already exists (race): {Topic} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 189 | LogInformation | DLQ auto-creation disabled. Skipping topic creation: {TopicName} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 218 | LogInformation | DLQ topic created: {TopicName} with {RetentionMs}ms retention, {Partitions} partitions |
| src/Infrastructure/Admin/KafkaAdminService.cs | 227 | LogDebug | DLQ topic already exists (race condition): {TopicName} |
| src/Infrastructure/Admin/KafkaAdminService.cs | 249 | LogDebug | Kafka connectivity validated: {BrokerCount} brokers available |
| src/Infrastructure/Admin/KafkaAdminService.cs | 312 | LogDebug | KafkaAdminService disposed |
| src/Infrastructure/Admin/KafkaAdminService.cs | 316 | LogWarning | Error disposing KafkaAdminService |
| src/KsqlContext.cs | 301 | LogInformation | ✅ Kafka initialization completed. DLQ topic '{Topic}' is ready with 5-second retention. |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 80 | LogWarning | Error consuming message from topic {TopicName} |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 145 | LogError | Failed to consume batch: {EntityType} -> {Topic} |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 156 | LogTrace | Offset committed: {EntityType} -> {Topic} |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 160 | LogError | Failed to commit offset: {EntityType} -> {Topic} |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 174 | LogInformation | Seeked to offset: {EntityType} -> {TopicPartitionOffset} |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 178 | LogError | Failed to seek to offset: {EntityType} -> {TopicPartitionOffset} |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 192 | LogWarning | Failed to get assigned partitions: {EntityType} |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 205 | LogDebug | Subscribed to topic: {EntityType} -> {Topic} |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 209 | LogError | Failed to subscribe to topic: {EntityType} -> {Topic} |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 255 | LogWarning | Failed to deserialize key for topic {TopicName}, using default key |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 269 | LogDebug | Key/Value merge completed: {EntityType}, HasKeys: {HasKeys}, KeyType: {KeyType} |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 275 | LogWarning | Failed to merge key/value for topic {TopicName}, using value-only entity |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 323 | LogWarning | Deserialization failed for topic {Topic} |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 345 | LogError | Failed to send deserialization error to DLQ |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 364 | LogWarning | Failed to extract correlation ID from headers |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 386 | LogWarning | Error disposing consumer: {EntityType} |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 54 | LogInformation | Type-safe KafkaConsumerManager initialized |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 104 | LogDebug | Consumer created: {EntityType} -> {TopicName} |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 109 | LogError | Failed to create consumer: {EntityType} |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 192 | LogError | Message handler failed: {EntityType} |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 198 | LogInformation | Subscription cancelled: {EntityType} |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 202 | LogError | Subscription error: {EntityType} |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 241 | LogDebug | Created SchemaRegistryClient with URL: {Url} |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 278 | LogDebug | Generated key schema: {Schema} |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 289 | LogDebug | Generated value schema: {Schema} |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 370 | LogInformation | Disposing type-safe KafkaConsumerManager... |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 392 | LogInformation | Type-safe KafkaConsumerManager disposed |
| src/Messaging/Internal/ErrorHandlingContext.cs | 69 | LogError | CUSTOM_HANDLER_ERROR |
| src/Messaging/Internal/ErrorHandlingContext.cs | 78 | LogWarning | SKIP |
| src/Messaging/Internal/ErrorHandlingContext.cs | 118 | LogError | UNKNOWN ERROR ACTION: {Action}, skipping item |
| src/Messaging/Producers/Core/KafkaProducer.cs | 71 | LogDebug | Message sent: {EntityType} -> {Topic}, Partition: {Partition}, Offset: {Offset} |
| src/Messaging/Producers/Core/KafkaProducer.cs | 86 | LogError | Failed to send message: {EntityType} -> {Topic} |
| src/Messaging/Producers/Core/KafkaProducer.cs | 116 | LogDebug | Tombstone sent: {EntityType} -> {Topic}, Partition: {Partition}, Offset: {Offset} |
| src/Messaging/Producers/Core/KafkaProducer.cs | 131 | LogError | Failed to send tombstone: {EntityType} -> {Topic} |
| src/Messaging/Producers/Core/KafkaProducer.cs | 147 | LogTrace | Producer flushed: {EntityType} -> {Topic} |
| src/Messaging/Producers/Core/KafkaProducer.cs | 151 | LogWarning | Failed to flush producer: {EntityType} -> {Topic} |
| src/Messaging/Producers/Core/KafkaProducer.cs | 189 | LogWarning | Error disposing producer: {EntityType} |
| src/Messaging/Producers/KafkaProducerManager.cs | 51 | LogInformation | Type-safe KafkaProducerManager initialized |
| src/Messaging/Producers/KafkaProducerManager.cs | 96 | LogDebug | Producer created: {EntityType} -> {TopicName} |
| src/Messaging/Producers/KafkaProducerManager.cs | 101 | LogError | Failed to create producer: {EntityType} |
| src/Messaging/Producers/KafkaProducerManager.cs | 261 | LogDebug | Created SchemaRegistryClient with URL: {Url} |
| src/Messaging/Producers/KafkaProducerManager.cs | 296 | LogDebug | Generated key schema: {Schema} |
| src/Messaging/Producers/KafkaProducerManager.cs | 307 | LogDebug | Generated value schema: {Schema} |
| src/Messaging/Producers/KafkaProducerManager.cs | 390 | LogInformation | Disposing type-safe KafkaProducerManager... |
| src/Messaging/Producers/KafkaProducerManager.cs | 423 | LogInformation | Type-safe KafkaProducerManager disposed |
| src/Window/Finalization/WindowFinalConsumer.cs | 34 | LogInformation | Starting subscription to finalized windows: {Topic}({Window}) → RocksDB |
| src/Window/Finalization/WindowFinalConsumer.cs | 57 | LogDebug | Processing new finalized window: {WindowKey} from POD: {PodId} |
| src/Window/Finalization/WindowFinalConsumer.cs | 70 | LogDebug | Duplicate finalized window ignored: {WindowKey}.  |
| src/Window/Finalization/WindowFinalConsumer.cs | 149 | LogInformation | WindowFinalConsumer disposed with RocksDB persistence |
| src/Window/Finalization/WindowFinalizationManager.cs | 30 | LogInformation | WindowFinalizationManager initialized with interval: {Interval}ms |
| src/Window/Finalization/WindowFinalizationManager.cs | 43 | LogDebug | Window processor already registered: {Key} |
| src/Window/Finalization/WindowFinalizationManager.cs | 50 | LogInformation | Registered window processor: {EntityType} -> {Windows}min |
| src/Window/Finalization/WindowFinalizationManager.cs | 62 | LogTrace | Processing window finalization at {Timestamp} |
| src/Window/Finalization/WindowFinalizationManager.cs | 89 | LogInformation | WindowFinalizationManager disposed |
| src/Window/Finalization/WindowProcessor.cs | 58 | LogTrace | Added event to window: {WindowKey}, Events: {Count} |
| src/Window/Finalization/WindowProcessor.cs | 127 | LogInformation | Finalized window: {WindowKey}, Events: {EventCount},  |
| src/Window/Finalization/WindowProcessor.cs | 134 | LogError | Failed to finalize window: {WindowKey} |
| src/Window/Finalization/WindowProcessor.cs | 170 | LogDebug | Sent finalized window to topic: {Topic}, Key: {Key} |
| src/Window/Finalization/WindowProcessor.cs | 198 | LogDebug | Cleaned up {Count} old windows for entity {EntityType} |
