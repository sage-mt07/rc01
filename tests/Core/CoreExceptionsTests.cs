using Kafka.Ksql.Linq.Core.Exceptions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Kafka.Ksql.Linq.Tests.Core;

public class CoreExceptionsTests
{
    private class TestException : CoreException
    {
        public TestException(string m) : base(m) { }
        public TestException(string m, Exception i) : base(m, i) { }
    }

    [Fact]
    public void CoreConfigurationException_Constructors()
    {
        var ex1 = new CoreConfigurationException("m1");
        Assert.Equal("m1", ex1.Message);
        var inner = new Exception("i");
        var ex2 = new CoreConfigurationException("m2", inner);
        Assert.Equal(inner, ex2.InnerException);
    }

    [Fact]
    public void CoreException_BaseConstructor_SetsMessage()
    {
        var ex = new TestException("msg");
        Assert.Equal("msg", ex.Message);
    }

    [Fact]
    public void CoreValidationException_StoresErrors()
    {
        var errs = new List<string> { "a", "b" };
        var ex = new CoreValidationException("m", errs);
        Assert.Equal(errs, ex.ValidationErrors);
        var ex2 = new CoreValidationException(errs);
        Assert.Equal(2, ex2.ValidationErrors.Count);
        Assert.Contains("Validation failed", ex2.Message);
    }

    [Fact]
    public void EntityModelException_Constructors()
    {
        var ex1 = new EntityModelException(typeof(string), "bad");
        Assert.Equal(typeof(string), ex1.EntityType);
        Assert.Contains("String", ex1.Message);
        var inner = new Exception("i");
        var ex2 = new EntityModelException(typeof(int), "x", inner);
        Assert.Equal(inner, ex2.InnerException);
        Assert.Equal(typeof(int), ex2.EntityType);
    }
}
