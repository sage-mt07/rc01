﻿using Kafka.Ksql.Linq.Serialization.Abstractions;
using Kafka.Ksql.Linq.Serialization.Avro.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Serialization.Avro.Management;
internal interface IAvroSchemaRegistrationService
{
    /// <summary>
    /// 全スキーマ一括登録（初期化パス）
    /// </summary>
    Task RegisterAllSchemasAsync(IReadOnlyDictionary<Type, AvroEntityConfiguration> configurations);

    /// <summary>
    /// 型指定スキーマ情報取得
    /// </summary>
    Task<AvroSchemaInfo> GetSchemaInfoAsync<T>() where T : class;

    /// <summary>
    /// Type指定スキーマ情報取得
    /// </summary>
    AvroSchemaInfo GetSchemaInfoAsync(Type entityType);

    /// <summary>
    /// 全登録スキーマ取得
    /// </summary>
    Task<List<AvroSchemaInfo>> GetAllRegisteredSchemasAsync();
}
