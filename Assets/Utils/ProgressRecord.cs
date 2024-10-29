using System;

class ProgressRecord<T> : IProgress<T>
{
    public T? Progress { get; private set; }
    readonly Action<T>? _handler;

    public ProgressRecord(Action<T>? handler)
    {
        Progress = default;
        _handler = handler;
    }

    public void Report(T value)
    {
        Progress = value;
        _handler?.Invoke(value);
    }
}
