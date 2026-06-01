using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

class MainThreadManager : Singleton<MainThreadManager>
{
    readonly ConcurrentQueue<(Func<object> Task, TaskCompletionSource<object> CompletionSource)> PendingTasks = new();

    public async Task<T> ScheduleAsync<T>(Func<T> work) where T : notnull
    {
        TaskCompletionSource<object> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingTasks.Enqueue((() => work(), tcs));
        return (T)await tcs.Task;
    }

    void Update()
    {
        while (PendingTasks.TryDequeue(out (Func<object> Task, TaskCompletionSource<object> CompletionSource) entry))
        {
            try
            {
                object res = entry.Task();
                entry.CompletionSource.SetResult(res);
            }
            catch (Exception ex)
            {
                entry.CompletionSource.SetException(ex);
            }
        }
    }

    void OnDestroy() => Dispose();

    void OnDisable() => Dispose();

    void Dispose()
    {
        foreach ((Func<object> Task, TaskCompletionSource<object> CompletionSource) item in PendingTasks)
        {
            item.CompletionSource.SetCanceled();
        }
        PendingTasks.Clear();
    }
}
