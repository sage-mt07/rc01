using System;
using Kafka.Ksql.Linq.Query.Pipeline;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Query.Pipeline;

public class GeneratorBaseTests
{
    [Fact]
    public void AssembleQuery_OrdersPartsAndTrims()
    {
        var select = QueryPart.Required("SELECT *", 10);
        var from = QueryPart.Required("FROM t", 20);
        var where = QueryPart.Required("WHERE Id = 1", 40);

        var result = PrivateAccessor.InvokePrivate<string>(
            typeof(GeneratorBase),
            "AssembleQuery",
            new[] { typeof(QueryPart[]) },
            args: new object[] { new[] { where, select, from } });

        Assert.Equal("SELECT * FROM t WHERE Id = 1", result);
    }

    [Fact]
    public void AssembleQuery_IgnoresEmptyOptionalParts()
    {
        var select = QueryPart.Required("SELECT *", 10);
        var emptyOpt = QueryPart.Optional(string.Empty, 30);
        var from = QueryPart.Required("FROM t", 20);

        var result = PrivateAccessor.InvokePrivate<string>(
            typeof(GeneratorBase),
            "AssembleQuery",
            new[] { typeof(QueryPart[]) },
            args: new object[] { new[] { select, emptyOpt, from } });

        Assert.Equal("SELECT * FROM t", result);
    }

    [Fact]
    public void AssembleQuery_NoValidParts_Throws()
    {
        Assert.ThrowsAny<Exception>(() =>
            PrivateAccessor.InvokePrivate<string>(
                typeof(GeneratorBase),
                "AssembleQuery",
                new[] { typeof(QueryPart[]) },
                args: new object[] { new[] { QueryPart.Optional(string.Empty) } }));
    }

    [Theory]
    [InlineData(typeof(string), "VARCHAR")]
    [InlineData(typeof(int), "INTEGER")]
    [InlineData(typeof(long), "BIGINT")]
    [InlineData(typeof(double), "DOUBLE")]
    [InlineData(typeof(bool), "BOOLEAN")]
    [InlineData(typeof(DateTime), "TIMESTAMP")]
    [InlineData(typeof(decimal), "DECIMAL(38, 9)")]
    [InlineData(typeof(byte[]), "BYTES")]
    public void MapToKSqlType_ReturnsExpected(Type type, string expected)
    {
        var result = PrivateAccessor.InvokePrivate<string>(
            typeof(GeneratorBase),
            "MapToKSqlType",
            new[] { typeof(Type) },
            args: new object[] { type });

        Assert.Equal(expected, result);
    }

    [Fact]
    public void MapToKSqlType_Unknown_ThrowsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            PrivateAccessor.InvokePrivate<string>(
                typeof(GeneratorBase),
                "MapToKSqlType",
                new[] { typeof(Type) },
                args: new object[] { typeof(Uri) }));
    }
}
