using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
#endif
public class ThreadedDataRequester : MonoBehaviour
{
	static readonly ConcurrentQueue<TaskInfo> DataQueue = new();
	static readonly List<Task> Tasks = new();

	public static void RequestData<T>(Func<T> task, Action<T> callback)
	{
		Task? _task = null;
		_task = Task.Run(() =>
		{
			DataQueue.Enqueue(new TaskInfo(v => callback.Invoke((T)v!), task.Invoke(), _task!));
		});
		Tasks.Add(_task);
	}

#if UNITY_EDITOR
	static ThreadedDataRequester() => UnityEditor.EditorApplication.update += DequeueResults;
#endif

	static void DequeueResults()
	{
		int l = DataQueue.Count;
		for (int i = 0; i < l; i++)
		{
			if (!DataQueue.TryDequeue(out TaskInfo taskInfo)) break;
			taskInfo.Callback(taskInfo.Parameter);
			Tasks.Remove(taskInfo.Task);
		}

		for (int i = 0; i < Tasks.Count; i++)
		{
			Task task = Tasks[i];
			if (task.IsFaulted)
			{
				Debug.LogError(task.Exception);
				Tasks.RemoveAt(i--);
			}
			else if (task.IsCanceled)
			{
				Debug.LogError("Task cancelled");
				Tasks.RemoveAt(i--);
			}
		}
	}

	void Update() => DequeueResults();

	void OnDestroy()
	{
		Task.WaitAll(Tasks.ToArray());
	}

	readonly struct TaskInfo
	{
		public readonly Action<object?> Callback;
		public readonly object? Parameter;
		public readonly Task Task;

		public TaskInfo(Action<object?> callback, object? parameter, Task task)
		{
			Callback = callback;
			Parameter = parameter;
			Task = task;
		}
	}
}
