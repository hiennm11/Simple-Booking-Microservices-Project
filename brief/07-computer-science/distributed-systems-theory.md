# 🌍 Distributed Systems Theory for Microservices

**Category**: Computer Science Foundations  
**Difficulty**: Advanced  
**Focus**: Theory behind distributed systems and how it applies to your project

---

## 📖 Overview

Microservices are distributed systems. Understanding fundamental theorems and patterns helps you design resilient, scalable architectures and debug complex issues.

---

## 🎯 CAP Theorem

### The Theorem

**In a distributed system, you can have at most 2 of 3**:

1. **Consistency (C)**: All nodes see the same data at the same time
2. **Availability (A)**: Every request gets a response (success or failure)
3. **Partition Tolerance (P)**: System continues despite network failures

```text
      Consistency
          ┌───┐
          │ C │
          └───┘
         /     \
        /       \
   ┌───┐       ┌───┐
   │ A │───────│ P │
   └───┘       └───┘
 Availability  Partition
               Tolerance

Pick 2:
- CA: Traditional databases (single node)
- CP: MongoDB, HBase (consistency > availability)
- AP: Cassandra, DynamoDB (availability > consistency)
```

### Real-World Trade-offs

**Network partitions WILL happen**, so you must choose between C and A.

#### CP Systems (Consistency + Partition Tolerance)

**Behavior**: During partition, system becomes unavailable to maintain consistency.

**Example**: Banking system

```csharp
// MongoDB with majority write concern (CP)
public async Task TransferMoneyAsync(string fromAccount, string toAccount, decimal amount)
{
    using var session = await _mongoClient.StartSessionAsync();
    session.StartTransaction();
    
    try
    {
        // Both writes must succeed on majority of nodes
        await _accounts.UpdateOneAsync(
            session,
            a => a.Id == fromAccount,
            Builders<Account>.Update.Inc(a => a.Balance, -amount),
            new UpdateOptions { WriteConcern = WriteConcern.WMajority }
        );
        
        await _accounts.UpdateOneAsync(
            session,
            a => a.Id == toAccount,
            Builders<Account>.Update.Inc(a => a.Balance, amount),
            new UpdateOptions { WriteConcern = WriteConcern.WMajority }
        );
        
        await session.CommitTransactionAsync();
    }
    catch (MongoException)
    {
        await session.AbortTransactionAsync();
        throw; // Fail request to maintain consistency
    }
}
```

**Trade-off**: If network partition occurs, request fails (unavailable) rather than risk inconsistent balances.

#### AP Systems (Availability + Partition Tolerance)

**Behavior**: During partition, system remains available but may return stale data.

**Example**: Social media feed

```csharp
// Cassandra with eventual consistency (AP)
public async Task<List<Post>> GetUserFeedAsync(string userId)
{
    // Read from any available node, may be stale
    var posts = await _cassandraSession
        .ExecuteAsync(new SimpleStatement(
            "SELECT * FROM posts WHERE user_id = ? LIMIT 50",
            userId
        ).SetConsistencyLevel(ConsistencyLevel.One));
    
    return posts.Select(row => new Post
    {
        Id = row.GetValue<Guid>("id"),
        Content = row.GetValue<string>("content"),
        Timestamp = row.GetValue<DateTimeOffset>("timestamp")
    }).ToList();
    
    // May show slightly old posts, but always responds
}
```

**Trade-off**: During partition, different users may see different feeds temporarily. Better than "Service Unavailable" error.

### Your Project: CP or AP?

**PostgreSQL (UserService, BookingService)**: **CP**

- Strong consistency within each database
- During network issues, database becomes unavailable
- Correct choice for booking data (can't double-book)

**MongoDB (PaymentService)**: **CP by default**

- Default write concern requires majority acknowledgment
- Can configure for AP if needed

**RabbitMQ**: **AP**

- Messages may be duplicated during network partitions
- Better to process duplicate than lose messages
- Idempotency in consumers handles duplicates

**Overall System**: **AP with eventual consistency**

```text
User creates booking:
1. BookingService writes to PostgreSQL (CP) ✓
2. Publishes BookingCreatedEvent to RabbitMQ (AP) ✓
3. PaymentService processes event (eventual) ⏳

Between steps 2-3: System is temporarily inconsistent
Eventually: Payment processed, system consistent
```

---

## ⏱️ Eventual Consistency

### Definition

**System will become consistent given enough time**, but may be temporarily inconsistent.

### Example in Your Project

**Scenario**: Create booking

```text
Time    BookingService         RabbitMQ            PaymentService
────────────────────────────────────────────────────────────────────
t=0     Create booking
        Status: Pending
        ✓ Saved to DB

t=1                            Publish event
                               BookingCreated
                               
t=2                            ✓ Event in queue

t=3                                                Consume event
                                                   Create payment
                                                   
t=4                                                ✓ Payment saved
                                                   
t=5                                                Publish event
                                                   PaymentSucceeded
                                                   
t=6                            ✓ Event in queue

t=7     Consume event
        Update status
        Status: Confirmed
        
t=8     ✓ Saved to DB

Result: Eventually consistent after ~8 time units
```

### Handling Inconsistencies

#### 1. Read-Your-Writes Consistency

**Problem**: User creates booking, immediately queries it, sees old status.

**Solution**: Return latest state from write operation

```csharp
// ✅ Return booking immediately after creation
public async Task<ActionResult<BookingDto>> CreateBookingAsync(CreateBookingRequest request)
{
    var booking = new Booking
    {
        Id = Guid.NewGuid(),
        UserId = request.UserId,
        Status = BookingStatus.Pending,
        CreatedAt = DateTime.UtcNow
    };
    
    await _dbContext.Bookings.AddAsync(booking);
    await _dbContext.SaveChangesAsync();
    
    // Publish event (eventual)
    await _eventBus.PublishAsync(new BookingCreatedEvent(booking.Id));
    
    // Return immediately with current state
    return CreatedAtAction(
        nameof(GetBooking),
        new { id = booking.Id },
        new BookingDto
        {
            Id = booking.Id,
            Status = "Pending", // User sees consistent state
            CreatedAt = booking.CreatedAt
        }
    );
}
```

#### 2. Idempotent Operations

**Problem**: Event processed twice due to retry → duplicate payment.

**Solution**: Check if already processed

```csharp
// PaymentService
public async Task HandleBookingCreatedAsync(BookingCreatedEvent evt)
{
    // Check if payment already exists (idempotency)
    var existingPayment = await _dbContext.Payments
        .FirstOrDefaultAsync(p => p.BookingId == evt.BookingId);
    
    if (existingPayment != null)
    {
        _logger.LogInformation(
            "Payment for booking {BookingId} already processed, skipping",
            evt.BookingId
        );
        return; // Idempotent: safe to process twice
    }
    
    // Create new payment
    var payment = new Payment
    {
        Id = Guid.NewGuid(),
        BookingId = evt.BookingId,
        Amount = evt.TotalAmount,
        Status = PaymentStatus.Pending
    };
    
    await _dbContext.Payments.AddAsync(payment);
    await _dbContext.SaveChangesAsync();
}
```

#### 3. Versioning

**Problem**: Two services update same data concurrently → lost update.

**Solution**: Optimistic concurrency control

```csharp
public class Booking
{
    public Guid Id { get; set; }
    public string Status { get; set; }
    
    [ConcurrencyCheck] // EF Core: Check version on update
    public int Version { get; set; }
}

public async Task UpdateBookingStatusAsync(Guid id, string newStatus)
{
    var booking = await _dbContext.Bookings.FindAsync(id);
    if (booking == null)
        throw new NotFoundException();
    
    booking.Status = newStatus;
    // Version automatically incremented by EF Core
    
    try
    {
        await _dbContext.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        // Version mismatch: someone else updated first
        throw new ConcurrentUpdateException(
            "Booking was modified by another process"
        );
    }
}
```

---

## 🔄 Two-Phase Commit vs Saga Pattern

### Two-Phase Commit (2PC)

**Distributed transaction protocol** (NOT used in microservices).

**Phases**:

1. **Prepare**: Coordinator asks all services "Can you commit?"
2. **Commit**: If all say yes, coordinator says "Commit now"

```text
Coordinator          Service A         Service B
    │                    │                 │
    ├──Prepare?─────────→│                 │
    │                    ├──Yes─────────┐  │
    ├──Prepare?─────────┼────────────────→ │
    │                    │                 ├──Yes
    │                    │                 │
    ├──Commit──────────→ │                 │
    │                    ├──Done───────┐   │
    ├──Commit──────────┼─────────────────→ │
    │                    │                 ├──Done
```

**Problems**:

- ❌ Blocking: Services locked during prepare phase
- ❌ Single point of failure: Coordinator crash = deadlock
- ❌ Not scalable: Locks don't scale
- ❌ Cross-service transactions violate microservice independence

**Verdict**: Don't use in microservices!

### Saga Pattern

**Sequence of local transactions**, with compensating actions for rollback.

#### Choreography Saga (Your Project)

**No central coordinator**, services react to events.

**Example**: Create booking with payment

```text
1. BookingService: Create booking (status=Pending)
   ↓ Publish BookingCreatedEvent
   
2. PaymentService: Process payment
   ↓ If success: Publish PaymentSucceededEvent
   ↓ If failure: Publish PaymentFailedEvent
   
3. BookingService: Update booking status
   ↓ If PaymentSucceeded: status=Confirmed
   ↓ If PaymentFailed: status=Cancelled (compensate)
```

**Implementation**:

```csharp
// BookingService: Handle payment failure (compensation)
public class PaymentFailedEventHandler : IEventHandler<PaymentFailedEvent>
{
    private readonly BookingDbContext _dbContext;
    
    public async Task HandleAsync(PaymentFailedEvent evt)
    {
        var booking = await _dbContext.Bookings
            .FirstOrDefaultAsync(b => b.Id == evt.BookingId);
        
        if (booking == null)
        {
            _logger.LogWarning("Booking {BookingId} not found", evt.BookingId);
            return;
        }
        
        // Compensating action: Cancel booking
        booking.Status = BookingStatus.Cancelled;
        booking.CancellationReason = "Payment failed";
        booking.CancelledAt = DateTime.UtcNow;
        
        await _dbContext.SaveChangesAsync();
        
        // Could also publish BookingCancelledEvent
        // to notify other services (e.g., refund, email)
    }
}
```

#### Orchestration Saga

**Central coordinator** manages saga steps.

**Example**: Orchestrator service

```csharp
public class BookingOrchestrator
{
    public async Task CreateBookingWithPaymentAsync(CreateBookingRequest request)
    {
        var sagaId = Guid.NewGuid();
        
        try
        {
            // Step 1: Create booking
            var bookingId = await _bookingService.CreateBookingAsync(new
            {
                UserId = request.UserId,
                EventName = request.EventName
            });
            
            // Step 2: Process payment
            var paymentId = await _paymentService.ProcessPaymentAsync(new
            {
                BookingId = bookingId,
                Amount = request.TotalAmount
            });
            
            // Step 3: Confirm booking
            await _bookingService.ConfirmBookingAsync(bookingId);
            
            return bookingId;
        }
        catch (PaymentFailedException)
        {
            // Compensate: Cancel booking
            await _bookingService.CancelBookingAsync(bookingId);
            throw;
        }
        catch (Exception ex)
        {
            // Log saga failure
            _logger.LogError(ex, "Saga {SagaId} failed", sagaId);
            throw;
        }
    }
}
```

### Comparison

| Aspect | Choreography (Your Project) | Orchestration |
|--------|----------------------------|---------------|
| **Complexity** | Distributed across services | Centralized |
| **Coupling** | Loose (event-driven) | Tight (orchestrator calls services) |
| **Debugging** | Harder (trace events) | Easier (single flow) |
| **Scalability** | High | Orchestrator can bottleneck |
| **Use Case** | Simple flows, few services | Complex flows, many services |

---

## 🔐 Distributed Transactions Problem

### The Dual-Write Problem

**Problem**: Write to database + publish event atomically.

```csharp
// ❌ Not atomic: Database succeeds, event publish fails
public async Task CreateBookingAsync(CreateBookingRequest request)
{
    var booking = new Booking { /* ... */ };
    
    // Write 1: Database
    await _dbContext.Bookings.AddAsync(booking);
    await _dbContext.SaveChangesAsync();
    
    // Write 2: Event bus
    // What if this fails? ← DATABASE INCONSISTENT WITH EVENTS
    await _eventBus.PublishAsync(new BookingCreatedEvent(booking.Id));
}
```

**Consequence**: Booking created in database but PaymentService never processes it.

### Solution 1: Transactional Outbox Pattern (Your Project)

**Store event in database within same transaction**, then publish asynchronously.

```csharp
// ✅ Atomic: Both written to database in single transaction
public async Task CreateBookingAsync(CreateBookingRequest request)
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync();
    
    try
    {
        // Write 1: Booking
        var booking = new Booking { /* ... */ };
        await _dbContext.Bookings.AddAsync(booking);
        
        // Write 2: Outbox (same transaction!)
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "BookingCreated",
            Payload = JsonSerializer.Serialize(new BookingCreatedEvent(booking.Id)),
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null
        };
        await _dbContext.OutboxMessages.AddAsync(outboxMessage);
        
        // Both committed atomically
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}

// Background service publishes from outbox
public class OutboxPublisher : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var unprocessedMessages = await _dbContext.OutboxMessages
                .Where(m => m.ProcessedAt == null)
                .OrderBy(m => m.CreatedAt)
                .Take(10)
                .ToListAsync();
            
            foreach (var message in unprocessedMessages)
            {
                try
                {
                    // Publish to event bus
                    await _eventBus.PublishRawAsync(
                        message.EventType,
                        message.Payload
                    );
                    
                    // Mark as processed
                    message.ProcessedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish outbox message {MessageId}", message.Id);
                    // Will retry in next iteration
                }
            }
            
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

**Guarantees**:

- ✅ Database and outbox always consistent (single transaction)
- ✅ At-least-once delivery (retry until published)
- ✅ Survives crashes (persistent storage)

**Document**: `/docs/phase6-advanced/OUTBOX_PATTERN_IMPLEMENTATION.md`

### Solution 2: Event Sourcing

**Store events as source of truth**, derive state from events.

```csharp
// Instead of storing current state
public class Booking
{
    public Guid Id { get; set; }
    public string Status { get; set; } // Current state
}

// Store all events
public class BookingEvents
{
    public List<DomainEvent> Events { get; set; }
}

public abstract class DomainEvent
{
    public Guid AggregateId { get; set; }
    public DateTime Timestamp { get; set; }
}

public class BookingCreatedEvent : DomainEvent
{
    public string UserId { get; set; }
    public string EventName { get; set; }
}

public class BookingConfirmedEvent : DomainEvent
{
    public string PaymentId { get; set; }
}

// Reconstruct state from events
public class Booking
{
    private readonly List<DomainEvent> _events = new();
    
    public Guid Id { get; private set; }
    public string Status { get; private set; }
    
    public void Apply(BookingCreatedEvent evt)
    {
        Id = evt.AggregateId;
        Status = "Pending";
        _events.Add(evt);
    }
    
    public void Apply(BookingConfirmedEvent evt)
    {
        Status = "Confirmed";
        _events.Add(evt);
    }
    
    public static Booking FromEvents(IEnumerable<DomainEvent> events)
    {
        var booking = new Booking();
        foreach (var evt in events)
        {
            switch (evt)
            {
                case BookingCreatedEvent e:
                    booking.Apply(e);
                    break;
                case BookingConfirmedEvent e:
                    booking.Apply(e);
                    break;
            }
        }
        return booking;
    }
}
```

**Benefits**:

- ✅ Complete audit trail (all events stored)
- ✅ Time travel (reconstruct state at any point)
- ✅ No dual-write problem (events ARE the data)

**Drawbacks**:

- ❌ Complex queries (need projections)
- ❌ Schema evolution difficult
- ❌ Learning curve

---

## 🕰️ Time and Ordering

### Clocks in Distributed Systems

**Problem**: Each server has its own clock, may drift.

```text
Server A clock: 10:00:00.000
Server B clock: 09:59:58.500 (1.5 seconds behind)

Event 1 on A: timestamp = 10:00:00.000
Event 2 on B: timestamp = 09:59:59.000

Event 2 appears to happen before Event 1, but actually happened after!
```

### Logical Clocks (Lamport Timestamps)

**Algorithm**: Each event gets logical timestamp, ensuring causality.

```text
Rules:
1. Each process maintains counter
2. Increment counter before each event
3. Send counter with messages
4. On receive: counter = max(local, received) + 1
```

**Example**:

```text
Process A:  Event(1) ──Send(2)────→ Event(4)
                                         ↓
                                    Receive(5)

Process B:  Event(1) ──────────────→ Event(2)
                ↓                        ↑
            Receive(3)──────────────Send(3)

Lamport timestamps show causality:
- Send(2) happened before Receive(3)
- Send(3) happened before Receive(5)
```

**Implementation**:

```csharp
public class LamportClock
{
    private long _counter = 0;
    private readonly object _lock = new();
    
    public long Tick()
    {
        lock (_lock)
        {
            return ++_counter;
        }
    }
    
    public long Update(long receivedTimestamp)
    {
        lock (_lock)
        {
            _counter = Math.Max(_counter, receivedTimestamp) + 1;
            return _counter;
        }
    }
}

// Usage in events
public class BookingCreatedEvent
{
    public Guid BookingId { get; set; }
    public long LamportTimestamp { get; set; } // Logical time
    public DateTime PhysicalTimestamp { get; set; } // Wall clock time
}

public async Task PublishEventAsync(BookingCreatedEvent evt)
{
    evt.LamportTimestamp = _lamportClock.Tick();
    evt.PhysicalTimestamp = DateTime.UtcNow;
    
    await _eventBus.PublishAsync(evt);
}
```

### Vector Clocks

**More precise**: Can detect concurrent events (Lamport can't).

```csharp
public class VectorClock
{
    private readonly Dictionary<string, long> _clocks;
    private readonly string _nodeId;
    
    public VectorClock(string nodeId, List<string> allNodeIds)
    {
        _nodeId = nodeId;
        _clocks = allNodeIds.ToDictionary(id => id, _ => 0L);
    }
    
    public void Tick()
    {
        _clocks[_nodeId]++;
    }
    
    public void Update(Dictionary<string, long> receivedClock)
    {
        foreach (var (nodeId, timestamp) in receivedClock)
        {
            _clocks[nodeId] = Math.Max(_clocks[nodeId], timestamp);
        }
        Tick(); // Increment own counter
    }
    
    public Dictionary<string, long> GetClock()
    {
        return new Dictionary<string, long>(_clocks);
    }
}
```

---

## 🔀 Consensus Algorithms

### Problem

Multiple nodes must agree on a value (e.g., who is leader, what is current state).

### Raft Algorithm

**Leader-based consensus** (used by etcd, Consul).

**Roles**:

- **Leader**: Handles all client requests
- **Follower**: Replicates leader's log
- **Candidate**: Requests votes to become leader

**Normal Operation**:

```text
Client → Leader: Write request
         Leader → Followers: Replicate to log
         Followers → Leader: Acknowledge
         Leader → Client: Success (after majority ack)
```

**Leader Failure**:

```text
1. Follower timeout (no heartbeat from leader)
2. Follower becomes Candidate
3. Candidate requests votes from all nodes
4. If majority vote yes: Candidate becomes Leader
5. New Leader resumes operations
```

**Log Replication**:

```text
Leader Log:  [Entry1, Entry2, Entry3]
              ↓       ↓       ↓
Follower1:   [Entry1, Entry2, Entry3] ✓
Follower2:   [Entry1, Entry2, Entry3] ✓
Follower3:   [Entry1, Entry2]         ← Lagging, will catch up

Commit when majority (2 of 3) have entry
```

### Paxos Algorithm

**Older, more complex** consensus algorithm.

**Phases**:

1. **Prepare**: Proposer sends proposal number
2. **Promise**: Acceptors promise not to accept older proposals
3. **Accept**: Proposer sends value
4. **Accepted**: Acceptors accept if no newer proposal

**Used by**: Google Chubby, Apache Cassandra

### Your Project

**No consensus algorithm** (single instance services).

**When you'd need consensus**:

- Multi-instance BookingService with shared state
- Leader election for background job processing
- Distributed configuration management

**Tools that use consensus**:

- **Consul**: Service discovery with Raft consensus
- **etcd**: Kubernetes uses for cluster state (Raft)
- **ZooKeeper**: Distributed coordination (ZAB, similar to Paxos)

---

## 💾 Replication Strategies

### Master-Slave (Primary-Replica)

**Write to master**, read from replicas.

```text
     ┌────────┐
     │ Master │ ← All writes
     └────────┘
      ↓  ↓  ↓
   ┌───┬───┬───┐
   │ R │ R │ R │ ← Read-only replicas
   └───┴───┴───┘
```

**PostgreSQL Streaming Replication**:

```sql
-- Master configuration
wal_level = replica
max_wal_senders = 5
wal_keep_segments = 32

-- Replica configuration
hot_standby = on
```

**Pros**:

- ✅ Read scalability (add more replicas)
- ✅ Simple (clear write authority)

**Cons**:

- ❌ Write bottleneck (single master)
- ❌ Replication lag (replicas may be stale)

### Multi-Master (Master-Master)

**Write to any master**, conflict resolution needed.

```text
  ┌────────┐ ↔ ┌────────┐
  │Master1 │   │Master2 │
  └────────┘   └────────┘
      ↕            ↕
  ┌────────┐ ↔ ┌────────┐
  │Master3 │   │Master4 │
  └────────┘   └────────┘
```

**Conflict Resolution**:

- **Last-Write-Wins (LWW)**: Use timestamp (may lose data)
- **Version Vectors**: Detect conflicts, require manual resolution
- **CRDTs**: Conflict-free data types (e.g., counter, set)

**Used by**: Cassandra, DynamoDB, CouchDB

### Your Project: Single Instance per Service

```yaml
services:
  bookingservice:
    # Single instance, no replication
    image: bookingservice:latest
```

**To add replication**:

```yaml
services:
  bookingservice:
    # Multiple instances
    deploy:
      replicas: 3
  
  bookingdb:
    # PostgreSQL with streaming replication
    image: postgres:15
    environment:
      - POSTGRES_REPLICATION_MODE=master
```

---

## 🎓 Key Takeaways

### Distributed Systems Challenges in Your Project

| Challenge | Your Solution | Theory |
|-----------|---------------|--------|
| **Dual-write problem** | Transactional Outbox | Atomic writes to DB + event store |
| **Service coordination** | Choreography Saga | Event-driven, no coordinator |
| **Eventual consistency** | Idempotent consumers | Accept temporary inconsistency |
| **Network failures** | Retry with backoff, timeouts | Assume network is unreliable |
| **Concurrent updates** | Database transactions | Pessimistic/optimistic locking |

### Design Principles

1. **Embrace Eventual Consistency**: Most microservices don't need strong consistency
2. **Plan for Failures**: Network will fail, services will crash
3. **Idempotency**: Operations must be safe to retry
4. **Observability**: Log events with correlation IDs to trace flows
5. **Compensating Actions**: Saga pattern for distributed transactions

### When to Use What

| Pattern | Use When | Avoid When |
|---------|----------|------------|
| **Outbox Pattern** | Need guaranteed event delivery | Real-time requirements (adds latency) |
| **Event Sourcing** | Need audit trail, time travel | Simple CRUD, complex queries |
| **Saga** | Multi-service transactions | Strong consistency required |
| **Eventual Consistency** | Scale > consistency | Financial transactions |
| **Strong Consistency** | Correctness critical | High throughput needed |

---

## 📚 Further Study

### Books

- **Designing Data-Intensive Applications** by Martin Kleppmann (must-read!)
- **Distributed Systems** by Maarten van Steen & Andrew Tanenbaum

### Papers

- **CAP Theorem**: "Brewer's Conjecture and the Feasibility of Consistent, Available, Partition-Tolerant Web Services"
- **Raft Consensus**: "In Search of an Understandable Consensus Algorithm"
- **Lamport Clocks**: "Time, Clocks, and the Ordering of Events in a Distributed System"

### Related Documents

- [Outbox Pattern Implementation](/docs/phase6-advanced/OUTBOX_PATTERN_IMPLEMENTATION.md)
- [Event-Driven Architecture](../01-architecture-patterns/event-driven-architecture.md)
- [Networking Fundamentals](./networking-fundamentals.md)

---

**Last Updated**: November 7, 2025  
**Next**: [Concurrency & Async/Await](./concurrency-async-await.md)
