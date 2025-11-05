-- ============================================================
-- Seq Query Templates for Retry Logic Monitoring
-- Simple Booking Microservices Project - Phase 5
-- Updated for Seq 2025.2
-- ============================================================
--
-- HOW TO USE THESE QUERIES IN SEQ 2025.2:
--
-- METHOD 1: Quick Search
-- 1. Go to Events page
-- 2. Click in search box
-- 3. Click "SQL" button to switch to SQL mode
-- 4. Copy and paste query below
-- 5. Press Enter to execute
--
-- METHOD 2: Save Query for Reuse
-- 1. Execute query using Method 1
-- 2. Click bookmark/star icon near search bar
-- 3. Give it a descriptive name
-- 4. Find saved queries in Workspace (left sidebar)
--
-- METHOD 3: Add to Dashboard
-- 1. Save query using Method 2
-- 2. Go to Dashboards > Open or create dashboard
-- 3. Click "Add chart"
-- 4. Select chart type (Value, Time series, Table)
-- 5. Enter or select your saved query
-- 6. Configure time range and refresh interval
-- 7. Add to dashboard
--
-- For detailed instructions: ../SEQ_2025_QUICK_REFERENCE.md
--
-- ============================================================

-- ------------------------------------------------------------
-- 1. RETRY OVERVIEW DASHBOARD
-- ------------------------------------------------------------

-- Total retry attempts in last hour (all services)
SELECT 
    COUNT(*) as TotalRetries
FROM stream
WHERE @Message LIKE '%retry%' 
  AND @Message LIKE '%Attempt%'
  AND @Timestamp > Now() - 1h;

-- Retry attempts by service (last 24 hours)
SELECT 
    Service,
    COUNT(*) as RetryCount
FROM stream
WHERE @Message LIKE '%retry%' 
  AND @Message LIKE '%Attempt%'
  AND @Timestamp > Now() - 24h
GROUP BY Service
ORDER BY RetryCount DESC;

-- Retry attempts by hour (last 24 hours)
SELECT 
    DATEPART(hour, @Timestamp) as Hour,
    COUNT(*) as RetryCount
FROM stream
WHERE @Message LIKE '%retry%'
  AND @Timestamp > Now() - 24h
GROUP BY DATEPART(hour, @Timestamp)
ORDER BY Hour;

-- ------------------------------------------------------------
-- 2. EVENT PUBLISHING RETRIES
-- ------------------------------------------------------------

-- Event publishing retry attempts (all services)
SELECT 
    Service,
    COUNT(*) as PublishRetries
FROM stream
WHERE @Message LIKE '%Event publishing retry%'
  AND @Timestamp > Now() - 1h
GROUP BY Service
ORDER BY PublishRetries DESC;

-- Failed event publishing (exhausted retries)
SELECT 
    @Timestamp,
    Service,
    @Message,
    @Exception
FROM stream
WHERE @Message LIKE '%Failed to publish%after retries%'
  AND @Timestamp > Now() - 24h
ORDER BY @Timestamp DESC;

-- Event publishing retry rate per minute (last hour)
SELECT 
    DATEPART(minute, @Timestamp) as Minute,
    Service,
    COUNT(*) as RetryCount
FROM stream
WHERE @Message LIKE '%Event publishing retry%'
  AND @Timestamp > Now() - 1h
GROUP BY DATEPART(minute, @Timestamp), Service
ORDER BY Minute DESC;

-- ------------------------------------------------------------
-- 3. RABBITMQ CONNECTION RETRIES
-- ------------------------------------------------------------

-- RabbitMQ connection retry attempts
SELECT 
    Service,
    COUNT(*) as ConnectionRetries
FROM stream
WHERE @Message LIKE '%RabbitMQ connection retry%'
  AND @Timestamp > Now() - 1h
GROUP BY Service
ORDER BY ConnectionRetries DESC;

-- RabbitMQ connection establishment timeline
SELECT 
    @Timestamp,
    Service,
    @Message
FROM stream
WHERE (@Message LIKE '%RabbitMQ connection retry%' 
    OR @Message LIKE '%RabbitMQ connection established%'
    OR @Message LIKE '%established connection to RabbitMQ%')
  AND @Timestamp > Now() - 1h
ORDER BY @Timestamp DESC;

-- Failed RabbitMQ connections (after all retries)
SELECT 
    @Timestamp,
    Service,
    @Message,
    @Exception
FROM stream
WHERE @Message LIKE '%RabbitMQ connection failed%'
  AND @Timestamp > Now() - 24h
ORDER BY @Timestamp DESC;

-- Average connection establishment time by service
SELECT 
    Service,
    AVG(DATEDIFF(second, 
        LAG(@Timestamp) OVER (PARTITION BY Service ORDER BY @Timestamp),
        @Timestamp)) as AvgConnectionTimeSeconds
FROM stream
WHERE @Message LIKE '%connection established%'
  AND @Timestamp > Now() - 24h
GROUP BY Service;

-- ------------------------------------------------------------
-- 4. MESSAGE CONSUMPTION RETRIES
-- ------------------------------------------------------------

-- Message processing retry attempts
SELECT 
    Service,
    COUNT(*) as MessageRetries
FROM stream
WHERE @Message LIKE '%Retrying message processing%'
  AND @Timestamp > Now() - 1h
GROUP BY Service
ORDER BY MessageRetries DESC;

-- Messages requeued (with attempt counts)
SELECT 
    @Timestamp,
    Service,
    @Message
FROM stream
WHERE @Message LIKE '%Requeuing message%'
  AND @Timestamp > Now() - 1h
ORDER BY @Timestamp DESC;

-- Messages moved to DLQ (dead letter queue)
SELECT 
    @Timestamp,
    Service,
    @Message,
    BookingId
FROM stream
WHERE @Message LIKE '%Moving to DLQ%' 
   OR @Message LIKE '%Rejecting message%'
  AND @Timestamp > Now() - 24h
ORDER BY @Timestamp DESC;

-- Message retry exhaustion rate (last hour)
SELECT 
    Service,
    COUNT(*) as ExhaustedRetries
FROM stream
WHERE (@Message LIKE '%failed after%requeue attempts%'
    OR @Message LIKE '%Moving to DLQ%')
  AND @Timestamp > Now() - 1h
GROUP BY Service;

-- ------------------------------------------------------------
-- 5. ERROR ANALYSIS
-- ------------------------------------------------------------

-- Most common retry errors (last 24 hours)
SELECT 
    SUBSTRING(@Message, 
        CHARINDEX('Error:', @Message) + 7, 
        CHARINDEX(' -', @Message, CHARINDEX('Error:', @Message)) - CHARINDEX('Error:', @Message) - 7
    ) as ErrorType,
    COUNT(*) as Occurrences
FROM stream
WHERE @Message LIKE '%Error:%'
  AND @Message LIKE '%retry%'
  AND @Timestamp > Now() - 24h
GROUP BY SUBSTRING(@Message, 
    CHARINDEX('Error:', @Message) + 7, 
    CHARINDEX(' -', @Message, CHARINDEX('Error:', @Message)) - CHARINDEX('Error:', @Message) - 7
)
ORDER BY Occurrences DESC;

-- Exceptions by type (retry-related)
SELECT 
    @ExceptionType,
    COUNT(*) as Count
FROM stream
WHERE @Exception IS NOT NULL
  AND @Message LIKE '%retry%'
  AND @Timestamp > Now() - 24h
GROUP BY @ExceptionType
ORDER BY Count DESC;

-- Critical failures (all retries exhausted)
SELECT 
    @Timestamp,
    Service,
    @Level,
    @Message,
    @Exception
FROM stream
WHERE (@Level = 'Error' OR @Level = 'Fatal')
  AND (@Message LIKE '%failed after%retries%'
    OR @Message LIKE '%exhausted%')
  AND @Timestamp > Now() - 24h
ORDER BY @Timestamp DESC;

-- ------------------------------------------------------------
-- 6. PERFORMANCE METRICS
-- ------------------------------------------------------------

-- Retry delay distribution (event publishing)
SELECT 
    CASE 
        WHEN @Message LIKE '%after 2%ms%' THEN '2s (Attempt 1)'
        WHEN @Message LIKE '%after 4%ms%' THEN '4s (Attempt 2)'
        WHEN @Message LIKE '%after 8%ms%' THEN '8s (Attempt 3)'
        ELSE 'Other'
    END as RetryDelay,
    COUNT(*) as Count
FROM stream
WHERE @Message LIKE '%Event publishing retry%'
  AND @Timestamp > Now() - 24h
GROUP BY CASE 
    WHEN @Message LIKE '%after 2%ms%' THEN '2s (Attempt 1)'
    WHEN @Message LIKE '%after 4%ms%' THEN '4s (Attempt 2)'
    WHEN @Message LIKE '%after 8%ms%' THEN '8s (Attempt 3)'
    ELSE 'Other'
END
ORDER BY Count DESC;

-- Success rate after retries (event publishing)
SELECT 
    Service,
    SUM(CASE WHEN @Message LIKE '%event published%' THEN 1 ELSE 0 END) as Successes,
    SUM(CASE WHEN @Message LIKE '%Failed to publish%after retries%' THEN 1 ELSE 0 END) as Failures,
    CAST(SUM(CASE WHEN @Message LIKE '%event published%' THEN 1 ELSE 0 END) * 100.0 / 
        (SUM(CASE WHEN @Message LIKE '%event published%' THEN 1 ELSE 0 END) + 
         SUM(CASE WHEN @Message LIKE '%Failed to publish%after retries%' THEN 1 ELSE 0 END))) as decimal(5,2)) as SuccessRate
FROM stream
WHERE (@Message LIKE '%event published%' 
    OR @Message LIKE '%Failed to publish%after retries%')
  AND @Timestamp > Now() - 24h
GROUP BY Service;

-- Average retry attempts before success
SELECT 
    Service,
    AVG(CAST(SUBSTRING(@Message, 
        CHARINDEX('Attempt', @Message) + 8, 
        1) as INT)) as AvgRetryAttempts
FROM stream
WHERE @Message LIKE '%retry%Attempt%'
  AND @Timestamp > Now() - 24h
GROUP BY Service;

-- ------------------------------------------------------------
-- 7. BOOKING FLOW MONITORING
-- ------------------------------------------------------------

-- Booking creation to payment confirmation flow
SELECT 
    BookingId,
    MIN(CASE WHEN @Message LIKE '%Booking created%' THEN @Timestamp END) as BookingCreated,
    MIN(CASE WHEN @Message LIKE '%Payment processed%' THEN @Timestamp END) as PaymentProcessed,
    MIN(CASE WHEN @Message LIKE '%status updated to CONFIRMED%' THEN @Timestamp END) as BookingConfirmed,
    DATEDIFF(second,
        MIN(CASE WHEN @Message LIKE '%Booking created%' THEN @Timestamp END),
        MIN(CASE WHEN @Message LIKE '%status updated to CONFIRMED%' THEN @Timestamp END)
    ) as TotalFlowTimeSeconds
FROM stream
WHERE BookingId IS NOT NULL
  AND @Timestamp > Now() - 1h
GROUP BY BookingId
HAVING MIN(CASE WHEN @Message LIKE '%Booking created%' THEN @Timestamp END) IS NOT NULL
ORDER BY BookingCreated DESC;

-- Retry impact on booking flow
SELECT 
    BookingId,
    COUNT(CASE WHEN @Message LIKE '%retry%' THEN 1 END) as RetryCount,
    MIN(@Timestamp) as FirstEvent,
    MAX(@Timestamp) as LastEvent,
    DATEDIFF(second, MIN(@Timestamp), MAX(@Timestamp)) as FlowDurationSeconds
FROM stream
WHERE BookingId IS NOT NULL
  AND @Timestamp > Now() - 1h
GROUP BY BookingId
HAVING COUNT(CASE WHEN @Message LIKE '%retry%' THEN 1 END) > 0
ORDER BY RetryCount DESC;

-- ------------------------------------------------------------
-- 8. ALERTS & THRESHOLDS
-- ------------------------------------------------------------

-- HIGH RETRY RATE ALERT (>10 retries per minute)
SELECT 
    DATEPART(minute, @Timestamp) as Minute,
    Service,
    COUNT(*) as RetryCount
FROM stream
WHERE @Message LIKE '%retry%'
  AND @Timestamp > Now() - 10m
GROUP BY DATEPART(minute, @Timestamp), Service
HAVING COUNT(*) > 10
ORDER BY RetryCount DESC;

-- RETRY EXHAUSTION ALERT (any failures in last 5 minutes)
SELECT 
    @Timestamp,
    Service,
    @Message
FROM stream
WHERE (@Message LIKE '%failed after%retries%'
    OR @Message LIKE '%Moving to DLQ%')
  AND @Timestamp > Now() - 5m
ORDER BY @Timestamp DESC;

-- CONNECTION FAILURE ALERT (RabbitMQ down)
SELECT 
    @Timestamp,
    Service,
    COUNT(*) as FailedConnectionAttempts
FROM stream
WHERE @Message LIKE '%RabbitMQ connection retry%'
  AND @Timestamp > Now() - 5m
GROUP BY @Timestamp, Service
HAVING COUNT(*) >= 5;

-- SYSTEM HEALTH SCORE (last hour)
SELECT 
    CAST(
        (1.0 - 
            CAST(COUNT(CASE WHEN @Level = 'Error' OR @Level = 'Fatal' THEN 1 END) as FLOAT) / 
            NULLIF(COUNT(*), 0)
        ) * 100 
    as DECIMAL(5,2)) as HealthScorePercentage,
    COUNT(CASE WHEN @Level = 'Error' OR @Level = 'Fatal' THEN 1 END) as ErrorCount,
    COUNT(CASE WHEN @Level = 'Warning' THEN 1 END) as WarningCount,
    COUNT(CASE WHEN @Level = 'Information' THEN 1 END) as InfoCount
FROM stream
WHERE @Timestamp > Now() - 1h;

-- ------------------------------------------------------------
-- 9. CORRELATION & TRACING
-- ------------------------------------------------------------

-- Find all events for a specific booking
-- Replace 'YOUR-BOOKING-ID' with actual BookingId
SELECT 
    @Timestamp,
    Service,
    @Level,
    @Message
FROM stream
WHERE BookingId = 'YOUR-BOOKING-ID'
ORDER BY @Timestamp;

-- Find all retry attempts for a specific operation
-- Replace 'YOUR-PAYMENT-ID' with actual PaymentId
SELECT 
    @Timestamp,
    Service,
    @Message
FROM stream
WHERE PaymentId = 'YOUR-PAYMENT-ID'
  AND @Message LIKE '%retry%'
ORDER BY @Timestamp;

-- ------------------------------------------------------------
-- 10. DAILY SUMMARY REPORT
-- ------------------------------------------------------------

-- Daily summary (last 24 hours)
SELECT 
    Service,
    COUNT(*) as TotalLogs,
    COUNT(CASE WHEN @Level = 'Information' THEN 1 END) as InfoLogs,
    COUNT(CASE WHEN @Level = 'Warning' THEN 1 END) as WarningLogs,
    COUNT(CASE WHEN @Level = 'Error' THEN 1 END) as ErrorLogs,
    COUNT(CASE WHEN @Message LIKE '%retry%' THEN 1 END) as RetryAttempts,
    COUNT(CASE WHEN @Message LIKE '%failed after%retries%' THEN 1 END) as ExhaustedRetries,
    COUNT(CASE WHEN @Message LIKE '%published%' OR @Message LIKE '%processed%' THEN 1 END) as SuccessfulOperations
FROM stream
WHERE @Timestamp > Now() - 24h
GROUP BY Service
ORDER BY Service;

-- ============================================================
-- QUICK REFERENCE - SEQ 2025.2
-- ============================================================
--
-- ACCESSING QUERIES:
-- - Events page > Search box > SQL mode
-- - Save with bookmark icon
-- - Find in Workspace sidebar
--
-- CREATING DASHBOARDS:
-- - Dashboards > Create dashboard
-- - Add chart > Select query
-- - Configure type and refresh
--
-- SETTING UP ALERTS:
-- - Workspace > Signals > Create signal
-- - Copy query from signals-alerts.sql
-- - Add notification action
--
-- TIME RANGES:
-- - Real-time: Now() - 5m or Now() - 1h
-- - Historical: Now() - 24h or Now() - 7d
-- - Adjust based on your monitoring needs
--
-- PERFORMANCE TIPS:
-- - Keep time windows reasonable (1h for real-time)
-- - Use LIMIT for large result sets
-- - Use aggregations instead of raw data
-- - Test queries before adding to dashboards
--
-- DOCUMENTATION:
-- - Full guide: ../PHASE5_OBSERVABILITY.md
-- - Quick reference: ../SEQ_2025_QUICK_REFERENCE.md
-- - Dashboard guide: DASHBOARD_GUIDE.md
-- - Alert configs: signals-alerts.sql
--
-- ============================================================
--
-- Last Updated: November 5, 2025
-- Seq Version: 2025.2
-- Total Queries: 29
-- Status: Production Ready
--
-- ============================================================
