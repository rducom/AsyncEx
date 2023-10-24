using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Nito.AsyncEx
{
    public sealed partial class AsyncContext
    {
        /// <summary>
        /// A blocking queue.
        /// </summary>
        public sealed class TaskQueue : IDisposable
        {
            /// <summary>
            /// The underlying blocking collection.
            /// </summary>
            private readonly ConcurrentQueue<Tuple<Task, bool>> _concurrentQueue = new ConcurrentQueue<Tuple<Task, bool>>();
            private int _isCompleted = 0;
            private readonly SemaphoreSlim _monitorRoot = new SemaphoreSlim(0);
            private const int _millisencondsBeforeTimeoutWait = 50;

            /// <summary>
            /// Initializes a new instance of the <see cref="TaskQueue"/> class.
            /// </summary>
            public TaskQueue()
            {
            }

            /// <summary>
            /// Gets a blocking enumerable that removes items from the queue. This enumerable only completes after <see cref="CompleteAdding"/> has been called.
            /// </summary>
            /// <returns>A blocking enumerable that removes items from the queue.</returns>
            public IEnumerable<Tuple<Task, bool>> GetConsumingEnumerable()
            {
                while (_isCompleted == 0 || !_concurrentQueue.IsEmpty)
                {
                    if (_concurrentQueue.TryDequeue(out var result))
                    {
                        yield return result;
                    }
                    if (_isCompleted == 0 && _concurrentQueue.IsEmpty)
                    {
                        // Attend une notification de l'ajout d'un élément ou de la fin d'ajout, timeout à 1 ms pour réduire les attentes infinies.
                        _monitorRoot.Wait(_millisencondsBeforeTimeoutWait);
                    }
                }
            }

            /// <summary>
            /// Generates an enumerable of <see cref="Task"/> instances currently queued to the scheduler waiting to be executed.
            /// </summary>
            /// <returns>An enumerable that allows traversal of tasks currently queued to this scheduler.</returns>
            public IEnumerable<Task> GetScheduledTasks()
            {
                var allItems = _concurrentQueue.ToArray();
                return allItems.Select(i => i.Item1);
            }

            /// <summary>
            /// Attempts to add the item to the queue. If the queue has been marked as complete for adding, this method returns <c>false</c>.
            /// </summary>
            /// <param name="item">The item to enqueue.</param>
            /// <param name="propagateExceptions">A value indicating whether exceptions on this task should be propagated out of the main loop.</param>
            public bool TryAdd(Task item, bool propagateExceptions)
            {
                if (_isCompleted > 0)
                {
                    return false;
                }
                _concurrentQueue.Enqueue(Tuple.Create(item, propagateExceptions));
                _monitorRoot.Release(1);
                return true;
            }

            /// <summary>
            /// Marks the queue as complete for adding, allowing the enumerator returned from <see cref="GetConsumingEnumerable"/> to eventually complete. This method may be called several times.
            /// </summary>
            public void CompleteAdding()
            {
                Interlocked.Increment(ref _isCompleted);
                _monitorRoot.Release(1);
            }

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose()
            {
            }
        }
    }
}
