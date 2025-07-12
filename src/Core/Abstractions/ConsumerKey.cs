﻿using System;

namespace Kafka.Ksql.Linq.Core.Abstractions;



public class ConsumerKey : IEquatable<ConsumerKey>
{
    public Type EntityType { get; }
    public string TopicName { get; }
    public string GroupId { get; }

    public ConsumerKey(Type entityType, string topicName, string groupId)
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        TopicName = topicName ?? throw new ArgumentNullException(nameof(topicName));
        GroupId = groupId ?? throw new ArgumentNullException(nameof(groupId));
    }

    public override int GetHashCode() => HashCode.Combine(EntityType, TopicName, GroupId);

    public bool Equals(ConsumerKey? other)
    {
        return other != null &&
               EntityType == other.EntityType &&
               TopicName == other.TopicName &&
               GroupId == other.GroupId;
    }

    public override bool Equals(object? obj) => obj is ConsumerKey other && Equals(other);

    public override string ToString() => $"{EntityType.Name}:{TopicName}:{GroupId}";
}
