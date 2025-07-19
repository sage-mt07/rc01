namespace Kafka.Ksql.Linq.Configuration;

/// <summary>
/// Global decimal precision settings used across query generation and schema definitions.
/// </summary>
public static class DecimalPrecisionConfig
{
    /// <summary>
    /// Precision applied when mapping decimal types. Default is 38.
    /// </summary>
    public static int DecimalPrecision { get; set; } = 38;

    /// <summary>
    /// Scale applied when mapping decimal types. Default is 9.
    /// </summary>
    public static int DecimalScale { get; set; } = 9;
}
