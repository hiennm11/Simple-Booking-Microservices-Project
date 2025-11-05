-- ============================================================
-- Seq Signals (Alerts) Configuration
-- Simple Booking Microservices Project - Phase 5
-- Updated for Seq 2025.2
-- ============================================================
--
-- HOW TO USE IN SEQ 2025.2:
-- 1. Go to Workspace (left sidebar)
-- 2. Click "Signals" tab
-- 3. Click "Create signal" button
-- 4. Copy the SELECT query below
-- 5. Paste into "Signal expression" field
-- 6. Configure:
--    - Title: [Name from comments below]
--    - Suppress: [Time from comments below]
-- 7. Add Action:
--    - Click "Add action"
--    - Select notification app (Email/Slack)
--    - Configure message template
-- 8. Save signal
--
-- PREREQUISITES:
-- - Install notification apps first (Profile icon > Apps)
-- - Recommended: Seq.App.EmailPlus or Seq.App.Slack
--
-- For detailed instructions: See ../SEQ_2025_QUICK_REFERENCE.md
-- ============================================================

-- SIGNAL 1: High Retry Rate
-- Alert when retry attempts exceed threshold
-- 
-- Query:
SELECT 
    Service,
    COUNT(*) as RetryCount
FROM stream
WHERE @Message LIKE '%retry%'
  AND @Timestamp > Now() - 5m
GROUP BY Service
HAVING COUNT(*) > 20;

-- Signal Configuration:
-- Title: High Retry Rate
-- Description: Alerts when any service has more than 20 retry attempts in 5 minutes
-- Suppress: 10 minutes (to avoid alert spam)
-- Action: Email + Slack
-- Priority: Medium (Warning)

-- ============================================================

-- SIGNAL 2: Retry Exhaustion
-- Alert when retries are completely exhausted
-- 
-- Query:
SELECT 
    @Timestamp,
    Service,
    @Message
FROM stream
WHERE (@Message LIKE '%failed after%retries%'
    OR @Message LIKE '%Moving to DLQ%'
    OR @Message LIKE '%Rejecting message%')
  AND @Timestamp > Now() - 1m;

-- Signal Configuration:
-- Title: Retry Exhaustion Detected
-- Description: Critical alert when operations fail after all retry attempts
-- Suppress: 5 minutes
-- Action: Email + SMS + PagerDuty (if configured)
-- Priority: Critical (Error)

-- ============================================================

-- SIGNAL 3: RabbitMQ Connection Failure
-- Alert when RabbitMQ connection fails repeatedly
-- 
-- Query:
SELECT 
    Service,
    COUNT(*) as ConnectionRetries
FROM stream
WHERE @Message LIKE '%RabbitMQ connection retry%'
  AND @Timestamp > Now() - 2m
GROUP BY Service
HAVING COUNT(*) >= 3;

-- Signal Configuration:
-- Title: RabbitMQ Connection Issues
-- Description: RabbitMQ is unreachable or connection failing
-- Suppress: 15 minutes
-- Action: Email + Slack
-- Priority: High (Error)

-- ============================================================

-- SIGNAL 4: System Health Degradation
-- Alert when error rate exceeds acceptable threshold
-- 
-- Query:
SELECT 
    CAST(
        (CAST(COUNT(CASE WHEN @Level = 'Error' OR @Level = 'Fatal' THEN 1 END) as FLOAT) / 
        NULLIF(COUNT(*), 0)) * 100 
    as DECIMAL(5,2)) as ErrorPercentage
FROM stream
WHERE @Timestamp > Now() - 5m
HAVING CAST(
    (CAST(COUNT(CASE WHEN @Level = 'Error' OR @Level = 'Fatal' THEN 1 END) as FLOAT) / 
    NULLIF(COUNT(*), 0)) * 100 
as DECIMAL(5,2)) > 5.0;

-- Signal Configuration:
-- Title: High Error Rate
-- Description: System error rate exceeds 5% in 5 minutes
-- Suppress: 10 minutes
-- Action: Email + Slack
-- Priority: Medium (Warning)

-- ============================================================

-- SIGNAL 5: Event Publishing Failures
-- Alert on event publishing failures
-- 
-- Query:
SELECT 
    @Timestamp,
    Service,
    PaymentId,
    BookingId,
    @Message
FROM stream
WHERE @Message LIKE '%Failed to publish%event after retries%'
  AND @Timestamp > Now() - 1m;

-- Signal Configuration:
-- Title: Event Publishing Failure
-- Description: Critical - events not being published after retries
-- Suppress: 5 minutes
-- Action: Email + PagerDuty
-- Priority: Critical (Error)

-- ============================================================

-- SIGNAL 6: Consumer Dead Letter Queue Activity
-- Alert when messages are moved to DLQ
-- 
-- Query:
SELECT 
    @Timestamp,
    Service,
    BookingId,
    @Message
FROM stream
WHERE @Message LIKE '%Rejecting message for BookingId%'
  AND @Timestamp > Now() - 5m;

-- Signal Configuration:
-- Title: Dead Letter Queue Activity
-- Description: Messages failing all retry attempts
-- Suppress: 30 minutes
-- Action: Email
-- Priority: Low (Warning)

-- ============================================================

-- SIGNAL 7: Service Startup Issues
-- Alert when services take too long to start
-- 
-- Query:
SELECT 
    Service,
    COUNT(*) as StartupRetries
FROM stream
WHERE @Message LIKE '%connection retry%'
  AND @Message LIKE '%Waiting for RabbitMQ to become available%'
  AND @Timestamp > Now() - 3m
GROUP BY Service
HAVING COUNT(*) > 5;

-- Signal Configuration:
-- Title: Service Startup Delayed
-- Description: Service having trouble starting (RabbitMQ not ready)
-- Suppress: 20 minutes
-- Action: Email
-- Priority: Low (Warning)

-- ============================================================

-- SIGNAL 8: Database Connection Issues
-- Alert on database retry attempts
-- 
-- Query:
SELECT 
    Service,
    COUNT(*) as DbRetries
FROM stream
WHERE @Message LIKE '%Database operation retry%'
  AND @Timestamp > Now() - 5m
GROUP BY Service
HAVING COUNT(*) > 10;

-- Signal Configuration:
-- Title: Database Connection Issues
-- Description: Database operations requiring excessive retries
-- Suppress: 15 minutes
-- Action: Email + Slack
-- Priority: Medium (Warning)

-- ============================================================
-- ============================================================

-- QUICK SETUP GUIDE FOR SEQ 2025.2:
-- 
-- STEP 1: Install Notification Apps
-- -------------------------------
-- 1. Click profile icon (top-right) > Apps
-- 2. Click "Install from NuGet"
-- 3. Search for and install:
--    - Seq.App.EmailPlus (for email notifications)
--    - Seq.App.Slack (for Slack notifications)
-- 4. Configure each app with your settings
--
-- STEP 2: Create Signals
-- -------------------------------
-- 1. Go to Workspace (left sidebar)
-- 2. Click "Signals" tab
-- 3. Click "Create signal" button
-- 4. For each signal above:
--    a. Copy the SELECT query
--    b. Paste into "Signal expression" field
--    c. Set Title from comments
--    d. Set Suppress duration from comments
--    e. Click "Add action"
--    f. Select your notification app
--    g. Configure message template
--    h. Save signal
--
-- STEP 3: Test Your Signals
-- -------------------------------
-- 1. Trigger a test scenario (see PHASE5_OBSERVABILITY.md)
-- 2. Check Workspace > Signals for trigger history
-- 3. Verify notifications were received
-- 4. Adjust thresholds if needed
--
-- For detailed instructions: ../SEQ_2025_QUICK_REFERENCE.md
-- 
-- ============================================================

-- RECOMMENDED NOTIFICATION MATRIX:
-- 
-- | Signal                    | Email | Slack | SMS | PagerDuty |
-- |---------------------------|-------|-------|-----|-----------|
-- | High Retry Rate           |   ✓   |   ✓   |     |           |
-- | Retry Exhaustion          |   ✓   |   ✓   |  ✓  |     ✓     |
-- | RabbitMQ Connection       |   ✓   |   ✓   |     |           |
-- | High Error Rate           |   ✓   |   ✓   |     |           |
-- | Event Publishing Failure  |   ✓   |   ✓   |     |     ✓     |
-- | DLQ Activity              |   ✓   |       |     |           |
-- | Service Startup Delayed   |   ✓   |       |     |           |
-- | Database Issues           |   ✓   |   ✓   |     |           |
-- 
-- ============================================================
--
-- Last Updated: November 5, 2025
-- Seq Version: 2025.2
-- Status: Production Ready
--
-- ============================================================
