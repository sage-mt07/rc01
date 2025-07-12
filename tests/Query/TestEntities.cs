using System;

#nullable enable

namespace Kafka.Ksql.Linq.Tests;

public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool? IsProcessed { get; set; }
    public string Type { get; set; } = string.Empty;
}

public class ChildEntity
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class GrandChildEntity
{
    public int Id { get; set; }
    public int ChildId { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class WindowDef
{
    public WindowDef TumblingWindow() => this;
    public WindowDef HoppingWindow() => this;
    public WindowDef SessionWindow() => this;
    public WindowDef Size(TimeSpan ts) => this;
    public WindowDef AdvanceBy(TimeSpan ts) => this;
    public WindowDef Gap(TimeSpan ts) => this;
    public WindowDef Retention(TimeSpan ts) => this;
    public WindowDef GracePeriod(TimeSpan ts) => this;
    public WindowDef EmitFinal() => this;
}

public class Order
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string? Region { get; set; }
}

public class Payment
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
}
