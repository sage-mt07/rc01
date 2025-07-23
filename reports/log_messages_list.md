| File | Line | Severity | Message | Category | LogID |
|------|------|----------|---------|----------|-------|
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 34 | LogInformation | Starting database import: {ConnectionString} | Importer | IMP-001 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 45 | LogDebug | Executing query: {Query} | Importer | IMP-002 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 67 | LogInformation | Imported {Count} windows from database | Importer | IMP-003 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 79 | LogInformation | Database import completed: {Count} windows imported | Importer | IMP-004 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 83 | LogError | Database import failed | Importer | IMP-005 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 94 | LogInformation | Starting CSV import: {FilePath} | Importer | IMP-006 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 109 | LogWarning | CSV file is empty: {FilePath} | Importer | IMP-007 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 135 | LogInformation | Imported {Count} windows from CSV | Importer | IMP-008 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 142 | LogWarning | Failed to parse CSV line {LineNumber}: {Line} | Importer | IMP-009 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 159 | LogInformation | CSV import completed: {Count} windows imported from {TotalLines} lines | Importer | IMP-010 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 164 | LogError | CSV import failed: {FilePath} | Importer | IMP-011 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 175 | LogInformation | Starting JSON import: {FilePath} | Importer | IMP-012 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 203 | LogWarning | No valid window data found in JSON file: {FilePath} | Importer | IMP-013 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 210 | LogInformation | JSON import completed: {Count} windows imported | Importer | IMP-014 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 214 | LogError | JSON import failed: {FilePath} | Importer | IMP-015 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 225 | LogInformation | Starting directory import: {Directory} | Importer | IMP-016 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 238 | LogWarning | No matching files found in directory: {Directory} with pattern: {Pattern} | Importer | IMP-017 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 251 | LogInformation | Processing file {Current}/{Total}: {FileName} | Importer | IMP-018 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 270 | LogWarning | Unsupported file format: {FilePath} | Importer | IMP-019 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 275 | LogInformation | File processed successfully: {FileName} | Importer | IMP-020 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 280 | LogError | Failed to process file: {FilePath} | Importer | IMP-021 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 289 | LogInformation | Directory import completed: {Success} success, {Failed} failed, {Total} total files | Importer | IMP-022 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 364 | LogWarning | Failed to map database row to window | Importer | IMP-023 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 393 | LogInformation | Sent batch {Current}/{Total} ({Count} windows) | Importer | IMP-024 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 453 | LogWarning | Invalid timestamp format at line {Line}: {Timestamp} | Importer | IMP-025 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 483 | LogWarning | Failed to map CSV row to window at line {Line} | Importer | IMP-026 |
| Kafka.Ksql.Linq.Importer/WindowDataImporter.cs | 524 | LogInformation | WindowDataImporter disposed | Importer | IMP-027 |
| physicalTests/TestEnvironment.cs | 130 | LogError | Failed to drop objects | Tests | TES-001 |
| physicalTests/TestEnvironment.cs | 168 | LogWarning | Failed to delete schema {Subject}: {StatusCode} | Tests | TES-002 |
| physicalTests/TestEnvironment.cs | 174 | LogError | Failed to delete schema {Subject} | Tests | TES-003 |
| physicalTests/TestEnvironment.cs | 205 | LogError | Service check failed | Tests | TES-004 |
| physicalTests/TestEnvironment.cs | 228 | LogError | Failed to create DLQ topic: {Reason} | Tests | TES-005 |
| src/Cache/Core/ReadCachedEntitySet.cs | 33 | LogWarning | Table cache not available for {Entity} | Cache | CAC-001 |
| src/Cache/Core/RocksDbTableCache.cs | 27 | LogInformation | Table cache for {Type} is RUNNING | Cache | CAC-002 |
| src/Cache/Core/RocksDbTableCache.cs | 46 | LogInformation | Table cache for {Type} disposed | Cache | CAC-003 |
| src/Cache/Core/TableCacheRegistry.cs | 33 | LogInformation | Initialized cache for {Entity} | Cache | CAC-004 |
| src/Core/Extensions/LoggerFactoryExtensions.cs | 62 | LogDebug |  | Core | COR-001 |
| src/Core/Extensions/LoggerFactoryExtensions.cs | 86 | LogDebug |  | Core | COR-002 |
| src/Core/Extensions/LoggerFactoryExtensions.cs | 110 | LogInformation |  | Core | COR-003 |
| src/Core/Extensions/LoggerFactoryExtensions.cs | 133 | LogWarning |  | Core | COR-004 |
| src/Core/Extensions/LoggerFactoryExtensions.cs | 157 | LogError |  | Core | COR-005 |
| src/Core/Models/KeyExtractor.cs | 86 | LogError | Failed to convert key value '{Value}' to {Type} | Core | COR-006 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 30 | LogDebug | KafkaAdminService initialized with BootstrapServers: {BootstrapServers} | Infrastructure | INF-001 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 47 | LogDebug | DLQ topic already exists: {DlqTopicName} | Infrastructure | INF-002 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 53 | LogInformation | DLQ topic created successfully: {DlqTopicName} | Infrastructure | INF-003 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 57 | LogError | Failed to ensure DLQ topic exists: {DlqTopicName} | Infrastructure | INF-004 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 68 | LogDebug | Topic already exists: {TopicName} | Infrastructure | INF-005 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 86 | LogInformation | Topic created: {TopicName} | Infrastructure | INF-006 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 93 | LogDebug | Topic already exists (race): {TopicName} | Infrastructure | INF-007 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 130 | LogWarning | Failed to check topic existence: {TopicName} | Infrastructure | INF-008 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 149 | LogDebug | DB topic already exists: {Topic} | Infrastructure | INF-009 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 163 | LogInformation | DB topic created: {Topic} | Infrastructure | INF-010 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 170 | LogDebug | DB topic already exists (race): {Topic} | Infrastructure | INF-011 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 189 | LogInformation | DLQ auto-creation disabled. Skipping topic creation: {TopicName} | Infrastructure | INF-012 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 218 | LogInformation | DLQ topic created: {TopicName} with {RetentionMs}ms retention, {Partitions} partitions | Infrastructure | INF-013 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 227 | LogDebug | DLQ topic already exists (race condition): {TopicName} | Infrastructure | INF-014 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 249 | LogDebug | Kafka connectivity validated: {BrokerCount} brokers available | Infrastructure | INF-015 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 312 | LogDebug | KafkaAdminService disposed | Infrastructure | INF-016 |
| src/Infrastructure/Admin/KafkaAdminService.cs | 316 | LogWarning | Error disposing KafkaAdminService | Infrastructure | INF-017 |
| src/KsqlContext.cs | 301 | LogInformation | ✅ Kafka initialization completed. DLQ topic '{Topic}' is ready with 5-second retention. | KsqlContext | KSQ-001 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 80 | LogWarning | Error consuming message from topic {TopicName} | Messaging | MES-001 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 145 | LogError | Failed to consume batch: {EntityType} -> {Topic} | Messaging | MES-002 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 156 | LogTrace | Offset committed: {EntityType} -> {Topic} | Messaging | MES-003 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 160 | LogError | Failed to commit offset: {EntityType} -> {Topic} | Messaging | MES-004 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 174 | LogInformation | Seeked to offset: {EntityType} -> {TopicPartitionOffset} | Messaging | MES-005 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 178 | LogError | Failed to seek to offset: {EntityType} -> {TopicPartitionOffset} | Messaging | MES-006 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 192 | LogWarning | Failed to get assigned partitions: {EntityType} | Messaging | MES-007 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 205 | LogDebug | Subscribed to topic: {EntityType} -> {Topic} | Messaging | MES-008 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 209 | LogError | Failed to subscribe to topic: {EntityType} -> {Topic} | Messaging | MES-009 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 255 | LogWarning | Failed to deserialize key for topic {TopicName}, using default key | Messaging | MES-010 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 269 | LogDebug | Key/Value merge completed: {EntityType}, HasKeys: {HasKeys}, KeyType: {KeyType} | Messaging | MES-011 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 275 | LogWarning | Failed to merge key/value for topic {TopicName}, using value-only entity | Messaging | MES-012 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 323 | LogWarning | Deserialization failed for topic {Topic} | Messaging | MES-013 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 345 | LogError | Failed to send deserialization error to DLQ | Messaging | MES-014 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 364 | LogWarning | Failed to extract correlation ID from headers | Messaging | MES-015 |
| src/Messaging/Consumers/Core/KafkaConsumer.cs | 386 | LogWarning | Error disposing consumer: {EntityType} | Messaging | MES-016 |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 54 | LogInformation | Type-safe KafkaConsumerManager initialized | Messaging | MES-017 |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 104 | LogDebug | Consumer created: {EntityType} -> {TopicName} | Messaging | MES-018 |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 109 | LogError | Failed to create consumer: {EntityType} | Messaging | MES-019 |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 192 | LogError | Message handler failed: {EntityType} | Messaging | MES-020 |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 198 | LogInformation | Subscription cancelled: {EntityType} | Messaging | MES-021 |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 202 | LogError | Subscription error: {EntityType} | Messaging | MES-022 |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 241 | LogDebug | Created SchemaRegistryClient with URL: {Url} | Messaging | MES-023 |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 278 | LogDebug | Generated key schema: {Schema} | Messaging | MES-024 |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 289 | LogDebug | Generated value schema: {Schema} | Messaging | MES-025 |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 370 | LogInformation | Disposing type-safe KafkaConsumerManager... | Messaging | MES-026 |
| src/Messaging/Consumers/KafkaConsumerManager.cs | 392 | LogInformation | Type-safe KafkaConsumerManager disposed | Messaging | MES-027 |
| src/Messaging/Internal/ErrorHandlingContext.cs | 69 | LogError | CUSTOM_HANDLER_ERROR | Messaging | MES-028 |
| src/Messaging/Internal/ErrorHandlingContext.cs | 78 | LogWarning | SKIP | Messaging | MES-029 |
| src/Messaging/Internal/ErrorHandlingContext.cs | 118 | LogError | UNKNOWN ERROR ACTION: {Action}, skipping item | Messaging | MES-030 |
| src/Messaging/Producers/Core/KafkaProducer.cs | 71 | LogDebug | Message sent: {EntityType} -> {Topic}, Partition: {Partition}, Offset: {Offset} | Messaging | MES-031 |
| src/Messaging/Producers/Core/KafkaProducer.cs | 86 | LogError | Failed to send message: {EntityType} -> {Topic} | Messaging | MES-032 |
| src/Messaging/Producers/Core/KafkaProducer.cs | 116 | LogDebug | Tombstone sent: {EntityType} -> {Topic}, Partition: {Partition}, Offset: {Offset} | Messaging | MES-033 |
| src/Messaging/Producers/Core/KafkaProducer.cs | 131 | LogError | Failed to send tombstone: {EntityType} -> {Topic} | Messaging | MES-034 |
| src/Messaging/Producers/Core/KafkaProducer.cs | 147 | LogTrace | Producer flushed: {EntityType} -> {Topic} | Messaging | MES-035 |
| src/Messaging/Producers/Core/KafkaProducer.cs | 151 | LogWarning | Failed to flush producer: {EntityType} -> {Topic} | Messaging | MES-036 |
| src/Messaging/Producers/Core/KafkaProducer.cs | 189 | LogWarning | Error disposing producer: {EntityType} | Messaging | MES-037 |
| src/Messaging/Producers/KafkaProducerManager.cs | 51 | LogInformation | Type-safe KafkaProducerManager initialized | Messaging | MES-038 |
| src/Messaging/Producers/KafkaProducerManager.cs | 96 | LogDebug | Producer created: {EntityType} -> {TopicName} | Messaging | MES-039 |
| src/Messaging/Producers/KafkaProducerManager.cs | 101 | LogError | Failed to create producer: {EntityType} | Messaging | MES-040 |
| src/Messaging/Producers/KafkaProducerManager.cs | 261 | LogDebug | Created SchemaRegistryClient with URL: {Url} | Messaging | MES-041 |
| src/Messaging/Producers/KafkaProducerManager.cs | 296 | LogDebug | Generated key schema: {Schema} | Messaging | MES-042 |
| src/Messaging/Producers/KafkaProducerManager.cs | 307 | LogDebug | Generated value schema: {Schema} | Messaging | MES-043 |
| src/Messaging/Producers/KafkaProducerManager.cs | 390 | LogInformation | Disposing type-safe KafkaProducerManager... | Messaging | MES-044 |
| src/Messaging/Producers/KafkaProducerManager.cs | 423 | LogInformation | Type-safe KafkaProducerManager disposed | Messaging | MES-045 |
| src/Window/Finalization/WindowFinalConsumer.cs | 34 | LogInformation | Starting subscription to finalized windows: {Topic}({Window}) → RocksDB | Window | WIN-001 |
| src/Window/Finalization/WindowFinalConsumer.cs | 57 | LogDebug | Processing new finalized window: {WindowKey} from POD: {PodId} | Window | WIN-002 |
| src/Window/Finalization/WindowFinalConsumer.cs | 70 | LogDebug | Duplicate finalized window ignored: {WindowKey}.  | Window | WIN-003 |
| src/Window/Finalization/WindowFinalConsumer.cs | 149 | LogInformation | WindowFinalConsumer disposed with RocksDB persistence | Window | WIN-004 |
| src/Window/Finalization/WindowFinalizationManager.cs | 30 | LogInformation | WindowFinalizationManager initialized with interval: {Interval}ms | Window | WIN-005 |
| src/Window/Finalization/WindowFinalizationManager.cs | 43 | LogDebug | Window processor already registered: {Key} | Window | WIN-006 |
| src/Window/Finalization/WindowFinalizationManager.cs | 50 | LogInformation | Registered window processor: {EntityType} -> {Windows}min | Window | WIN-007 |
| src/Window/Finalization/WindowFinalizationManager.cs | 62 | LogTrace | Processing window finalization at {Timestamp} | Window | WIN-008 |
| src/Window/Finalization/WindowFinalizationManager.cs | 89 | LogInformation | WindowFinalizationManager disposed | Window | WIN-009 |
| src/Window/Finalization/WindowProcessor.cs | 58 | LogTrace | Added event to window: {WindowKey}, Events: {Count} | Window | WIN-010 |
| src/Window/Finalization/WindowProcessor.cs | 127 | LogInformation | Finalized window: {WindowKey}, Events: {EventCount},  | Window | WIN-011 |
| src/Window/Finalization/WindowProcessor.cs | 134 | LogError | Failed to finalize window: {WindowKey} | Window | WIN-012 |
| src/Window/Finalization/WindowProcessor.cs | 170 | LogDebug | Sent finalized window to topic: {Topic}, Key: {Key} | Window | WIN-013 |
| src/Window/Finalization/WindowProcessor.cs | 198 | LogDebug | Cleaned up {Count} old windows for entity {EntityType} | Window | WIN-014 |
