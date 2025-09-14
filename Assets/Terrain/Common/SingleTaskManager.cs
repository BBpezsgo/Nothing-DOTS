using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public static class SingleTaskManager<TKey, TResult>
{
    static readonly Dictionary<TKey, Task<TResult>> Requests = new();
    static readonly Dictionary<TKey, TResult> Cache = new();

    public static Task<TResult> Run(TKey key, Func<TKey, Task<TResult>> taskFactory)
    {
        lock (Requests)
        {
            if (Requests.TryGetValue(key, out Task<TResult>? result))
            {
                return result;
            }

            Task<TResult> task = Task.Run(async () =>
            {
                TResult result = await taskFactory(key);
                lock (Requests)
                {
                    Requests.Remove(key);
                }
                return result;
            });

            Requests.Add(key, task);

            return task;
        }
    }

    public static Task<TResult> RunCached(TKey key, Func<TKey, Task<TResult>> taskFactory)
    {
        if (Cache.TryGetValue(key, out var cached))
        {
            return Task.FromResult(cached);
        }

        return Task.Run(async () =>
        {
            TResult? result = await Run(key, taskFactory);
            Cache[key] = result;
            return result;
        });
    }
}
