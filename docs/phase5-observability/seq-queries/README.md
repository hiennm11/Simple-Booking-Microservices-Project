# Seq Query Library

> **🔔 Seq 2025.2 Users**: UI navigation has changed in the latest version. See [SEQ_2025_QUICK_REFERENCE.md](../SEQ_2025_QUICK_REFERENCE.md) for updated instructions.

This directory contains production-ready Seq queries, alerts, and dashboard configurations for monitoring the Simple Booking Microservices Project.

## 📁 Files

### 1. retry-monitoring.sql
**Purpose**: Comprehensive query library for monitoring retry logic and system health

**Contents**:
- 29 production-ready queries
- 10 query categories
- Complete monitoring coverage

**Query Categories**:
1. **Retry Overview** (3 queries) - General retry statistics
2. **Event Publishing Retry** (3 queries) - Event delivery monitoring
3. **RabbitMQ Connection Retry** (4 queries) - Connection health
4. **Message Consumption Retry** (4 queries) - Consumer monitoring
5. **Error Analysis** (3 queries) - Error pattern detection
6. **Performance Metrics** (3 queries) - Latency tracking
7. **Booking Flow Monitoring** (2 queries) - Business metrics
8. **Alerts & Thresholds** (4 queries) - Real-time alerts
9. **Correlation & Tracing** (2 queries) - Request tracing
10. **Daily Summary** (1 query) - Daily reports

### 2. signals-alerts.sql
**Purpose**: Pre-configured alert signals for proactive monitoring

**Contents**:
- 8 critical alert configurations
- Notification matrix (Email/Slack/SMS/PagerDuty)
- Suppression window configurations

**Alert Types**:
- 🚨 **Critical**: Retry Exhaustion, Event Publishing Failure
- 🔴 **High**: RabbitMQ Connection Failure
- ⚠️ **Medium**: High Retry Rate, High Error Rate, Database Issues
- ⚠️ **Low**: DLQ Activity, Service Startup Delays

### 3. DASHBOARD_GUIDE.md
**Purpose**: Step-by-step guide for creating monitoring dashboards

**Contents**:
- 6 dashboard templates
- 20 chart configurations
- Setup instructions

**Dashboard Templates**:
1. Retry Overview Dashboard
2. Event Publishing Health Dashboard
3. RabbitMQ Connection Health Dashboard
4. Message Processing & DLQ Dashboard
5. System Health Overview Dashboard
6. Booking Flow Monitoring Dashboard

### 4. quick-reference.json ⭐ NEW
**Purpose**: Quick copy-paste JSON with essential queries

**Contents**:
- 10 essential queries organized by purpose
- Recommended starter dashboard layout
- 2 critical alert configurations
- Step-by-step usage instructions

**Use this for**: Fast setup - copy queries directly into Seq

### 5. queries-export.json
**Purpose**: Complete query library in JSON format

**Contents**:
- 17 saved queries with metadata
- 2 pre-configured dashboard layouts
- Chart positioning and configuration
- Import instructions for Seq 2025.2

### 6. signals-export.json
**Purpose**: All 8 alert signals in JSON format

**Contents**:
- Complete signal configurations
- Notification app mappings
- Suppression windows
- Priority levels and descriptions

## 🚀 Quick Start

### Step 1: Access Seq
```bash
# Open Seq in browser
http://localhost:5341
```

### Step 2: Import Query
1. Open `retry-monitoring.sql`
2. Find desired query
3. Copy query text
4. In Seq: **Analytics** > **New Query**
5. Paste query
6. Click **Run** to test
7. Click **Save** to keep

### Step 3: Create Dashboard
1. Follow `DASHBOARD_GUIDE.md`
2. Start with "Retry Overview Dashboard"
3. Add charts one by one
4. Set refresh intervals
5. Save dashboard

### Step 4: Configure Alert
1. Open `signals-alerts.sql`
2. Choose alert configuration
3. In Seq: **Settings** > **Signals** > **Add Signal**
4. Paste query
5. Configure notification channel
6. Set suppression window
7. Test and activate

## 📊 Most Useful Queries

### 1. Real-Time Retry Monitor
```sql
SELECT Service, COUNT(*) as RetryCount
FROM stream
WHERE @Message LIKE '%retry%' AND @Timestamp > Now() - 5m
GROUP BY Service;
```

**Use Case**: Monitor current retry activity across all services

### 2. Failed Operations (Critical)
```sql
SELECT @Timestamp, Service, @Message
FROM stream
WHERE @Message LIKE '%failed after%retries%'
  AND @Timestamp > Now() - 1h
ORDER BY @Timestamp DESC;
```

**Use Case**: Identify operations that exhausted all retries

### 3. System Health Score
```sql
SELECT 
    CAST((1.0 - CAST(COUNT(CASE WHEN @Level = 'Error' THEN 1 END) as FLOAT) / 
    NULLIF(COUNT(*), 0)) * 100 as DECIMAL(5,2)) as HealthScore
FROM stream
WHERE @Timestamp > Now() - 1h;
```

**Use Case**: Overall system health percentage

### 4. Event Publishing Success Rate
```sql
SELECT 
    CAST(SUM(CASE WHEN @Message LIKE '%event published%' THEN 1 ELSE 0 END) * 100.0 / 
    NULLIF(COUNT(*), 0) as decimal(5,2)) as SuccessRate
FROM stream
WHERE (@Message LIKE '%event published%' OR @Message LIKE '%Failed to publish%')
  AND @Timestamp > Now() - 1h;
```

**Use Case**: Track event delivery reliability

### 5. Booking Flow Timeline
```sql
SELECT BookingId, @Timestamp, Service, @Message
FROM stream
WHERE BookingId = 'YOUR-BOOKING-ID'
ORDER BY @Timestamp;
```

**Use Case**: Debug specific booking by tracing all related events

## 🚨 Critical Alerts to Configure First

### Priority 1: Must Configure
1. ✅ **Retry Exhaustion** - Catch failed operations immediately
2. ✅ **Event Publishing Failure** - Ensure event delivery
3. ✅ **RabbitMQ Connection Failure** - Monitor connectivity

### Priority 2: Recommended
4. ✅ **High Retry Rate** - Detect degraded performance
5. ✅ **High Error Rate** - Overall system health

### Priority 3: Nice to Have
6. ⚪ **DLQ Activity** - Track problematic messages
7. ⚪ **Database Issues** - Database health
8. ⚪ **Service Startup Delays** - Deployment issues

## 📈 Dashboard Recommendations

### For Operations Team
Start with:
1. **Retry Overview Dashboard** - Primary monitoring view
2. **System Health Overview** - Overall status

### For DevOps Team
Add:
3. **RabbitMQ Connection Health** - Infrastructure monitoring
4. **Message Processing & DLQ** - Consumer health

### For Development Team
Include:
5. **Event Publishing Health** - Event delivery
6. **Booking Flow Monitoring** - Business logic

### For Management
Focus on:
- System Health Score (from System Health Overview)
- Booking Success Rate (from Booking Flow Monitoring)
- Error Rate Trends (from Retry Overview)

## 🔧 Query Usage Tips

### Time Windows
```sql
-- Last 5 minutes (real-time)
WHERE @Timestamp > Now() - 5m

-- Last hour (recent)
WHERE @Timestamp > Now() - 1h

-- Last 24 hours (daily)
WHERE @Timestamp > Now() - 24h

-- Last 7 days (weekly)
WHERE @Timestamp > Now() - 7d
```

### Filtering by Service
```sql
-- Single service
WHERE Service = 'PaymentService'

-- Multiple services
WHERE Service IN ('PaymentService', 'BookingService')

-- All except
WHERE Service != 'ApiGateway'
```

### Filtering by Log Level
```sql
-- Errors only
WHERE @Level = 'Error'

-- Errors and warnings
WHERE @Level IN ('Error', 'Warning')

-- Everything except Debug
WHERE @Level != 'Debug'
```

### Correlation Tracking
```sql
-- Find all logs for a booking
WHERE BookingId = 'abc-123'

-- Find all logs for a payment
WHERE PaymentId = 'xyz-789'

-- Find related events
WHERE BookingId IS NOT NULL AND PaymentId IS NOT NULL
```

## 📖 Additional Documentation

### Comprehensive Guides
- [PHASE5_OBSERVABILITY.md](../PHASE5_OBSERVABILITY.md) - Complete implementation guide
- [PHASE5_SUMMARY.md](../PHASE5_SUMMARY.md) - Quick reference
- [RETRY_LOGIC_AND_POLLY.md](../RETRY_LOGIC_AND_POLLY.md) - Retry implementation details

### Related Documentation
- [PHASE4_CONNECTION_RETRY.md](../PHASE4_CONNECTION_RETRY.md) - Connection retry logic
- [DOCKER_SETUP.md](../DOCKER_SETUP.md) - Infrastructure setup
- [QUICKSTART.md](../../QUICKSTART.md) - Project quick start

## 🆘 Troubleshooting

### Query Returns No Results
1. Check time window - may need to increase
2. Verify services are logging to Seq
3. Test with broader filter first
4. Check log level filter

### Query Too Slow
1. Reduce time window
2. Add `TOP 100` limit
3. Use specific filters
4. Check Seq performance

### Dashboard Not Updating
1. Check auto-refresh is enabled
2. Verify query is valid
3. Clear browser cache
4. Restart Seq container

### Alert Not Triggering
1. Test query manually
2. Check signal is enabled
3. Verify notification channel
4. Check suppression window

## 🔗 External Resources

- [Seq Documentation](https://docs.datalust.co/docs)
- [Seq Query Language Reference](https://docs.datalust.co/docs/query-syntax)
- [Serilog Best Practices](https://github.com/serilog/serilog/wiki/Structured-Data)
- [Structured Logging Guide](https://www.loggly.com/ultimate-guide/structuredlogging/)

## 📝 Contributing

When adding new queries:
1. Follow existing naming conventions
2. Add comments explaining purpose
3. Test query before committing
4. Update this README
5. Document in main guides

## 📄 License

Part of Simple Booking Microservices Project

---

**Last Updated**: November 5, 2025  
**Status**: Production Ready  
**Version**: 1.0.0

---

*For complete implementation details, see [PHASE5_OBSERVABILITY.md](../PHASE5_OBSERVABILITY.md)*
