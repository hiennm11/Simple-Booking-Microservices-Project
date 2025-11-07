# ⚡ Concurrency & Async/Await in .NET Microservices

**Category**: Computer Science Foundations  
**Difficulty**: Intermediate to Advanced  
**Focus**: Threading, async/await, and avoiding common pitfalls

---

## 📖 Overview

Modern .NET microservices use asynchronous programming extensively. Understanding how async/await works under the hood helps you write efficient, scalable code and debug concurrency issues.

---

## 🔄 Concurrency vs Parallelism

### Definitions

**Concurrency**: Multiple tasks making progress (not necessarily at same time)

```text
Single Core CPU:
Time: ──────────────────────────────→
       [Task A][Task B][Task A][Task B]
       (Interleaved execution)
```

**Parallelism**: Multiple tasks executing simultaneously

```text
Multi-Core CPU:
Core 1: [Task A──────────────────────]
Core 2: [Task B──────────────────────]
        (True simultaneous execution)
```

### Real Example

**Concurrency (async/await)**:

```csharp
public async Task<BookingDto> GetBookingAsync(Guid id)
{
    // Thread starts database query
    var booking = await _dbContext.Bookings.FindAsync(id);
    // Thread released during I/O, handles other requests
    
    // Thread returns to continue
    return new BookingDto
    {
        Id = booking.Id,
        Status = booking.Status
    };
}

// Single thread handles 1000s of concurrent requests
```

**Parallelism (Task.WhenAll)**:

```csharp
public async Task<DashboardDto> GetDashboardAsync(string userId)
{
    // Start all queries in parallel on thread pool
    var bookingsTask = _dbContext.Bookings
        .Where(b => b.UserId == userId)
        .ToListAsync();
    
    var paymentsTask = _dbContext.Payments
        .Where(p => p.UserId == userId)
        .ToListAsync();
    
    var userTask = _dbContext.Users
        .FindAsync(userId);
    
    // Wait for all to complete (parallel execution)
    await Task.WhenAll(bookingsTask, paymentsTask, userTask);
    
    // All results ready
    return new DashboardDto
    {
        Bookings = await bookingsTask,
        Payments = await paymentsTask,
        User = await userTask
    };
}

// Multiple threads execute database queries simultaneously
```

---

## 🧵 Threads in .NET

### Thread Basics

**Thread**: Execution path in a process

```csharp
// Create and start thread manually
var thread = new Thread(() =>
{
    Console.WriteLine($"Thread ID: {Thread.CurrentThread.ManagedThreadId}");
    Thread.Sleep(1000);
    Console.WriteLine("Work done");
});

thread.Start();
thread.Join(); // Wait for completion
```

**Cost**: ~1MB stack memory per thread + OS overhead

**Limit**: Few thousand threads max per process

### Thread Pool

**Shared pool of worker threads**, reused for tasks.

```csharp
// Queue work to thread pool
ThreadPool.QueueUserWorkItem(_ =>
{
    Console.WriteLine($"Thread ID: {Thread.CurrentThread.ManagedThreadId}");
    // Do work
});

// Or use Task.Run (preferred)
await Task.Run(() =>
{
    // CPU-bound work on thread pool
    var result = ComputeExpensiveCalculation();
    return result;
});
```

**Benefits**:

- ✅ Efficient: Threads reused, no creation overhead
- ✅ Scalable: Grows/shrinks dynamically
- ✅ Safe: Limits thread count automatically

**Thread Pool Size**:

```csharp
ThreadPool.GetMinThreads(out int minWorker, out int minIO);
ThreadPool.GetMaxThreads(out int maxWorker, out int maxIO);

Console.WriteLine($"Min: {minWorker} worker, {minIO} IO");
Console.WriteLine($"Max: {maxWorker} worker, {maxIO} IO");

// Typical output:
// Min: 8 worker, 8 IO
// Max: 32767 worker, 1000 IO
```

### Your ASP.NET Core Services

**Kestrel uses thread pool for requests**:

```text
Request 1 → Thread Pool Thread 5 → async/await → Thread released
Request 2 → Thread Pool Thread 3 → async/await → Thread released
Request 3 → Thread Pool Thread 7 → async/await → Thread released

100 concurrent requests, but only ~10-20 threads active at once!
```

---

## ⚙️ How Async/Await Works

### Under the Hood

**async/await is syntactic sugar** for state machine.

**Before async/await**:

```csharp
public void GetBookingCallback(Guid id, Action<Booking> callback)
{
    _dbContext.Bookings.FindAsync(id).ContinueWith(task =>
    {
        var booking = task.Result;
        callback(booking);
    });
}

// Nested callbacks → callback hell
```

**With async/await**:

```csharp
public async Task<Booking> GetBookingAsync(Guid id)
{
    var booking = await _dbContext.Bookings.FindAsync(id);
    return booking;
}

// Clean, synchronous-looking code
```

### State Machine

**Compiler transforms async method** into state machine.

**Your code**:

```csharp
public async Task<string> ProcessBookingAsync(Guid id)
{
    var booking = await GetBookingAsync(id);         // State 0 → State 1
    var payment = await ProcessPaymentAsync(booking); // State 1 → State 2
    return payment.TransactionId;                     // State 2 → Complete
}
```

**Compiler generates** (simplified):

```csharp
public Task<string> ProcessBookingAsync(Guid id)
{
    var stateMachine = new ProcessBookingStateMachine
    {
        Id = id,
        Builder = AsyncTaskMethodBuilder<string>.Create(),
        State = 0
    };
    
    stateMachine.Builder.Start(ref stateMachine);
    return stateMachine.Builder.Task;
}

struct ProcessBookingStateMachine : IAsyncStateMachine
{
    public int State;
    public Guid Id;
    public AsyncTaskMethodBuilder<string> Builder;
    
    private Booking _booking;
    private Payment _payment;
    
    public void MoveNext()
    {
        try
        {
            switch (State)
            {
                case 0:
                    // Start GetBookingAsync
                    var awaiter1 = GetBookingAsync(Id).GetAwaiter();
                    if (!awaiter1.IsCompleted)
                    {
                        State = 1;
                        Builder.AwaitUnsafeOnCompleted(ref awaiter1, ref this);
                        return; // Exit, resume when complete
                    }
                    goto case 1;
                
                case 1:
                    // GetBookingAsync completed
                    _booking = awaiter1.GetResult();
                    
                    // Start ProcessPaymentAsync
                    var awaiter2 = ProcessPaymentAsync(_booking).GetAwaiter();
                    if (!awaiter2.IsCompleted)
                    {
                        State = 2;
                        Builder.AwaitUnsafeOnCompleted(ref awaiter2, ref this);
                        return; // Exit, resume when complete
                    }
                    goto case 2;
                
                case 2:
                    // ProcessPaymentAsync completed
                    _payment = awaiter2.GetResult();
                    
                    // Return result
                    Builder.SetResult(_payment.TransactionId);
                    return;
            }
        }
        catch (Exception ex)
        {
            Builder.SetException(ex);
        }
    }
}
```

**Key Insight**: Method doesn't block, it returns immediately and resumes later.

### ConfigureAwait(false)

**Default**: async continuation resumes on original context (SynchronizationContext or TaskScheduler).

**In ASP.NET Core**: No SynchronizationContext, so `ConfigureAwait(false)` is unnecessary.

```csharp
// Library code: Use ConfigureAwait(false) to avoid context capture
public async Task<string> LibraryMethodAsync()
{
    var result = await _httpClient.GetStringAsync(url)
        .ConfigureAwait(false); // Don't capture context
    
    return result;
}

// ASP.NET Core controller: ConfigureAwait(false) not needed
public async Task<IActionResult> ControllerActionAsync()
{
    var result = await _service.GetDataAsync();
    // No SynchronizationContext, continuation is already threadpool
    
    return Ok(result);
}
```

---

## 🔒 Thread Safety

### Race Conditions

**Problem**: Multiple threads access shared state without synchronization.

```csharp
// ❌ NOT thread-safe
public class UnsafeCounter
{
    private int _count = 0;
    
    public void Increment()
    {
        _count++; // Not atomic! Read, increment, write
    }
    
    public int GetCount() => _count;
}

// Test with concurrent increments
var counter = new UnsafeCounter();
var tasks = Enumerable.Range(0, 1000)
    .Select(_ => Task.Run(() => counter.Increment()));

await Task.WhenAll(tasks);
Console.WriteLine(counter.GetCount()); // Expected: 1000, Actual: ~950 (lost updates!)
```

**Why**: `_count++` is three operations:

```assembly
1. Read _count into register
2. Increment register
3. Write register to _count
```

**Thread interleaving**:

```text
Thread A: Read _count (0)
Thread B: Read _count (0)
Thread A: Increment (0 → 1)
Thread B: Increment (0 → 1)
Thread A: Write (1)
Thread B: Write (1)
Result: _count = 1 (expected 2, lost 1 update)
```

### Solution 1: Interlocked (Lock-Free)

```csharp
// ✅ Thread-safe with Interlocked
public class SafeCounter
{
    private int _count = 0;
    
    public void Increment()
    {
        Interlocked.Increment(ref _count); // Atomic operation
    }
    
    public int GetCount() => Interlocked.CompareExchange(ref _count, 0, 0);
}

// Test: Always correct result
var counter = new SafeCounter();
var tasks = Enumerable.Range(0, 1000)
    .Select(_ => Task.Run(() => counter.Increment()));

await Task.WhenAll(tasks);
Console.WriteLine(counter.GetCount()); // Always 1000 ✓
```

**Interlocked Methods**:

- `Interlocked.Increment(ref value)`: Atomic ++
- `Interlocked.Decrement(ref value)`: Atomic --
- `Interlocked.Add(ref value, amount)`: Atomic +=
- `Interlocked.CompareExchange(ref value, newValue, comparand)`: Atomic compare-and-swap
- `Interlocked.Exchange(ref value, newValue)`: Atomic assignment

### Solution 2: Lock Statement

```csharp
// ✅ Thread-safe with lock
public class ThreadSafeBookingCache
{
    private readonly Dictionary<Guid, Booking> _cache = new();
    private readonly object _lock = new();
    
    public void Add(Guid id, Booking booking)
    {
        lock (_lock)
        {
            _cache[id] = booking;
        }
    }
    
    public Booking? Get(Guid id)
    {
        lock (_lock)
        {
            _cache.TryGetValue(id, out var booking);
            return booking;
        }
    }
    
    public void Remove(Guid id)
    {
        lock (_lock)
        {
            _cache.Remove(id);
        }
    }
}
```

**Performance**: Lock introduces contention, slower than Interlocked.

### Solution 3: SemaphoreSlim (Async-Compatible Lock)

```csharp
// ✅ Thread-safe async lock
public class AsyncSafeCache
{
    private readonly Dictionary<string, string> _cache = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1); // Max 1 concurrent
    
    public async Task<string?> GetOrAddAsync(string key, Func<Task<string>> factory)
    {
        await _semaphore.WaitAsync(); // Acquire lock
        try
        {
            if (_cache.TryGetValue(key, out var value))
            {
                return value;
            }
            
            // Not in cache, fetch
            value = await factory();
            _cache[key] = value;
            return value;
        }
        finally
        {
            _semaphore.Release(); // Release lock
        }
    }
}
```

**Why SemaphoreSlim**: `lock` keyword doesn't work with async/await.

```csharp
// ❌ DOES NOT COMPILE
public async Task BadAsync()
{
    lock (_lock)
    {
        await SomeOperationAsync(); // Error: Cannot await in lock
    }
}
```

### Solution 4: ConcurrentDictionary (Lock-Free Collection)

```csharp
// ✅ Thread-safe without explicit locking
public class ConcurrentCache
{
    private readonly ConcurrentDictionary<Guid, Booking> _cache = new();
    
    public void Add(Guid id, Booking booking)
    {
        _cache[id] = booking; // Thread-safe
    }
    
    public Booking? Get(Guid id)
    {
        _cache.TryGetValue(id, out var booking);
        return booking; // Thread-safe
    }
    
    public Booking GetOrAdd(Guid id, Func<Booking> factory)
    {
        return _cache.GetOrAdd(id, _ => factory()); // Thread-safe
    }
}
```

**Concurrent Collections**:

- `ConcurrentDictionary<TKey, TValue>`: Thread-safe dictionary
- `ConcurrentQueue<T>`: Thread-safe FIFO queue
- `ConcurrentStack<T>`: Thread-safe LIFO stack
- `ConcurrentBag<T>`: Thread-safe unordered collection

### Your Project: Where Thread Safety Matters

**Singleton Services**:

```csharp
// Registered as singleton → shared across all requests
builder.Services.AddSingleton<IEventBus, RabbitMqEventBus>();

// Must be thread-safe!
public class RabbitMqEventBus : IEventBus
{
    private readonly ConcurrentDictionary<string, List<Type>> _handlers = new();
    // ConcurrentDictionary is thread-safe
    
    public void Subscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>
    {
        var eventName = typeof(TEvent).Name;
        _handlers.AddOrUpdate(
            eventName,
            new List<Type> { typeof(THandler) },
            (key, existing) =>
            {
                existing.Add(typeof(THandler));
                return existing;
            }
        );
    }
}
```

**Scoped Services**: New instance per request → no shared state → thread-safe by default.

```csharp
// New instance per request
builder.Services.AddScoped<IBookingService, BookingService>();

// Each request gets own instance, no threading issues
```

---

## ⚠️ Common Async Pitfalls

### 1. Async Void (Never Use!)

```csharp
// ❌ BAD: Async void (unhandled exceptions crash app)
public async void ProcessBookingAsync(Guid id)
{
    await _dbContext.Bookings.FindAsync(id);
    throw new Exception("Boom!"); // Crashes app, can't catch
}

// ✅ GOOD: Async Task (exceptions can be caught)
public async Task ProcessBookingAsync(Guid id)
{
    await _dbContext.Bookings.FindAsync(id);
    throw new Exception("Boom!"); // Caller can catch
}

// Exception: Event handlers can be async void
private async void OnRabbitMqMessage(object sender, BasicDeliverEventArgs args)
{
    try
    {
        await HandleMessageAsync(args);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error handling message");
        // Must catch here, can't propagate
    }
}
```

### 2. Blocking on Async Code

```csharp
// ❌ DEADLOCK: Blocks thread, prevents continuation
public void BadMethod()
{
    var result = GetBookingAsync(id).Result; // BLOCKS
    // If GetBookingAsync needs this thread to continue → DEADLOCK
}

// ❌ DEADLOCK: Same problem
public void AnotherBadMethod()
{
    GetBookingAsync(id).Wait(); // BLOCKS
}

// ✅ GOOD: Await properly
public async Task GoodMethod()
{
    var result = await GetBookingAsync(id); // Doesn't block
}

// Exception: Main method
public static async Task Main(string[] args)
{
    // OK to block in Main (no context to deadlock)
    var app = builder.Build();
    await app.RunAsync(); // Or: app.Run() is also OK
}
```

### 3. Not Awaiting Task

```csharp
// ❌ FIRE-AND-FORGET: Exceptions lost, no error handling
public async Task ProcessBookingAsync(Guid id)
{
    _ = SendEmailAsync(id); // Not awaited! May never execute or fail silently
    
    return; // Method completes, SendEmailAsync may still be running
}

// ✅ GOOD: Await task
public async Task ProcessBookingAsync(Guid id)
{
    await SendEmailAsync(id); // Properly awaited, exceptions propagate
}

// If truly fire-and-forget, handle errors:
public async Task ProcessBookingAsync(Guid id)
{
    _ = Task.Run(async () =>
    {
        try
        {
            await SendEmailAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email");
        }
    });
}
```

### 4. async in Constructor (Impossible!)

```csharp
// ❌ CAN'T DO: Constructors can't be async
public class BookingService
{
    public BookingService()
    {
        await InitializeAsync(); // DOESN'T COMPILE
    }
}

// ✅ GOOD: Use factory pattern
public class BookingService
{
    private BookingService() { }
    
    public static async Task<BookingService> CreateAsync()
    {
        var service = new BookingService();
        await service.InitializeAsync();
        return service;
    }
    
    private async Task InitializeAsync()
    {
        // Async initialization
    }
}

// Or: Initialize in first method call
public class BookingService
{
    private Task? _initTask;
    
    private async Task EnsureInitializedAsync()
    {
        if (_initTask == null)
        {
            _initTask = InitializeAsync();
        }
        await _initTask;
    }
    
    public async Task<Booking> GetBookingAsync(Guid id)
    {
        await EnsureInitializedAsync();
        // Now initialized
    }
}
```

### 5. Parallel Execution Mistakes

```csharp
// ❌ BAD: Sequential execution (slow!)
public async Task<DashboardDto> SlowDashboardAsync(string userId)
{
    var bookings = await GetBookingsAsync(userId);    // Wait 100ms
    var payments = await GetPaymentsAsync(userId);    // Wait 100ms
    var user = await GetUserAsync(userId);            // Wait 100ms
    // Total: 300ms
    
    return new DashboardDto { Bookings = bookings, Payments = payments, User = user };
}

// ✅ GOOD: Parallel execution (fast!)
public async Task<DashboardDto> FastDashboardAsync(string userId)
{
    var bookingsTask = GetBookingsAsync(userId);    // Start immediately
    var paymentsTask = GetPaymentsAsync(userId);    // Start immediately
    var userTask = GetUserAsync(userId);            // Start immediately
    
    await Task.WhenAll(bookingsTask, paymentsTask, userTask); // Wait for all
    // Total: 100ms (parallel)
    
    return new DashboardDto
    {
        Bookings = await bookingsTask,
        Payments = await paymentsTask,
        User = await userTask
    };
}
```

---

## 🚀 Performance Best Practices

### 1. Avoid Unnecessary Task Creation

```csharp
// ❌ BAD: Wraps synchronous work in Task
public async Task<int> GetCachedValueAsync(string key)
{
    return await Task.FromResult(_cache[key]); // Unnecessary Task
}

// ✅ GOOD: Return Task directly if no async work
public Task<int> GetCachedValueAsync(string key)
{
    return Task.FromResult(_cache[key]);
}

// Even better: Synchronous method
public int GetCachedValue(string key)
{
    return _cache[key];
}
```

### 2. ValueTask for Hot Paths

**Task**: Allocates on heap (overhead)  
**ValueTask**: Struct, no allocation if synchronous result

```csharp
// ✅ GOOD: ValueTask for frequently synchronous path
public async ValueTask<Booking?> GetBookingAsync(Guid id)
{
    // Check cache first (fast path, synchronous)
    if (_cache.TryGetValue(id, out var cached))
    {
        return cached; // No Task allocation!
    }
    
    // Cache miss, fetch from database (slow path, asynchronous)
    var booking = await _dbContext.Bookings.FindAsync(id);
    _cache[id] = booking;
    return booking;
}
```

**When to use**:

- ✅ Method often returns synchronously (cached data)
- ✅ Hot path (called very frequently)
- ❌ Method always async (use Task)
- ❌ Stored in collection (ValueTask can't be awaited twice)

### 3. Parallel.ForEachAsync for CPU-Bound Work

```csharp
// Process many items in parallel
public async Task ProcessBookingsAsync(List<Guid> bookingIds)
{
    await Parallel.ForEachAsync(
        bookingIds,
        new ParallelOptions { MaxDegreeOfParallelism = 4 }, // Max 4 concurrent
        async (bookingId, ct) =>
        {
            var booking = await _dbContext.Bookings.FindAsync(bookingId, ct);
            await ProcessBookingAsync(booking, ct);
        }
    );
}
```

### 4. Channels for Producer-Consumer

**Better than BlockingCollection** for async scenarios.

```csharp
public class BackgroundEventProcessor
{
    private readonly Channel<IntegrationEvent> _channel = 
        Channel.CreateUnbounded<IntegrationEvent>();
    
    // Producer: Add events to channel
    public async Task PublishAsync(IntegrationEvent evt)
    {
        await _channel.Writer.WriteAsync(evt);
    }
    
    // Consumer: Process events from channel
    public async Task StartProcessingAsync(CancellationToken ct)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await ProcessEventAsync(evt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process event");
            }
        }
    }
}
```

---

## 🎓 Key Takeaways

### Async/Await Rules

1. **Async all the way**: Don't block with `.Result` or `.Wait()`
2. **Never async void**: Use async Task (except event handlers)
3. **Always await**: Don't fire-and-forget unless intentional (and handle errors)
4. **Use ConfigureAwait(false)** in libraries (not needed in ASP.NET Core)
5. **ValueTask for hot paths** with synchronous fast path

### Thread Safety Checklist

- [ ] Singleton services → Use ConcurrentDictionary or locks
- [ ] Scoped services → Usually safe (new instance per request)
- [ ] Static fields → Must be thread-safe
- [ ] Async locks → Use SemaphoreSlim, not `lock` keyword
- [ ] Interlocked for simple counters (lock-free, fast)

### Performance Tips

- Parallel execution with `Task.WhenAll` for independent operations
- `Parallel.ForEachAsync` for CPU-bound batch processing
- Channels for async producer-consumer patterns
- ValueTask for frequently synchronous operations

### Your Project Thread Safety

| Component | Thread Safety | Why |
|-----------|---------------|-----|
| RabbitMqEventBus | Singleton → Must be thread-safe | ConcurrentDictionary for handlers |
| BookingService | Scoped → Safe | New instance per request |
| DbContext | Scoped → Safe | New instance per request, not thread-safe internally |
| HttpClient | Singleton → Thread-safe | Reuse for connection pooling |

---

## 📚 Further Study

### Resources

- **Microsoft Docs**: "Asynchronous programming with async and await"
- **Stephen Cleary's Blog**: "There Is No Thread" (explains async deeply)
- **Book**: "Concurrency in C# Cookbook" by Stephen Cleary

### Practice

- Analyze your project: Find all singleton services, verify thread safety
- Benchmark: Compare sequential vs parallel execution
- Experiment: Reproduce race condition, fix with locks/Interlocked

### Related Documents

- [Data Structures for Microservices](./data-structures-for-microservices.md)
- [Algorithms in Practice](./algorithms-in-practice.md)
- [Networking Fundamentals](./networking-fundamentals.md)

---

**Last Updated**: November 7, 2025  
**Congratulations**: You've completed the Computer Science Foundations section!
