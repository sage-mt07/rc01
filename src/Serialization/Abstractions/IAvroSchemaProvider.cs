using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Serialization.Abstractions;

public interface IAvroSchemaProvider
{
    Task<string> GetKeySchemaAsync<T>() where T : class;
    Task<string> GetValueSchemaAsync<T>() where T : class;
    Task<(string keySchema, string valueSchema)> GetSchemasAsync<T>() where T : class;
    Task<bool> ValidateSchemaAsync(string schema);
}
