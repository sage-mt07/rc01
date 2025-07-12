using Kafka.Ksql.Linq.Application;
using Kafka.Ksql.Linq.Configuration;
using Kafka.Ksql.Linq.Core.Abstractions;
using Kafka.Ksql.Linq.StateStore.Monitoring;
using Kafka.Ksql.Linq.StateStore;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
#nullable enable

namespace Kafka.Ksql.Linq.Tests.Application;

public class KsqlContextBindingEventTests
{
    private class BindingContext : KsqlContext
    {
        public BindingContext() : base() { }

        protected override bool SkipSchemaRegistration => true;

        protected override void OnModelCreating(IModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Sample>();
        }
    }

    private class Sample
    {
        public int Id { get; set; }
    }

    [Fact]
    public void ReadyStateChanged_IsForwarded()
    {
        var ctx = new BindingContext();

        var optionsField = typeof(KsqlContext).GetField("_dslOptions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var options = (KsqlDslOptions)optionsField.GetValue(ctx)!;
        options.Entities.Add(new EntityConfiguration { Entity = nameof(Sample), SourceTopic = "s", StoreType = StoreTypes.RocksDb });

        var initMethod = typeof(KsqlContext).GetMethod("InitializeStateStoreIntegration", BindingFlags.NonPublic | BindingFlags.Instance)!;
        initMethod.Invoke(ctx, null);

        var bindingsField = typeof(KsqlContext).GetField("_stateBindings", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var bindings = (List<IDisposable>)bindingsField.GetValue(ctx)!;
        Assert.Single(bindings);
        var binding = bindings[0];

        bool invoked = false;
        ctx.BindingReadyStateChanged += (s, e) => invoked = true;

        var onMethod = binding.GetType().GetMethod("OnReadyStateChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var args = new ReadyStateChangedEventArgs { TopicName = "s", IsReady = false, CurrentLag = 1, TimeToReady = TimeSpan.Zero };
        onMethod.Invoke(binding, new object?[] { null, args });

        Assert.True(invoked);
    }
}
