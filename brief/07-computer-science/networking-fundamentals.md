# 🌐 Networking Fundamentals for Microservices

**Category**: Computer Science Foundations  
**Difficulty**: Intermediate  
**Focus**: How services actually communicate over network

---

## 📖 Overview

Microservices communicate over networks. Understanding protocols, ports, and network layers helps debug issues and optimize performance.

---

## 🔌 OSI Model & TCP/IP Stack

### The 7 Layers (OSI Model)

```text
┌──────────────────────────────────────────┐
│ Layer 7: Application (HTTP, gRPC)       │ ← Your microservices here
├──────────────────────────────────────────┤
│ Layer 6: Presentation (JSON, encryption)│
├──────────────────────────────────────────┤
│ Layer 5: Session (Connections)          │
├──────────────────────────────────────────┤
│ Layer 4: Transport (TCP, UDP)           │ ← Ports, reliability
├──────────────────────────────────────────┤
│ Layer 3: Network (IP)                   │ ← Routing between hosts
├──────────────────────────────────────────┤
│ Layer 2: Data Link (Ethernet)           │
├──────────────────────────────────────────┤
│ Layer 1: Physical (Cables, WiFi)        │
└──────────────────────────────────────────┘
```

### TCP/IP Stack (What Actually Matters)

```text
Application Layer:  HTTP/HTTPS, AMQP (RabbitMQ), PostgreSQL Protocol
                    ↓
Transport Layer:    TCP (reliable) or UDP (fast but unreliable)
                    ↓
Internet Layer:     IP addresses (routing packets)
                    ↓
Network Access:     Ethernet, WiFi
```

---

## 🌍 HTTP/HTTPS - The Language of Microservices

### HTTP Request Structure

```http
POST /api/bookings HTTP/1.1
Host: localhost:5002
Content-Type: application/json
Authorization: Bearer eyJhbGc...
Content-Length: 87

{
  "userId": "123",
  "eventName": "Concert",
  "ticketCount": 2
}
```

**Parts**:

1. **Request Line**: `POST /api/bookings HTTP/1.1`
   - Method: POST (create resource)
   - Path: /api/bookings
   - Protocol: HTTP/1.1

2. **Headers**: Key-value metadata
   - `Host`: Which service
   - `Content-Type`: Data format (JSON)
   - `Authorization`: Security token
   - `Content-Length`: Body size in bytes

3. **Body**: Actual data (JSON)

### HTTP Response Structure

```http
HTTP/1.1 201 Created
Content-Type: application/json
Location: /api/bookings/abc-123
Date: Thu, 07 Nov 2025 10:30:00 GMT

{
  "id": "abc-123",
  "status": "Confirmed",
  "createdAt": "2025-11-07T10:30:00Z"
}
```

**Parts**:

1. **Status Line**: `HTTP/1.1 201 Created`
   - Protocol: HTTP/1.1
   - Status Code: 201 (Created)
   - Reason: Created

2. **Headers**: Response metadata

3. **Body**: Response data

### HTTP Methods in Your Project

| Method | Purpose | Idempotent? | Example in Your Project |
|--------|---------|-------------|-------------------------|
| GET | Retrieve data | Yes | `GET /api/bookings/{id}` |
| POST | Create resource | No | `POST /api/bookings` |
| PUT | Update (replace) | Yes | `PUT /api/users/{id}` |
| PATCH | Update (partial) | No | `PATCH /api/bookings/{id}/status` |
| DELETE | Remove resource | Yes | `DELETE /api/bookings/{id}` |

**Idempotent**: Same request multiple times = same result (safe to retry)

### Status Codes You Use

**2xx Success**:

- `200 OK`: Request succeeded (GET, PUT)
- `201 Created`: Resource created (POST)
- `204 No Content`: Success but no data returned (DELETE)

**4xx Client Error**:

- `400 Bad Request`: Invalid data
- `401 Unauthorized`: No authentication token
- `403 Forbidden`: Token valid but insufficient permissions
- `404 Not Found`: Resource doesn't exist
- `429 Too Many Requests`: Rate limit exceeded

**5xx Server Error**:

- `500 Internal Server Error`: Unexpected server error
- `503 Service Unavailable`: Service down or overloaded

### HTTP/1.1 vs HTTP/2 vs HTTP/3

| Feature | HTTP/1.1 | HTTP/2 | HTTP/3 |
|---------|----------|--------|--------|
| **Transport** | TCP | TCP | UDP (QUIC) |
| **Connections** | 6 per domain | 1 multiplexed | 1 multiplexed |
| **Headers** | Text, repeated | Binary, compressed | Binary, compressed |
| **Performance** | Baseline | 30-50% faster | 50-70% faster |
| **Head-of-line blocking** | Yes | Partially | No |

**Your project**: Uses HTTP/1.1 (ASP.NET Core default)

**To enable HTTP/2**:

```csharp
// Program.cs
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5001, o => o.Protocols = 
        HttpProtocols.Http1AndHttp2);
});
```

---

## 🔐 HTTPS - Secure Communication

### How TLS/SSL Works

```text
1. Client Hello → Server
   "I support these encryption methods"

2. Server Hello ← Server
   "Let's use AES-256. Here's my certificate"

3. Certificate Verification
   Client: "Is this certificate signed by trusted CA?"

4. Key Exchange
   Client generates symmetric key, encrypts with server's public key

5. Encrypted Communication
   Both use symmetric key for fast encryption/decryption
```

### HTTPS in Your Project

**Development** (Self-signed certificate):

```bash
# ASP.NET Core generates dev certificate
dotnet dev-certs https --trust
```

**docker-compose.yml**:

```yaml
services:
  apigateway:
    ports:
      - "5000:8080"    # HTTP
      - "5001:8081"    # HTTPS
    environment:
      - ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx
      - ASPNETCORE_Kestrel__Certificates__Default__Password=YourPassword
```

**Production** (Let's Encrypt):

```bash
# Get free SSL certificate
certbot certonly --standalone -d yourdomain.com
```

### Performance Impact

**HTTP vs HTTPS**:

- HTTPS adds ~50-100ms for TLS handshake (first request)
- Subsequent requests: Minimal overhead (~1-5ms)
- Trade-off: Security worth the cost

**Optimization**: Enable HTTP/2 with HTTPS

- HTTP/2 requires HTTPS
- Multiplexing + header compression = faster than HTTP/1.1

---

## 🔢 Ports - How Services Are Addressed

### Port Basics

```text
IP Address:Port
localhost:5001 = 127.0.0.1:5001

IP: Which computer
Port: Which service on that computer
```

### Well-Known Ports (0-1023)

| Port | Protocol | Use |
|------|----------|-----|
| 20-21 | FTP | File transfer |
| 22 | SSH | Secure shell |
| 25 | SMTP | Email |
| 53 | DNS | Domain name lookup |
| 80 | HTTP | Web traffic |
| 443 | HTTPS | Secure web traffic |

### Your Project Ports (5000-27017)

```yaml
# docker-compose.yml
services:
  apigateway:
    ports:
      - "5000:8080"    # HTTP
      - "5001:8081"    # HTTPS
  
  userservice:
    ports:
      - "5002:8080"    # Internal HTTP
  
  bookingservice:
    ports:
      - "5003:8080"
  
  paymentservice:
    ports:
      - "5004:8080"
  
  rabbitmq:
    ports:
      - "5672:5672"    # AMQP protocol
      - "15672:15672"  # Management UI
  
  userdb:
    ports:
      - "5432:5432"    # PostgreSQL
  
  bookingdb:
    ports:
      - "5433:5432"    # PostgreSQL (different host port!)
  
  paymentdb:
    ports:
      - "27017:27017"  # MongoDB
  
  seq:
    ports:
      - "5341:80"      # Seq UI
```

**Port Mapping**: `HOST:CONTAINER`

- `5000:8080`: Host port 5000 → Container port 8080
- Why different? Avoid conflicts on host machine

### Port Conflicts

**Problem**:

```bash
# Try to start service on port 5000
Error: Address already in use (port 5000)
```

**Solution**:

```bash
# Windows: Find process using port
netstat -ano | findstr :5000
# Shows: TCP 0.0.0.0:5000 ... LISTENING 12345

# Kill process
taskkill /PID 12345 /F
```

---

## 🚀 TCP vs UDP

### TCP (Transmission Control Protocol)

**Characteristics**:

- ✅ Reliable: Guarantees delivery, retransmits lost packets
- ✅ Ordered: Packets arrive in order sent
- ✅ Connection-oriented: Handshake before data transfer
- ❌ Slower: Overhead from acknowledgments

**Use Cases**: HTTP, HTTPS, RabbitMQ, PostgreSQL, MongoDB

**TCP Handshake (3-way)**:

```text
Client                    Server
   │                         │
   ├──── SYN ──────────────→ │ "Let's connect"
   │                         │
   │ ←─── SYN-ACK ──────────┤ "OK, let's connect"
   │                         │
   ├──── ACK ──────────────→ │ "Connection established"
   │                         │
   ├──── Data ─────────────→ │
   │ ←─── ACK ──────────────┤ "Got it"
```

### UDP (User Datagram Protocol)

**Characteristics**:

- ❌ Unreliable: May lose packets (no retransmission)
- ❌ Unordered: Packets may arrive out of order
- ✅ Fast: No handshake, no acknowledgments
- ✅ Low latency: Ideal for real-time

**Use Cases**: Video streaming, online gaming, DNS lookups

**UDP Communication**:

```text
Client                    Server
   │                         │
   ├──── Data ─────────────→ │ "Here's packet 1"
   ├──── Data ─────────────→ │ "Here's packet 2"
   │                         │
   (No acknowledgment, fire and forget)
```

### When to Use Each

| Scenario | Protocol | Reason |
|----------|----------|--------|
| HTTP API call | TCP | Need reliable delivery |
| Database query | TCP | Can't lose data |
| RabbitMQ message | TCP | Message must arrive |
| Video streaming | UDP | Speed > reliability |
| Online game position updates | UDP | Latest position matters, old packets irrelevant |
| DNS lookup | UDP | Fast, can retry if fails |

### In Your Project

All services use TCP:

- HTTP APIs: TCP port 80/443
- RabbitMQ: TCP port 5672 (AMQP over TCP)
- PostgreSQL: TCP port 5432
- MongoDB: TCP port 27017

**Why not UDP?** Microservices need reliable message delivery. Can't afford lost events or requests.

---

## 🌐 DNS - Service Discovery

### How DNS Works

```text
1. You: "What's the IP of bookingservice?"
2. DNS: "172.18.0.5"
3. You: Connect to 172.18.0.5:8080
```

### DNS in Docker

Docker creates internal DNS for container names:

```yaml
# docker-compose.yml
services:
  bookingservice:
    # Accessible as: http://bookingservice:8080
  
  rabbitmq:
    # Accessible as: amqp://rabbitmq:5672
```

**From APIGateway**:

```json
{
  "ReverseProxy": {
    "Routes": {
      "booking-route": {
        "ClusterId": "booking-cluster",
        "Match": {
          "Path": "/api/bookings/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "booking-cluster": {
        "Destinations": {
          "booking": {
            "Address": "http://bookingservice:8080"
          }
        }
      }
    }
  }
}
```

**Docker DNS Resolution**:

```text
bookingservice → Docker DNS → 172.18.0.5
rabbitmq → Docker DNS → 172.18.0.3
```

### DNS Caching

**Problem**: DNS lookup takes time (~10-100ms)

**Solution**: Cache results

```csharp
public class HttpClientWithDnsCache
{
    private static readonly SocketsHttpHandler _handler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        // DNS cache refreshed every 2 minutes
    };
    
    private static readonly HttpClient _client = new(_handler);
}
```

### Service Discovery Alternatives

| Method | Pros | Cons | Use Case |
|--------|------|------|----------|
| **Docker DNS** | Simple, built-in | Single host only | Development, small deployments |
| **Consul** | Dynamic, health checks | Complex setup | Production microservices |
| **Kubernetes DNS** | Automatic, scalable | Requires K8s | Cloud-native apps |
| **Hardcoded IPs** | Fast | Fragile, unmaintainable | Never use |

---

## 🔄 Connection Pooling

### Problem

Creating TCP connection is expensive:

```text
TCP Handshake: ~100ms
TLS Handshake: +50ms
Total: 150ms per request
```

### Solution: Reuse Connections

```csharp
// ❌ Bad: New connection per request
public async Task<string> BadWayAsync()
{
    using var client = new HttpClient(); // DON'T DO THIS
    return await client.GetStringAsync("http://bookingservice/api/bookings");
    // Connection closed after every request
}

// ✅ Good: Reuse connections
public class GoodWay
{
    private static readonly HttpClient _client = new();
    
    public async Task<string> GoodWayAsync()
    {
        return await _client.GetStringAsync("http://bookingservice/api/bookings");
        // Connection pooled and reused
    }
}
```

### HttpClient in Your Project

**File**: `src/ApiGateway/Program.cs`

```csharp
// HttpClient registered as singleton
builder.Services.AddHttpClient("BookingService", client =>
{
    client.BaseAddress = new Uri("http://bookingservice:8080");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Usage in controller
public class BookingController
{
    private readonly IHttpClientFactory _httpClientFactory;
    
    public BookingController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task<IActionResult> GetBookingAsync(string id)
    {
        var client = _httpClientFactory.CreateClient("BookingService");
        var response = await client.GetAsync($"/api/bookings/{id}");
        // Connection reused from pool
    }
}
```

### Database Connection Pooling

**PostgreSQL Connection String**:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=userdb;Port=5432;Database=userdb;Username=admin;Password=admin123;Pooling=true;MinPoolSize=5;MaxPoolSize=20"
  }
}
```

**What Happens**:

1. First request: Create 5 connections (MinPoolSize)
2. High load: Create up to 20 connections (MaxPoolSize)
3. Request completes: Return connection to pool (don't close)
4. Next request: Reuse existing connection (fast!)

**Performance Impact**:

- Without pooling: 100ms per request (TCP handshake)
- With pooling: 1ms per request (100x faster!)

---

## ⚡ Load Balancing

### Problem

Single service instance can't handle all traffic.

### Solution: Distribute Load

```text
           API Gateway
               │
     ┌─────────┼─────────┐
     │         │         │
  Service   Service   Service
  Instance   Instance   Instance
     1         2         3
```

### Load Balancing Algorithms

#### 1. Round Robin

```csharp
public class RoundRobinLoadBalancer
{
    private readonly List<string> _servers = new()
    {
        "http://booking1:8080",
        "http://booking2:8080",
        "http://booking3:8080"
    };
    
    private int _currentIndex = 0;
    private readonly object _lock = new();
    
    public string GetNextServer()
    {
        lock (_lock)
        {
            var server = _servers[_currentIndex];
            _currentIndex = (_currentIndex + 1) % _servers.Count;
            return server;
        }
    }
}

// Distribution:
// Request 1 → booking1
// Request 2 → booking2
// Request 3 → booking3
// Request 4 → booking1 (wrap around)
```

#### 2. Least Connections

```csharp
public class LeastConnectionsLoadBalancer
{
    private readonly Dictionary<string, int> _activeConnections = new()
    {
        ["http://booking1:8080"] = 0,
        ["http://booking2:8080"] = 0,
        ["http://booking3:8080"] = 0
    };
    
    public string GetNextServer()
    {
        // Pick server with fewest active connections
        var server = _activeConnections
            .OrderBy(kvp => kvp.Value)
            .First()
            .Key;
        
        _activeConnections[server]++;
        return server;
    }
    
    public void ReleaseConnection(string server)
    {
        _activeConnections[server]--;
    }
}

// Dynamic: Adapts to server load
```

#### 3. Weighted Round Robin

```csharp
public class WeightedRoundRobinLoadBalancer
{
    private readonly List<(string Server, int Weight)> _servers = new()
    {
        ("http://booking1:8080", 5), // 50% traffic (powerful server)
        ("http://booking2:8080", 3), // 30% traffic
        ("http://booking3:8080", 2)  // 20% traffic (weak server)
    };
    
    // Implementation: Repeat servers by weight
    private List<string> _weightedList;
    
    public WeightedRoundRobinLoadBalancer()
    {
        _weightedList = new();
        foreach (var (server, weight) in _servers)
        {
            for (int i = 0; i < weight; i++)
            {
                _weightedList.Add(server);
            }
        }
        // Result: [booking1, booking1, booking1, booking1, booking1,
        //          booking2, booking2, booking2,
        //          booking3, booking3]
    }
}
```

### YARP Load Balancing (Your Project)

**File**: `src/ApiGateway/appsettings.json`

```json
{
  "ReverseProxy": {
    "Clusters": {
      "booking-cluster": {
        "LoadBalancingPolicy": "RoundRobin",
        "Destinations": {
          "booking1": {
            "Address": "http://bookingservice1:8080"
          },
          "booking2": {
            "Address": "http://bookingservice2:8080"
          }
        }
      }
    }
  }
}
```

**Available Policies**:

- `RoundRobin`: Default, even distribution
- `LeastRequests`: Route to server with fewest active requests
- `Random`: Random selection
- `PowerOfTwoChoices`: Pick 2 random, choose least loaded

---

## 🔍 Network Debugging

### 1. Check if Service is Running

```bash
# Windows
netstat -ano | findstr :5002
# Output: TCP 0.0.0.0:5002 ... LISTENING

# Docker
docker ps
# Shows running containers and ports
```

### 2. Test Connection

```bash
# Windows
curl http://localhost:5002/api/health
# Or PowerShell
Invoke-WebRequest -Uri http://localhost:5002/api/health

# Test from another container
docker exec -it apigateway curl http://bookingservice:8080/api/health
```

### 3. Inspect Network Traffic

**tcpdump** (Linux):

```bash
# Capture traffic on port 5002
tcpdump -i any port 5002 -A

# Output shows HTTP requests/responses
```

**Wireshark** (Windows):

- GUI tool to capture and analyze packets
- Filter by port: `tcp.port == 5002`

### 4. Check DNS Resolution

```bash
# Inside container
docker exec -it apigateway nslookup bookingservice
# Should return IP address
```

### 5. Common Network Errors

| Error | Meaning | Solution |
|-------|---------|----------|
| `Connection refused` | Service not running or wrong port | Check service is up, verify port |
| `Timeout` | Service slow or unreachable | Check network, firewalls, increase timeout |
| `Connection reset` | Server closed connection | Check server logs, may be crashing |
| `Host not found` | DNS resolution failed | Check service name in docker-compose.yml |
| `SSL certificate error` | Certificate invalid or self-signed | Trust certificate or disable validation (dev only) |

---

## 📊 Network Performance Metrics

### Latency

**Definition**: Time for request to travel from client to server and back.

**Measurement**:

```csharp
var stopwatch = Stopwatch.StartNew();
var response = await _httpClient.GetAsync("http://bookingservice/api/bookings");
stopwatch.Stop();

_logger.LogInformation(
    "Request to BookingService took {Latency}ms",
    stopwatch.ElapsedMilliseconds
);
```

**Typical Values**:

- Same datacenter: 1-10ms
- Different datacenter (same region): 10-50ms
- Cross-region: 50-200ms
- Cross-continent: 200-500ms

### Throughput

**Definition**: Requests per second (RPS) or data transfer rate (MB/s).

**Measurement**:

```bash
# Load test with curl
# 100 requests, 10 concurrent
for i in {1..100}; do
  curl -s http://localhost:5000/api/bookings &
done
wait

# Calculate RPS
# 100 requests in 5 seconds = 20 RPS
```

### Bandwidth

**Definition**: Maximum data transfer rate of network link.

**Your Docker Network**:

- Internal network: ~10 Gbps (virtual, very fast)
- External network: Depends on host (100 Mbps - 1 Gbps typical)

### Connection Limits

**File Descriptors** (Linux):

- Each connection = 1 file descriptor
- Default limit: 1024
- High-traffic service needs: 10,000+

**Increase limit**:

```bash
# Linux
ulimit -n 65535
```

**ASP.NET Core Kestrel**:

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 10000;
    options.Limits.MaxConcurrentUpgradedConnections = 10000;
});
```

---

## 🎓 Key Takeaways

### Networking in Your Microservices

| Component | Protocol | Port | Purpose |
|-----------|----------|------|---------|
| APIGateway → Services | HTTP/1.1 over TCP | 8080 (internal) | API calls |
| Services → RabbitMQ | AMQP over TCP | 5672 | Event messaging |
| Services → PostgreSQL | PostgreSQL protocol over TCP | 5432/5433 | Database queries |
| Services → MongoDB | MongoDB protocol over TCP | 27017 | Document storage |
| Browser → APIGateway | HTTPS over TCP | 5001 | User requests |

### Performance Optimizations

1. **HTTP/2**: Multiplexing, header compression
2. **Connection Pooling**: Reuse TCP connections (HttpClient, database)
3. **DNS Caching**: Reduce lookup time
4. **Load Balancing**: Distribute load across instances
5. **Keep-Alive**: Reuse HTTP connections

### Debugging Checklist

- [ ] Service running? (`docker ps`)
- [ ] Port correct? (`netstat` or docker-compose.yml)
- [ ] DNS resolving? (`nslookup` inside container)
- [ ] Firewall blocking? (Test with `curl`)
- [ ] Timeout too short? (Increase in HttpClient)
- [ ] Connection pool exhausted? (Check max pool size)

---

## 📚 Further Study

### Practice
- Wireshark: Capture and analyze HTTP traffic
- Postman: Test API endpoints with different headers
- Docker networking: Create custom networks, inspect with `docker network inspect`

### Related Documents
- [Distributed Systems Theory](./distributed-systems-theory.md)
- [Algorithms in Practice](./algorithms-in-practice.md)
- [API Gateway Implementation](/docs/phase4-gateway-security/APIGATEWAY_IMPLEMENTATION.md)

---

**Last Updated**: November 7, 2025  
**Next**: [Distributed Systems Theory](./distributed-systems-theory.md)
