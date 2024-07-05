using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.Events;
using JetBrains.Annotations;

namespace BagoumLib.Tasks;

/// <summary>
/// A container that queues tasks for sequential execution.
/// </summary>
[PublicAPI]
public class TaskQueue {
    private LazyTask? _currentTask;
    
    /// <summary>
    /// The currently executing task.
    /// </summary>
    public Task? CurrentTask => _currentTask?.Task;
    
    /// <summary>
    /// True when a task is being executed.
    /// </summary>
    public Evented<bool> ExecutingTransition { get; } = new(false);

    /// <summary>
    /// If true, then only one task can be queued up at a time. True by default.
    /// </summary>
    public bool AllowOnlyOneQueued { get; set; } = true;

    private Queue<Func<Task>> queue = new();

    /// <summary>
    /// Enqueue a task for execution.
    /// </summary>
    /// <returns>A task that is completed when the provided task is run to completion.</returns>
    public Task EnqueueTask(Func<Task> task) {
        var tcs = new TaskCompletionSource<Unit>();
        if (AllowOnlyOneQueued)
            queue.Clear();
        queue.Enqueue(() => task().Pipe(tcs));
        if (_currentTask == null)
            StartNextTask();
        return tcs.Task.ContinueWithSync(StartNextTask);
    }

    private void StartNextTask() {
        _currentTask = null;
        if (queue.TryDequeue(out var task)) {
            ExecutingTransition.PublishIfNotSame(true);
            _currentTask = new(task);
            _ = _currentTask.Task;
        } else 
            ExecutingTransition.PublishIfNotSame(false);
    }
    
    private class LazyTask(Func<Task> gen) {
        private Task? generated;
        public Task Task => generated ??= gen();
    }
}