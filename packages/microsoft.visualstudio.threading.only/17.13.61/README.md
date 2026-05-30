# Microsoft.VisualStudio.Threading

Async synchronization primitives, async collections, TPL and dataflow extensions. The JoinableTaskFactory allows synchronously blocking the UI thread for async work. This package is applicable to any .NET application (not just Visual Studio).

[Full documentation](https://microsoft.github.io/vs-threading/docs/getting-started.html).

## Features

* Async versions of many threading synchronization primitives
  * `AsyncAutoResetEvent`
  * `AsyncBarrier`
  * `AsyncCountdownEvent`
  * `AsyncManualResetEvent`
  * `AsyncReaderWriterLock`
  * `AsyncSemaphore`
  * `ReentrantSemaphore`
* Async versions of very common types
  * `AsyncEventHandler`
  * `AsyncLazy<T>`
  * `AsyncLazyInitializer`
  * `AsyncLocal<T>`
  * `AsyncQueue<T>`
* Await extension methods
  * Await on a `TaskScheduler` to switch to it.
    Switch to a background thread with `await TaskScheduler.Default;`
  * Await on a `Task` with a timeout
  * Await on a `Task` with cancellation
* `JoinableTaskFactory` that allows you to schedule asynchronous or synchronous work
  that does not deadlock with the UI thread even when the UI thread needs to
  synchronously block on the result.
