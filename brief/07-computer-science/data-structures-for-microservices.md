# 📊 Data Structures for Microservices

**Category**: Computer Science Foundations  
**Difficulty**: Intermediate  
**Focus**: Practical application in distributed systems

---

## 📖 Overview

Data structures are fundamental building blocks in software. In microservices, choosing the right data structure impacts performance, scalability, and reliability. This document connects CS theory to real microservices implementations.

---

## 🗂️ Hash Tables (Hash Maps / Dictionaries)

### Theory

**Hash Table**: Data structure that maps keys to values using a hash function.

```
Time Complexity:
- Insert: O(1) average
- Lookup: O(1) average
- Delete: O(1) average
- Worst case: O(n) with poor hash function
```

**How It Works**:
```
Key → Hash Function → Index → Value

Example:
"user123" → hash() → 42 → UserObject
```

### In Your Microservices Project

#### 1. **Caching (Redis/In-Memory)**

```csharp
// In-memory cache using Dictionary (Hash Table)
public class UserCache
{
    private readonly Dictionary<string, User> _cache = new();
    
    public User? GetUser(string userId)
    {
        // O(1) lookup - instant retrieval
        if (_cache.TryGetValue(userId, out var user))
        {
            return user;
        }
        return null;
    }
    
    public void AddUser(User user)
    {
        // O(1) insert
        _cache[user.Id] = user;
    }
}
```

**Why Hash Table?**
- ✅ O(1) lookup - critical for high-traffic APIs
- ✅ Fast cache hit/miss determination
- ✅ Efficient memory usage with good hash function

#### 2. **Service Registry/Discovery**

```csharp
// Service registry maps service names to URLs
public class ServiceRegistry
{
    private readonly Dictionary<string, ServiceEndpoint> _services = new()
    {
        ["UserService"] = new ServiceEndpoint("http://localhost:5001"),
        ["BookingService"] = new ServiceEndpoint("http://localhost:5002"),
        ["PaymentService"] = new ServiceEndpoint("http://localhost:5003")
    };
    
    public string? GetServiceUrl(string serviceName)
    {
        // O(1) lookup - no iteration needed
        return _services.TryGetValue(serviceName, out var endpoint) 
            ? endpoint.Url 
            : null;
    }
}
```

#### 3. **Configuration Management**

```csharp
// appsettings.json → Dictionary in memory
public class AppSettings
{
    public Dictionary<string, string> ConnectionStrings { get; set; }
    public Dictionary<string, int> RateLimits { get; set; }
    
    // O(1) access to any setting
    public string GetConnectionString(string name)
    {
        return ConnectionStrings[name];
    }
}
```

### Real-World Application

**API Gateway Routing** (Your YARP Implementation):
```csharp
// Route table is a hash map
var routes = new Dictionary<string, RouteConfig>
{
    ["/users"] = new RouteConfig { Destination = "UserService" },
    ["/bookings"] = new RouteConfig { Destination = "BookingService" },
    ["/payment"] = new RouteConfig { Destination = "PaymentService" }
};

// O(1) route lookup for every request
public string GetDestination(string path)
{
    return routes[path].Destination;
}
```

**Performance Impact**:
- 1,000 requests/sec
- Without hash table (array): O(n) = 3 comparisons average = 3,000 operations
- With hash table: O(1) = 1 comparison = 1,000 operations
- **3x faster routing!**

---

## 📋 Queues (FIFO - First In, First Out)

### Theory

**Queue**: Linear data structure following FIFO principle.

```
Operations:
- Enqueue (add): O(1)
- Dequeue (remove): O(1)
- Peek (view front): O(1)

[Front] ← [Item 4] ← [Item 3] ← [Item 2] ← [Item 1] ← [Rear]
```

### In Your Microservices Project

#### 1. **Message Queue (RabbitMQ)**

```csharp
// RabbitMQ is essentially a distributed queue
public class RabbitMQEventBus : IEventBus
{
    public async Task PublishAsync<T>(T @event, string queueName)
    {
        // Enqueue message to queue
        var body = JsonSerializer.SerializeToUtf8Bytes(@event);
        channel.BasicPublish(
            exchange: "",
            routingKey: queueName,
            body: body
        );
        // Message added to REAR of queue
    }
}

public class EventConsumer
{
    public void StartConsuming(string queueName)
    {
        channel.BasicConsume(
            queue: queueName,
            autoAck: false,
            consumer: consumer
        );
        // Messages consumed from FRONT of queue (FIFO)
    }
}
```

**Why Queue?**
- ✅ **Guaranteed order**: First event published = first processed
- ✅ **Load leveling**: Consumer processes at its own pace
- ✅ **Decoupling**: Producer and consumer don't need to be online simultaneously

#### 2. **Background Job Queue**

```csharp
// Outbox publisher processes messages in FIFO order
public class OutboxPublisherService
{
    private async Task PublishPendingMessagesAsync()
    {
        // Get messages in FIFO order (oldest first)
        var messages = await _dbContext.OutboxMessages
            .Where(m => !m.Published)
            .OrderBy(m => m.CreatedAt)  // ← FIFO ordering
            .Take(100)
            .ToListAsync();
        
        // Process in order
        foreach (var message in messages)
        {
            await PublishMessageAsync(message);
        }
    }
}
```

**Real-World Scenario**:
```
Booking Flow (Order Matters!):
1. BookingCreated (10:00:00)
2. PaymentProcessed (10:00:05)
3. BookingConfirmed (10:00:10)

Queue ensures events processed in order:
If PaymentProcessed arrives before BookingCreated, 
it waits in queue until BookingCreated is processed.
```

#### 3. **Request Queue for Rate Limiting**

```csharp
// Token Bucket algorithm uses queue
public class TokenBucketRateLimiter
{
    private readonly Queue<DateTime> _requestTimestamps = new();
    
    public bool AllowRequest()
    {
        var now = DateTime.UtcNow;
        
        // Remove old requests (dequeue from front)
        while (_requestTimestamps.Count > 0 
               && _requestTimestamps.Peek() < now.AddMinutes(-1))
        {
            _requestTimestamps.Dequeue();
        }
        
        // Check rate limit
        if (_requestTimestamps.Count < 100)
        {
            _requestTimestamps.Enqueue(now); // Add to rear
            return true;
        }
        
        return false; // Rate limit exceeded
    }
}
```

### Performance Analysis

**Without Queue (Direct Processing)**:
```
Problem: Spike of 1000 requests in 1 second
Result: Service crashes (can't handle load)
```

**With Queue**:
```
1000 requests arrive → Queue buffers them
Service processes 10/sec → Takes 100 seconds
Result: ✅ Service stable, all requests processed
```

---

## 🌳 Trees (Hierarchical Structures)

### Theory

**Tree**: Hierarchical data structure with nodes connected by edges.

```
Binary Search Tree (BST):
       50
      /  \
    30    70
   /  \   /  \
  20  40 60  80

Operations (balanced):
- Search: O(log n)
- Insert: O(log n)
- Delete: O(log n)
```

### In Your Microservices Project

#### 1. **Service Dependency Tree**

```
Your Architecture:
                API Gateway (Root)
                     |
        ┌────────────┼────────────┐
        │            │            │
   UserService  BookingService  PaymentService
                     |
              ┌──────┴──────┐
         PostgreSQL      RabbitMQ
```

**Tree Traversal for Health Checks**:
```csharp
public class ServiceHealthChecker
{
    public async Task<HealthStatus> CheckSystemHealth()
    {
        // DFS (Depth-First Search) through service tree
        var gatewayHealth = await CheckService("Gateway");
        if (!gatewayHealth.IsHealthy) return gatewayHealth;
        
        // Check children (parallel)
        var userHealth = await CheckService("UserService");
        var bookingHealth = await CheckService("BookingService");
        var paymentHealth = await CheckService("PaymentService");
        
        // Aggregate results (post-order traversal)
        return AggregateHealth(gatewayHealth, userHealth, bookingHealth, paymentHealth);
    }
}
```

#### 2. **JSON Parsing (Document Tree)**

```csharp
// Event payload is a tree structure
{
  "eventId": "guid",              // Leaf
  "eventName": "BookingCreated",  // Leaf
  "data": {                        // Internal node
    "bookingId": "guid",          // Leaf
    "amount": 500000,             // Leaf
    "user": {                      // Internal node
      "id": "guid",               // Leaf
      "name": "John"              // Leaf
    }
  }
}

// Tree traversal to extract data
public class EventParser
{
    public BookingData ParseEvent(JsonDocument doc)
    {
        // Navigate tree: root → data → bookingId
        var root = doc.RootElement;
        var data = root.GetProperty("data");
        var bookingId = data.GetProperty("bookingId").GetString();
        
        // Recursive descent parsing
        return new BookingData { BookingId = bookingId };
    }
}
```

#### 3. **URL Routing Tree**

```
API Gateway Routes (Trie/Prefix Tree):
                /
               / \
           users booking
            /      |    \
        register  create  {id}
```

```csharp
// Route matching uses prefix tree
public class RouteTree
{
    public RouteNode Root = new("/");
    
    public void AddRoute(string path, RouteConfig config)
    {
        var parts = path.Split('/');
        var current = Root;
        
        foreach (var part in parts)
        {
            // Traverse/build tree
            if (!current.Children.ContainsKey(part))
            {
                current.Children[part] = new RouteNode(part);
            }
            current = current.Children[part];
        }
        
        current.Config = config;
    }
    
    public RouteConfig? FindRoute(string path)
    {
        // O(k) where k = path depth
        // Much faster than O(n) linear search
        var parts = path.Split('/');
        var current = Root;
        
        foreach (var part in parts)
        {
            if (!current.Children.ContainsKey(part))
                return null;
            current = current.Children[part];
        }
        
        return current.Config;
    }
}
```

**Performance**:
- 1000 routes
- Linear search: O(n) = 1000 comparisons
- Tree search: O(log n) = ~10 comparisons
- **100x faster!**

---

## 📈 Heaps (Priority Queue)

### Theory

**Heap**: Binary tree where parent is always greater (max-heap) or smaller (min-heap) than children.

```
Min-Heap (Priority Queue):
       1
      / \
     3   2
    / \
   7   5

Operations:
- Insert: O(log n)
- Extract-Min: O(log n)
- Peek-Min: O(1)
```

### In Your Microservices Project

#### 1. **Task Scheduling (Background Jobs)**

```csharp
// Schedule outbox publishing with priority
public class TaskScheduler
{
    // Min-heap: earliest task at top
    private readonly PriorityQueue<OutboxTask, DateTime> _tasks = new();
    
    public void ScheduleTask(OutboxTask task, DateTime executeAt)
    {
        // O(log n) insert
        _tasks.Enqueue(task, executeAt);
    }
    
    public async Task RunScheduler()
    {
        while (true)
        {
            if (_tasks.TryPeek(out var task, out var executeAt))
            {
                if (DateTime.UtcNow >= executeAt)
                {
                    // O(log n) extract minimum
                    _tasks.Dequeue();
                    await task.ExecuteAsync();
                }
            }
            
            await Task.Delay(100);
        }
    }
}
```

#### 2. **Rate Limiting with Priority**

```csharp
// Premium users get priority in queue
public class PriorityRateLimiter
{
    private readonly PriorityQueue<Request, int> _queue = new();
    
    public void QueueRequest(Request request, UserTier tier)
    {
        int priority = tier switch
        {
            UserTier.Premium => 1,    // Lowest number = highest priority
            UserTier.Standard => 5,
            UserTier.Free => 10
        };
        
        _queue.Enqueue(request, priority);
    }
    
    public async Task ProcessRequests()
    {
        while (_queue.Count > 0)
        {
            // Always process highest priority first
            var request = _queue.Dequeue();
            await ProcessRequestAsync(request);
        }
    }
}
```

#### 3. **Retry with Exponential Backoff**

```csharp
// Failed events with retry backoff
public class RetryScheduler
{
    private readonly PriorityQueue<FailedEvent, DateTime> _retryQueue = new();
    
    public void ScheduleRetry(FailedEvent @event, int attemptNumber)
    {
        // Exponential backoff: 2^attempt seconds
        var retryAfter = DateTime.UtcNow.AddSeconds(Math.Pow(2, attemptNumber));
        
        // Heap ensures earliest retry processed first
        _retryQueue.Enqueue(@event, retryAfter);
    }
}
```

---

## 🕸️ Graphs (Network Structures)

### Theory

**Graph**: Set of nodes (vertices) connected by edges.

```
Types:
- Directed: One-way connections
- Undirected: Two-way connections
- Weighted: Edges have costs

Representations:
- Adjacency Matrix: O(V²) space, O(1) edge lookup
- Adjacency List: O(V+E) space, O(degree) edge lookup
```

### In Your Microservices Project

#### 1. **Service Dependency Graph**

```csharp
// Directed graph: A → B means A depends on B
public class ServiceDependencyGraph
{
    private readonly Dictionary<string, List<string>> _dependencies = new()
    {
        ["APIGateway"] = new List<string> 
            { "UserService", "BookingService", "PaymentService" },
        ["BookingService"] = new List<string> 
            { "PostgreSQL", "RabbitMQ" },
        ["PaymentService"] = new List<string> 
            { "MongoDB", "RabbitMQ" }
    };
    
    // BFS to find all dependencies
    public List<string> GetAllDependencies(string service)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(service);
        
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Contains(current)) continue;
            
            visited.Add(current);
            
            if (_dependencies.ContainsKey(current))
            {
                foreach (var dependency in _dependencies[current])
                {
                    queue.Enqueue(dependency);
                }
            }
        }
        
        return visited.ToList();
    }
    
    // DFS to detect circular dependencies
    public bool HasCircularDependency()
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        
        foreach (var service in _dependencies.Keys)
        {
            if (HasCycleDFS(service, visited, recursionStack))
                return true;
        }
        
        return false;
    }
    
    private bool HasCycleDFS(string service, 
                             HashSet<string> visited, 
                             HashSet<string> stack)
    {
        visited.Add(service);
        stack.Add(service);
        
        if (_dependencies.ContainsKey(service))
        {
            foreach (var dependency in _dependencies[service])
            {
                if (!visited.Contains(dependency))
                {
                    if (HasCycleDFS(dependency, visited, stack))
                        return true;
                }
                else if (stack.Contains(dependency))
                {
                    // Circular dependency detected!
                    return true;
                }
            }
        }
        
        stack.Remove(service);
        return false;
    }
}
```

**Application**: Startup Order
```csharp
// Topological sort to determine startup order
public List<string> GetStartupOrder()
{
    // Services with no dependencies start first
    // Result: [PostgreSQL, MongoDB, RabbitMQ, UserService, 
    //          BookingService, PaymentService, APIGateway]
    return TopologicalSort(_dependencies);
}
```

#### 2. **Event Flow Graph**

```
Event propagation as a directed graph:

Client → APIGateway → BookingService → RabbitMQ → PaymentService
                                  ↓                      ↓
                            PostgreSQL              MongoDB
                                  ↑                      ↓
                          BookingService ← RabbitMQ ← (event)
```

#### 3. **Load Balancer Routing**

```csharp
// Weighted graph for load balancing
public class LoadBalancer
{
    // Edge weights = response time
    private readonly Dictionary<string, List<(string Instance, int Weight)>> _instances = new()
    {
        ["BookingService"] = new List<(string, int)>
        {
            ("booking-1", 10),  // 10ms response time
            ("booking-2", 15),  // 15ms response time
            ("booking-3", 20)   // 20ms response time
        }
    };
    
    public string GetBestInstance(string service)
    {
        // Choose instance with lowest weight (fastest response)
        return _instances[service]
            .OrderBy(x => x.Weight)
            .First()
            .Instance;
    }
}
```

---

## 🔢 Complexity Analysis in Practice

### Your BookingService Endpoints

```csharp
public class BookingService
{
    // O(1) - Hash table lookup
    public async Task<Booking> GetByIdAsync(Guid id)
    {
        return await _dbContext.Bookings.FindAsync(id);
        // Database index → hash table → O(1)
    }
    
    // O(n) - Full table scan
    public async Task<List<Booking>> GetAllAsync()
    {
        return await _dbContext.Bookings.ToListAsync();
        // Must read all n bookings
    }
    
    // O(n log n) - Sort operation
    public async Task<List<Booking>> GetSortedByDateAsync()
    {
        return await _dbContext.Bookings
            .OrderBy(b => b.CreatedAt)  // ← O(n log n)
            .ToListAsync();
    }
    
    // O(log n) - Binary search with index
    public async Task<List<Booking>> GetByUserAsync(Guid userId)
    {
        return await _dbContext.Bookings
            .Where(b => b.UserId == userId)  // ← Index lookup
            .ToListAsync();
    }
}
```

### Performance Impact

**Scenario**: 1 million bookings in database

| Operation | Complexity | Time (approx) |
|-----------|-----------|---------------|
| Find by ID | O(1) | 1ms |
| Get all bookings | O(n) | 1000ms |
| Get sorted | O(n log n) | 20,000ms |
| Find by user (indexed) | O(log n) | 20ms |

**Optimization**:
```sql
-- Add index (creates B-tree)
CREATE INDEX idx_bookings_userid ON bookings(user_id);

-- Changes complexity: O(n) → O(log n)
-- Speedup: 1000ms → 20ms (50x faster!)
```

---

## 🎯 Key Takeaways

### When to Use Each Data Structure

| Data Structure | Use When | Example in Your Project |
|---------------|----------|------------------------|
| **Hash Table** | Need O(1) lookup | Cache, routing table, service registry |
| **Queue** | FIFO processing | RabbitMQ, background jobs, rate limiting |
| **Tree** | Hierarchical data | Service dependencies, JSON, routing |
| **Heap** | Priority/scheduling | Task scheduler, priority queue |
| **Graph** | Network/relationships | Service dependencies, event flow |

### Performance Rules of Thumb

1. **Always use indexes** on database columns you query frequently
2. **Cache frequently accessed data** in hash table (Redis)
3. **Use queues** to buffer load spikes
4. **Tree structures** for hierarchical data (don't use arrays)
5. **Graph algorithms** to analyze service dependencies

### Common Mistakes

❌ **Linear search** when you need hash table  
❌ **No database indexes** on foreign keys  
❌ **Synchronous processing** when you need queue  
❌ **Circular dependencies** in service graph  
❌ **Wrong data structure** for the access pattern

---

## 📚 Further Study

### Books
- "Introduction to Algorithms" (CLRS) - Chapters on hash tables, trees, graphs
- "Algorithms" by Sedgewick - Practical implementations

### Practice
- **LeetCode**: Hash table, tree, graph problems
- **System Design**: Design cache, message queue, load balancer

### Related Docs
- [Algorithms in Practice](./algorithms-in-practice.md)
- [Database Indexing](../06-data-management/postgresql-ef-core.md)
- [Caching Strategies](../02-communication/caching-patterns.md)

---

**Last Updated**: November 7, 2025  
**Next**: [Algorithms in Practice](./algorithms-in-practice.md)
