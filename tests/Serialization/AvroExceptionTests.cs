using Kafka.Ksql.Linq.Serialization.Avro.Exceptions;
using System;
using System.Collections.Generic;
using Xunit;
using static Kafka.Ksql.Linq.Tests.PrivateAccessor;

namespace Kafka.Ksql.Linq.Tests.Serialization;

public class AvroExceptionTests
{
    [Fact]
    public void AvroSchemaRegistrationException_Ctor_SetsMessage()
    {
        var ex = new AvroSchemaRegistrationException("msg");
        Assert.Equal("msg", ex.Message);
    }

    [Fact]
    public void AvroSchemaRegistrationException_Ctor_WithInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new AvroSchemaRegistrationException("msg", inner);
        Assert.Equal(inner, ex.InnerException);
    }

    [Fact]
    public void SchemaRegistrationFatalException_PropertiesAndSummary()
    {
        var inner = new Exception("boom");
        var ex = new SchemaRegistrationFatalException(
            "subject",
            3,
            SchemaRegistrationFailureCategory.NetworkFailure,
            "failed",
            inner);

        Assert.Equal("subject", ex.Subject);
        Assert.Equal(3, ex.AttemptCount);
        Assert.Equal(SchemaRegistrationFailureCategory.NetworkFailure, ex.FailureCategory);
        Assert.Contains("Check Schema Registry", ex.OperationalAction);
        Assert.True((DateTime.UtcNow - ex.FailedAt).TotalSeconds < 5);

        var summary = ex.GetOperationalSummary();
        Assert.Contains("subject", summary);
        Assert.Contains("Attempts: 3", summary);
        Assert.Contains("NetworkFailure", summary);
        Assert.Equal(summary, ex.ToString());
    }

    [Fact]
    public void DetermineOperationalAction_ReturnsExpected()
    {
        var act = InvokePrivate<string>(typeof(SchemaRegistrationFatalException),
            "DetermineOperationalAction",
            new[] { typeof(SchemaRegistrationFailureCategory) },
            null,
            SchemaRegistrationFailureCategory.AuthenticationFailure);
        Assert.Contains("Verify Schema Registry credentials", act);

        var def = InvokePrivate<string>(typeof(SchemaRegistrationFatalException),
            "DetermineOperationalAction",
            new[] { typeof(SchemaRegistrationFailureCategory) },
            null,
            SchemaRegistrationFailureCategory.Unknown);
        Assert.Contains("Check application logs", def);
    }

    [Fact]
    public void FormatFatalMessage_IncludesDetails()
    {
        var msg = InvokePrivate<string>(typeof(SchemaRegistrationFatalException),
            "FormatFatalMessage",
            new[] { typeof(string), typeof(int), typeof(SchemaRegistrationFailureCategory), typeof(string) },
            null,
            "s", 2, SchemaRegistrationFailureCategory.ResourceExhausted, "oops");
        Assert.Contains("s", msg);
        Assert.Contains("after 2 attempts", msg);
        Assert.Contains("ResourceExhausted", msg);
        Assert.Contains("HUMAN INTERVENTION REQUIRED", msg);
    }

    [Fact]
    public void DetermineOperationalAction_AllCategories_ReturnsAction()
    {
        var expectations = new Dictionary<SchemaRegistrationFailureCategory, string>
        {
            [SchemaRegistrationFailureCategory.NetworkFailure] = "connectivity",
            [SchemaRegistrationFailureCategory.AuthenticationFailure] = "credentials",
            [SchemaRegistrationFailureCategory.SchemaIncompatible] = "schema compatibility",
            [SchemaRegistrationFailureCategory.RegistryUnavailable] = "Registry service status",
            [SchemaRegistrationFailureCategory.ConfigurationError] = "application configuration",
            [SchemaRegistrationFailureCategory.ResourceExhausted] = "disk space",
            [SchemaRegistrationFailureCategory.Unknown] = "application logs"
        };

        foreach (var kvp in expectations)
        {
            var action = InvokePrivate<string>(typeof(SchemaRegistrationFatalException),
                "DetermineOperationalAction",
                new[] { typeof(SchemaRegistrationFailureCategory) },
                null,
                kvp.Key);
            Assert.Contains(kvp.Value, action);
        }
    }
}
