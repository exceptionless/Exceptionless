# AsyncFixer

[![NuGet](https://img.shields.io/nuget/v/AsyncFixer.svg)](https://www.nuget.org/packages/AsyncFixer)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AsyncFixer.svg)](https://www.nuget.org/packages/AsyncFixer)

AsyncFixer helps developers find and correct common `async/await` misuses (anti-patterns) and, when possible, offers automatic fixes. It currently reports 6 categories of async/await misuse and provides code fixes for 3 of them. It has been validated against thousands of open-source C# projects and is designed to handle tricky real-world edge cases. Tool-friendly diagnostics support AI-assisted workflows even when a built-in code fix is not available. It is a popular open-source Roslyn analyzer for improving async/await code quality.

## Installation

**NuGet (Recommended):** Install as a project-local analyzer that participates in builds:

```bash
dotnet add package AsyncFixer
```

Or via Package Manager:
```powershell
Install-Package AsyncFixer
```

**VSIX:** Install as a Visual Studio extension from the [VS Marketplace](https://marketplace.visualstudio.com/items?itemName=SemihOkur.AsyncFixer2022).

## Quick Reference

| Rule | Description | Has Fix |
|------|-------------|:-------:|
| [AsyncFixer01](#asyncfixer01) | Unnecessary async/await usage | ✅ |
| [AsyncFixer02](#asyncfixer02) | Blocking calls inside async methods | ✅ |
| [AsyncFixer03](#asyncfixer03) | Async void methods and delegates | ✅ |
| [AsyncFixer04](#asyncfixer04) | Unawaited async call in using block | ❌ |
| [AsyncFixer05](#asyncfixer05) | Nested Task from TaskFactory.StartNew | ❌ |
| [AsyncFixer06](#asyncfixer06) | Task&lt;T&gt; to Task implicit conversion | ❌ |

## Configuration

You can configure rule severity in your `.editorconfig`:

```ini
[*.cs]
# Disable a rule
dotnet_diagnostic.AsyncFixer01.severity = none

# Treat as error
dotnet_diagnostic.AsyncFixer02.severity = error

# Treat as suggestion
dotnet_diagnostic.AsyncFixer03.severity = suggestion
```

Or suppress individual occurrences:

```csharp
#pragma warning disable AsyncFixer01
async Task<int> Method() => await SomeTaskAsync();
#pragma warning restore AsyncFixer01
```

---

## AsyncFixer01
### Unnecessary async/await usage

There are some async methods where there is no need to use `async/await` keywords. It is important to detect this kind of misuse because adding the async modifier comes at a price. AsyncFixer automatically removes `async/await` keywords from those methods.

**Example:**

```csharp
// ❌ Bad: Unnecessary async/await
async Task<int> GetValueAsync()
{
    return await _cache.GetAsync(key);
}

// ✅ Good: Return task directly
Task<int> GetValueAsync()
{
    return _cache.GetAsync(key);
}
```

> **Note:** Keep `async/await` when you need exception handling around the await, when there are multiple awaits, or when the method is inside a `using` or `try` block.

![asyncfixer-1.gif](https://raw.githubusercontent.com/semihokur/AsyncFixer/main/img/asyncfixer-1.gif)

## AsyncFixer02
### Long-running or blocking operations inside an async method

Developers use some potentially long-running or blocking operations inside async methods even though there are corresponding asynchronous versions of these methods in .NET or third-party libraries.

**Common blocking calls and their async replacements:**

| Blocking Call | Async Replacement |
|--------------|-------------------|
| `Task.Wait()` | `await task` |
| `Task.Result` | `await task` |
| `Task.WaitAll()` | `await Task.WhenAll()` |
| `Task.WaitAny()` | `await Task.WhenAny()` |
| `Thread.Sleep()` | `await Task.Delay()` |
| `StreamReader.ReadToEnd()` | `await StreamReader.ReadToEndAsync()` |

**Example:**

```csharp
// ❌ Bad: Blocking call can cause deadlocks
async Task ProcessAsync()
{
    var result = GetDataAsync().Result;  // Blocks!
    Thread.Sleep(1000);                   // Blocks!
}

// ✅ Good: Use async equivalents
async Task ProcessAsync()
{
    var result = await GetDataAsync();
    await Task.Delay(1000);
}
```

![asyncfixer-2.gif](https://raw.githubusercontent.com/semihokur/AsyncFixer/main/img/asyncfixer-2.gif)

## AsyncFixer03
### Fire-and-forget *async-void* methods and delegates

Some async methods and delegates are fire-and-forget, which return `void`. Unless a method is only called as an event handler, it must be awaitable. Otherwise, it is a code smell because it complicates control flow and makes error detection/correction difficult. Unhandled exceptions in those *async-void* methods and delegates will crash the process.

**Example:**

```csharp
// ❌ Bad: async void - exceptions will crash the process
async void ProcessDataAsync()
{
    await Task.Delay(1000);
    throw new Exception("Oops!"); // Crashes the app!
}

// ✅ Good: async Task - exceptions can be caught
async Task ProcessDataAsync()
{
    await Task.Delay(1000);
    throw new Exception("Oops!"); // Can be caught by caller
}
```

> **Note:** `async void` is acceptable for event handlers like `button_Click`.

![asyncfixer-3.gif](https://raw.githubusercontent.com/semihokur/AsyncFixer/main/img/asyncfixer-3.gif)

## AsyncFixer04
### Fire-and-forget async call inside an *using* block

Inside a `using` block, developers insert a fire-and-forget async call which uses a disposable object as a parameter or target object. It can cause potential exceptions or wrong results because the resource may be disposed before the async operation completes.

**Example:**

```csharp
// ❌ Bad: Stream disposed before copy completes
using (var stream = new FileStream("file.txt", FileMode.Open))
{
    stream.CopyToAsync(destination);  // Fire-and-forget!
}  // stream disposed here - CopyToAsync may still be running!

// ✅ Good: Await the async operation
using (var stream = new FileStream("file.txt", FileMode.Open))
{
    await stream.CopyToAsync(destination);
}
```

## AsyncFixer05
### Downcasting from a nested task to an outer task

Downcasting from a nested task to a task or awaiting a nested task is dangerous. There is no way to wait for and get the result of the child task. This usually occurs when mixing `async/await` keywords with the old threading APIs such as `TaskFactory.StartNew`.

**Example:**

```csharp
// ❌ Bad: StartNew returns Task<Task>, outer await completes immediately
async Task ProcessAsync()
{
    Console.WriteLine("Hello");
    await Task.Factory.StartNew(() => Task.Delay(1000)); // Returns Task<Task>!
    Console.WriteLine("World");  // Prints immediately, doesn't wait 1 second
}

// ✅ Good: Use Unwrap() or Task.Run()
async Task ProcessAsync()
{
    Console.WriteLine("Hello");
    await Task.Factory.StartNew(() => Task.Delay(1000)).Unwrap();
    // Or simply:
    await Task.Run(() => Task.Delay(1000));
    Console.WriteLine("World");  // Waits 1 second
}
```

**Fixes:**

1. Double await: `await (await Task.Factory.StartNew(() => Task.Delay(1000)));`
2. Use `Unwrap()`: `await Task.Factory.StartNew(() => Task.Delay(1000)).Unwrap();`
3. Use `Task.Run()`: `await Task.Run(() => Task.Delay(1000));` *(preferred)*

## AsyncFixer06
### Discarded `Task<T>` result when converted to `Task`

When a non-async lambda or delegate returns `Task<T>` but is assigned to a `Func<Task>` or similar delegate type expecting `Task`, the result value is silently discarded. This is because `Task<T>` implicitly converts to `Task`, but the generic result is lost.

> **Note:** For async lambdas, the compiler catches this with error CS8031. However, for non-async lambdas, there is no warning - the conversion happens silently.

**Example:**

```csharp
// ❌ Bad: Task<string> silently converted to Task, result discarded
Func<Task> fn = () => GetDataAsync();  // GetDataAsync returns Task<string>
await fn();  // The string result is lost!

// ✅ Good: Use correct delegate type
Func<Task<string>> fn = () => GetDataAsync();
var result = await fn();

// ✅ Also Good: Explicit discard if you don't need the result
Func<Task> fn = async () => { _ = await GetDataAsync(); };
```

---

## FAQ

### Should I always follow AsyncFixer01? What are the benefits of eliding async/await?

**Yes, in most cases.** Removing unnecessary `async/await` provides real benefits:

**Benefits of eliding async/await:**

1. **Performance**: Avoids allocating a state machine object for every call. In hot paths, this reduces GC pressure and improves throughput.
2. **Reduced overhead**: Eliminates the state machine's `MoveNext()` invocations and task wrapping/unwrapping.
3. **Simpler IL**: The compiled code is smaller and more straightforward.
4. **Consistent behavior**: When you just pass through a task, the behavior is identical to the underlying method.

```csharp
// ❌ Unnecessary overhead - allocates state machine
async Task<User> GetUserAsync(int id) => await _repository.GetUserAsync(id);

// ✅ Direct passthrough - no allocation, same behavior
Task<User> GetUserAsync(int id) => _repository.GetUserAsync(id);
```

**When to keep async/await:**

There are specific scenarios where you should keep `async/await`:

1. **Inside `using` blocks**: Ensures disposal happens after the task completes.
2. **Inside `try/catch`**: Required to catch exceptions from the awaited task.
3. **Multiple awaits**: When the method has more than one await expression.
4. **Exception stack traces matter**: `async/await` preserves the method in stack traces.

```csharp
// Keep async/await here - inside using block
async Task ProcessAsync()
{
    using var stream = new FileStream("file.txt", FileMode.Open);
    await stream.ReadAsync(buffer);  // Must await before disposal
}

// Keep async/await here - exception handling
async Task<Data> GetDataAsync()
{
    try {
        return await _client.GetAsync();
    } catch (HttpException) {
        return Data.Empty;
    }
}
```

**Summary**: Follow AsyncFixer01 for simple passthrough methods. Suppress it only when you have a specific reason (debugging needs, exception handling, resource management).

### Why does AsyncFixer02 warn about `.Result` after `await Task.WhenAll()`?

It doesn't anymore! AsyncFixer recognizes this safe pattern:

```csharp
Task<int>[] tasks = CreateTasks();
await Task.WhenAll(tasks);  // All tasks are now completed

foreach (var task in tasks)
    Console.WriteLine(task.Result);  // Safe - no warning
```

After `await Task.WhenAll()`, all tasks in the collection are guaranteed to be completed, so accessing `.Result` won't block. If you're still seeing warnings, make sure you have the latest version of AsyncFixer.

### What is the difference between AsyncFixer05 and AsyncFixer06?

Both rules detect task type mismatches, but they address different problems:

| Aspect | AsyncFixer05 | AsyncFixer06 |
|--------|--------------|--------------|
| **Pattern** | `Task<Task<T>>` (nested task) | `Task<T>` → `Task` (implicit conversion) |
| **Problem** | Awaiting outer task doesn't wait for inner task | Result value `T` is silently discarded |
| **Context** | `TaskFactory.StartNew` with async lambdas | Lambda assignments to `Func<Task>` |
| **Fix** | Use `Unwrap()` or `Task.Run()` | Change delegate type to `Func<Task<T>>` |

```csharp
// AsyncFixer05 - nested task: Task<Task>
Task task = Task.Factory.StartNew(() => DelayAsync());

// AsyncFixer06 - result discarded: Task<string> converted to Task
Func<Task> fn = () => GetDataAsync();  // GetDataAsync returns Task<string>
```

### How do I suppress AsyncFixer warnings?

There are several ways:

**1. Inline suppression (single occurrence):**
```csharp
#pragma warning disable AsyncFixer01
async Task<int> Method() => await SomeTaskAsync();
#pragma warning restore AsyncFixer01
```

**2. Attribute suppression (method or class level):**
```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage("AsyncUsage", "AsyncFixer01")]
async Task<int> Method() => await SomeTaskAsync();
```

**3. EditorConfig (project-wide):**
```ini
[*.cs]
dotnet_diagnostic.AsyncFixer01.severity = none
```

**4. GlobalSuppressions.cs (assembly level):**
```csharp
[assembly: SuppressMessage("AsyncUsage", "AsyncFixer01", Justification = "Team preference")]
```

---

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## License

This project is licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE).
