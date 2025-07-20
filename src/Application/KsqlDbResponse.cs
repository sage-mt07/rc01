namespace Kafka.Ksql.Linq.Application;

public record KsqlDbResponse(bool IsSuccess, string Message, int? ErrorCode = null, string? ErrorDetail = null);
