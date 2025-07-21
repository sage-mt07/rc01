using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Kafka.Ksql.Linq;
using Kafka.Ksql.Linq.Query.Pipeline;

namespace Kafka.Ksql.Linq.Tests.Integration;

internal static class KsqlTestLinqExtensions
{
    public static IQueryable<T> Entity<T>(this KsqlContext context) where T : class
        => new TestEntityQueryable<T>(context);

    public static string ToQueryString<T>(this IQueryable<T> query)
    {
        if (query is not TestEntityQueryable<T> testQuery)
            throw new InvalidOperationException("Query must originate from KsqlContext.Entity<T>()");
        var generator = new DMLQueryGenerator();
        var topic = testQuery.Context.GetTopicName<T>();
        return generator.GenerateLinqQuery(topic, query.Expression, false);
    }

    private class TestEntityQueryable<T> : IQueryable<T>
    {
        public TestEntityQueryable(KsqlContext context)
            : this(context, Expression.Constant(new List<T>().AsQueryable()))
        {
        }

        public TestEntityQueryable(KsqlContext context, Expression expression)
        {
            Context = context;
            Expression = expression;
            Provider = new TestEntityQueryProvider(context);
        }

        internal KsqlContext Context { get; }
        public Type ElementType => typeof(T);
        public Expression Expression { get; }
        public IQueryProvider Provider { get; }
        public IEnumerator<T> GetEnumerator() => throw new NotSupportedException();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private class TestEntityQueryProvider : IQueryProvider
    {
        private readonly KsqlContext _context;
        public TestEntityQueryProvider(KsqlContext context) => _context = context;
        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetGenericArguments().First();
            var qType = typeof(TestEntityQueryable<>).MakeGenericType(elementType);
            return (IQueryable)Activator.CreateInstance(qType, _context, expression)!;
        }
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            => new TestEntityQueryable<TElement>(_context, expression);
        public object? Execute(Expression expression) => throw new NotSupportedException();
        public TResult Execute<TResult>(Expression expression) => throw new NotSupportedException();
    }
}
