﻿using System;

namespace Kafka.Ksql.Linq.Core.Exceptions;
public class EntityModelException : CoreException
{
    public Type EntityType { get; }

    public EntityModelException(Type entityType, string message)
        : base($"Entity model error for {entityType.Name}: {message}")
    {
        EntityType = entityType;
    }

    public EntityModelException(Type entityType, string message, Exception innerException)
        : base($"Entity model error for {entityType.Name}: {message}", innerException)
    {
        EntityType = entityType;
    }
}
