using System;
using System.Threading.Tasks;

namespace Kafka.Ksql.Linq.Core.Abstractions;

internal class ManualCommitMessage<T> : IManualCommitMessage<T> where T : class
{
    public T Value { get; }

    private readonly Func<Task> _commitAction;
    private readonly Func<Task> _negativeAckAction;
    private bool _committed = false;

    internal ManualCommitMessage(T value, Func<Task> commitAction, Func<Task> negativeAckAction)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        _commitAction = commitAction ?? throw new ArgumentNullException(nameof(commitAction));
        _negativeAckAction = negativeAckAction ?? throw new ArgumentNullException(nameof(negativeAckAction));
    }

    public async Task CommitAsync()
    {
        if (_committed)
            return;

        await _commitAction();
        _committed = true;
    }

    public async Task NegativeAckAsync()
    {
        if (_committed)
            return;

        await _negativeAckAction();
        _committed = true;
    }
}
