using Kafka.Ksql.Linq.Core.Abstractions;
using System.Linq.Expressions;

namespace Kafka.Ksql.Linq.Query.Pipeline;

internal interface IDDLQueryGenerator
{
    string GenerateCreateStream(string streamName, string topicName, EntityModel entityModel);
    string GenerateCreateTable(string tableName, string topicName, EntityModel entityModel);
    string GenerateCreateStreamAs(string streamName, string baseObject, Expression linqExpression);
    string GenerateCreateTableAs(string tableName, string baseObject, Expression linqExpression);
}