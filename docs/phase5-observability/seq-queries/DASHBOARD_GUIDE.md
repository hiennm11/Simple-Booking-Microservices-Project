# Seq Dashboard Configuration Guide

> **📌 For Seq 2025.2**: This guide has been updated with current navigation paths. See [SEQ_2025_QUICK_REFERENCE.md](../SEQ_2025_QUICK_REFERENCE.md) for additional help.

## Overview

This guide provides step-by-step instructions for creating Seq dashboards to monitor retry logic and system health in the Simple Booking Microservices Project.

**Seq Version**: Updated for 2025.2  
**Last Updated**: November 5, 2025

---

## Getting Started with Dashboards in Seq 2025.2

### Creating a Dashboard

1. **Open Seq**: Navigate to <http://localhost:5341>
2. **Access Dashboards**: Click **"Dashboards"** in the left sidebar
3. **Create New**: Click **"Create dashboard"** button
4. **Name It**: Enter a descriptive name (e.g., "Retry Overview")
5. **Add Charts**: Click **"Add chart"** button to start adding visualizations

### Adding Charts

For each chart:
1. Click **"Add chart"** button in your dashboard
2. Select **chart type**:
   - **Value**: Single number/metric (e.g., total count)
   - **Time series**: Line/area graph over time
   - **Bar chart**: Comparison across categories
   - **Table**: Detailed event listing
3. Enter **signal expression** (SQL query from examples below)
4. Set **title** for the chart
5. Configure **time range** (e.g., "Last 1 hour")
6. Set **refresh interval** (e.g., "30 seconds")
7. Click **"Add to dashboard"**

### Tips
- Start with simple charts and add complexity gradually
- Use consistent time ranges across related charts
- Set appropriate refresh intervals (30s for real-time, 1-5m for historical)
- Arrange charts by dragging them to desired positions

---

## Dashboard 1: Retry Overview

**Purpose**: High-level view of all retry activity across services

### Charts to Include:

#### 1. Total Retry Attempts (Last Hour)
- **Type**: Number
- **Query**:
```sql
SELECT COUNT(*) as value
FROM stream
WHERE @Message LIKE '%retry%' 
  AND @Message LIKE '%Attempt%'
  AND @Timestamp > Now() - 1h;
```
- **Refresh**: 30 seconds

#### 2. Retry Attempts by Service
- **Type**: Bar Chart
- **Query**:
```sql
SELECT 
    Service as key,
    COUNT(*) as value
FROM stream
WHERE @Message LIKE '%retry%' 
  AND @Timestamp > Now() - 1h
GROUP BY Service
ORDER BY value DESC;
```
- **Refresh**: 1 minute

#### 3. Retry Rate Over Time
- **Type**: Line Chart
- **Query**:
```sql
SELECT 
    DATEPART(minute, @Timestamp) as key,
    COUNT(*) as value
FROM stream
WHERE @Message LIKE '%retry%'
  AND @Timestamp > Now() - 1h
GROUP BY DATEPART(minute, @Timestamp)
ORDER BY key;
```
- **Refresh**: 30 seconds

#### 4. Recent Failed Operations (After Retries)
- **Type**: Table
- **Query**:
```sql
SELECT 
    @Timestamp as [Time],
    Service,
    @Message as [Message]
FROM stream
WHERE (@Message LIKE '%failed after%retries%'
    OR @Message LIKE '%Moving to DLQ%')
  AND @Timestamp > Now() - 1h
ORDER BY @Timestamp DESC;
```
- **Refresh**: 30 seconds

---

## Dashboard 2: Event Publishing Health

**Purpose**: Monitor event publishing and retry success

### Charts to Include:

#### 1. Event Publishing Success Rate
- **Type**: Gauge (0-100%)
- **Query**:
```sql
SELECT 
    CAST(
        SUM(CASE WHEN @Message LIKE '%event published%' THEN 1 ELSE 0 END) * 100.0 / 
        NULLIF(COUNT(*), 0)
    as decimal(5,2)) as value
FROM stream
WHERE (@Message LIKE '%event published%' 
    OR @Message LIKE '%Failed to publish%')
  AND @Timestamp > Now() - 1h;
```
- **Thresholds**: 
  - Green: 95-100%
  - Yellow: 85-95%
  - Red: <85%
- **Refresh**: 1 minute

#### 2. Event Publishing Retries by Service
- **Type**: Stacked Bar Chart
- **Query**:
```sql
SELECT 
    Service as key,
    CASE 
        WHEN @Message LIKE '%Attempt 1%' THEN 'Attempt 1'
        WHEN @Message LIKE '%Attempt 2%' THEN 'Attempt 2'
        WHEN @Message LIKE '%Attempt 3%' THEN 'Attempt 3'
        ELSE 'Other'
    END as series,
    COUNT(*) as value
FROM stream
WHERE @Message LIKE '%Event publishing retry%'
  AND @Timestamp > Now() - 1h
GROUP BY Service, 
    CASE 
        WHEN @Message LIKE '%Attempt 1%' THEN 'Attempt 1'
        WHEN @Message LIKE '%Attempt 2%' THEN 'Attempt 2'
        WHEN @Message LIKE '%Attempt 3%' THEN 'Attempt 3'
        ELSE 'Other'
    END;
```
- **Refresh**: 1 minute

#### 3. Failed Event Publishing Timeline
- **Type**: Timeline
- **Query**:
```sql
SELECT 
    @Timestamp as [Time],
    Service,
    PaymentId,
    BookingId,
    @Message as [Error]
FROM stream
WHERE @Message LIKE '%Failed to publish%after retries%'
  AND @Timestamp > Now() - 24h
ORDER BY @Timestamp DESC;
```
- **Refresh**: 1 minute

---

## Dashboard 3: RabbitMQ Connection Health

**Purpose**: Monitor RabbitMQ connectivity and connection retries

### Charts to Include:

#### 1. Current Connection Status
- **Type**: Status Indicator
- **Query**:
```sql
SELECT 
    CASE 
        WHEN COUNT(*) = 0 THEN 'Healthy'
        WHEN COUNT(*) < 3 THEN 'Warning'
        ELSE 'Critical'
    END as value
FROM stream
WHERE @Message LIKE '%RabbitMQ connection retry%'
  AND @Timestamp > Now() - 2m;
```
- **Refresh**: 30 seconds

#### 2. Connection Retry Attempts (Last Hour)
- **Type**: Line Chart
- **Query**:
```sql
SELECT 
    DATEPART(minute, @Timestamp) as key,
    Service as series,
    COUNT(*) as value
FROM stream
WHERE @Message LIKE '%RabbitMQ connection retry%'
  AND @Timestamp > Now() - 1h
GROUP BY DATEPART(minute, @Timestamp), Service
ORDER BY key;
```
- **Refresh**: 30 seconds

#### 3. Average Connection Time by Service
- **Type**: Bar Chart
- **Query**:
```sql
SELECT 
    Service as key,
    AVG(
        CAST(
            SUBSTRING(@Message, 
                CHARINDEX('after ', @Message) + 6, 
                CHARINDEX('ms', @Message) - CHARINDEX('after ', @Message) - 6
            ) as INT
        )
    ) / 1000.0 as value
FROM stream
WHERE @Message LIKE '%connection established%'
  AND @Timestamp > Now() - 1h
GROUP BY Service;
```
- **Label**: "Average Connection Time (seconds)"
- **Refresh**: 2 minutes

---

## Dashboard 4: Message Processing & DLQ

**Purpose**: Monitor message consumption and dead letter queue

### Charts to Include:

#### 1. Messages in Dead Letter Queue
- **Type**: Number (with trend)
- **Query**:
```sql
SELECT COUNT(*) as value
FROM stream
WHERE (@Message LIKE '%Moving to DLQ%'
    OR @Message LIKE '%Rejecting message%')
  AND @Timestamp > Now() - 1h;
```
- **Color**: Red if > 0
- **Refresh**: 30 seconds

#### 2. Message Requeue Rate
- **Type**: Line Chart
- **Query**:
```sql
SELECT 
    DATEPART(minute, @Timestamp) as key,
    Service as series,
    COUNT(*) as value
FROM stream
WHERE @Message LIKE '%Requeuing message%'
  AND @Timestamp > Now() - 1h
GROUP BY DATEPART(minute, @Timestamp), Service
ORDER BY key;
```
- **Refresh**: 1 minute

#### 3. Recent DLQ Messages
- **Type**: Table
- **Query**:
```sql
SELECT 
    @Timestamp as [Time],
    Service,
    BookingId,
    SUBSTRING(@Message, 1, 100) as [Error Summary]
FROM stream
WHERE (@Message LIKE '%Rejecting message%'
    OR @Message LIKE '%Moving to DLQ%')
  AND @Timestamp > Now() - 24h
ORDER BY @Timestamp DESC;
```
- **Refresh**: 30 seconds

---

## Dashboard 5: System Health Overview

**Purpose**: Overall system health and performance

### Charts to Include:

#### 1. System Health Score
- **Type**: Gauge (0-100%)
- **Query**:
```sql
SELECT 
    CAST(
        (1.0 - 
            CAST(COUNT(CASE WHEN @Level = 'Error' OR @Level = 'Fatal' THEN 1 END) as FLOAT) / 
            NULLIF(COUNT(*), 0)
        ) * 100 
    as DECIMAL(5,2)) as value
FROM stream
WHERE @Timestamp > Now() - 1h;
```
- **Thresholds**:
  - Green: 95-100%
  - Yellow: 90-95%
  - Red: <90%
- **Refresh**: 1 minute

#### 2. Log Level Distribution
- **Type**: Pie Chart
- **Query**:
```sql
SELECT 
    @Level as key,
    COUNT(*) as value
FROM stream
WHERE @Timestamp > Now() - 1h
GROUP BY @Level;
```
- **Colors**:
  - Information: Blue
  - Warning: Yellow
  - Error: Red
  - Fatal: Dark Red
- **Refresh**: 1 minute

#### 3. Service Activity Timeline
- **Type**: Heatmap
- **Query**:
```sql
SELECT 
    DATEPART(minute, @Timestamp) as key,
    Service as series,
    COUNT(*) as value
FROM stream
WHERE @Timestamp > Now() - 1h
GROUP BY DATEPART(minute, @Timestamp), Service
ORDER BY key;
```
- **Refresh**: 1 minute

#### 4. Critical Issues (Last 24 Hours)
- **Type**: Table
- **Query**:
```sql
SELECT 
    @Timestamp as [Time],
    Service,
    @Level as [Level],
    SUBSTRING(@Message, 1, 150) as [Message]
FROM stream
WHERE (@Level = 'Error' OR @Level = 'Fatal')
  AND @Timestamp > Now() - 24h
ORDER BY @Timestamp DESC
LIMIT 50;
```
- **Refresh**: 30 seconds

---

## Dashboard 6: Booking Flow Monitoring

**Purpose**: End-to-end booking and payment flow tracking

### Charts to Include:

#### 1. Average Booking Flow Time
- **Type**: Number with trend
- **Query**:
```sql
SELECT 
    AVG(DATEDIFF(second,
        MIN(CASE WHEN @Message LIKE '%Booking created%' THEN @Timestamp END),
        MIN(CASE WHEN @Message LIKE '%status updated to CONFIRMED%' THEN @Timestamp END)
    )) as value
FROM stream
WHERE BookingId IS NOT NULL
  AND @Timestamp > Now() - 1h
GROUP BY BookingId
HAVING MIN(CASE WHEN @Message LIKE '%Booking created%' THEN @Timestamp END) IS NOT NULL;
```
- **Label**: "Average Flow Time (seconds)"
- **Refresh**: 1 minute

#### 2. Bookings with Retries
- **Type**: Bar Chart
- **Query**:
```sql
SELECT 
    BookingId as key,
    COUNT(CASE WHEN @Message LIKE '%retry%' THEN 1 END) as value
FROM stream
WHERE BookingId IS NOT NULL
  AND @Timestamp > Now() - 1h
GROUP BY BookingId
HAVING COUNT(CASE WHEN @Message LIKE '%retry%' THEN 1 END) > 0
ORDER BY value DESC
LIMIT 10;
```
- **Refresh**: 1 minute

#### 3. Recent Booking Events
- **Type**: Table
- **Query**:
```sql
SELECT 
    @Timestamp as [Time],
    BookingId,
    Service,
    CASE 
        WHEN @Message LIKE '%Booking created%' THEN 'Created'
        WHEN @Message LIKE '%Payment processed%' THEN 'Payment'
        WHEN @Message LIKE '%CONFIRMED%' THEN 'Confirmed'
        ELSE 'Other'
    END as [Stage],
    @Level as [Level]
FROM stream
WHERE BookingId IS NOT NULL
  AND @Timestamp > Now() - 1h
ORDER BY BookingId, @Timestamp;
```
- **Refresh**: 30 seconds

---

## Creating Dashboards in Seq

### Step-by-Step Instructions:

1. **Navigate to Dashboards**
   - Open Seq (http://localhost:5341)
   - Click "Dashboards" in the left sidebar
   - Click "Create dashboard"

2. **Add Dashboard Name**
   - Enter dashboard name (e.g., "Retry Overview")
   - Add description
   - Click "Create"

3. **Add Charts**
   - Click "Add chart"
   - Select chart type (Number, Bar, Line, etc.)
   - Paste query from above
   - Configure chart title and labels
   - Set refresh interval
   - Click "Save"

4. **Arrange Charts**
   - Drag and drop charts to organize
   - Resize charts as needed
   - Group related charts together

5. **Set Dashboard Refresh**
   - Click dashboard settings (gear icon)
   - Set overall refresh interval
   - Enable/disable auto-refresh

6. **Share Dashboard**
   - Click "Share" button
   - Generate public link (optional)
   - Export as JSON for backup

---

## Best Practices

### Chart Refresh Intervals

- **Real-time monitoring**: 30 seconds
- **General metrics**: 1-2 minutes
- **Historical data**: 5-10 minutes
- **Reports**: Manual refresh

### Dashboard Organization

1. **Critical Metrics** (top of dashboard)
   - Health scores
   - Current alerts
   - Error counts

2. **Trends** (middle section)
   - Time-series charts
   - Rate graphs
   - Comparison charts

3. **Details** (bottom section)
   - Tables with recent events
   - Detailed error logs
   - Correlation views

### Color Coding

- **Green**: Healthy, successful operations
- **Yellow**: Warnings, degraded performance
- **Red**: Errors, critical issues
- **Blue**: Informational, neutral metrics

### Performance Tips

1. Limit time windows (prefer 1h over 24h for real-time)
2. Use aggregations instead of raw data
3. Set appropriate refresh intervals
4. Limit table row counts (use LIMIT clause)
5. Create separate dashboards for different time ranges

---

## Dashboard Templates

Save these dashboard configurations for reuse. In Seq 2025.2, dashboards auto-save as you create them.

### Template 1: Operations Dashboard (Priority 1)

**Purpose**: Real-time operational monitoring

**Recommended Charts**:
- Retry Overview
- Event Publishing Health
- RabbitMQ Connection Health
- Message Processing & DLQ

**Who Uses**: Operations team, on-call engineers  
**Refresh**: 30 seconds  
**Time Range**: Last 1 hour

### Template 2: Business Dashboard (Priority 2)

**Purpose**: Business metrics and customer impact

**Recommended Charts**:
- Booking Flow Monitoring
- Success Rates
- Performance Metrics
- Customer Impact

**Who Uses**: Product managers, business stakeholders  
**Refresh**: 2-5 minutes  
**Time Range**: Last 24 hours

### Template 3: DevOps Dashboard (Priority 3)

**Purpose**: System health and infrastructure

**Recommended Charts**:
- System Health Overview
- Resource Usage
- Error Analysis
- Deployment Impact

**Who Uses**: DevOps team, SRE  
**Refresh**: 1 minute  
**Time Range**: Last 1 hour / Last 24 hours

---

## Sharing and Collaboration (Seq 2025.2)

### Share Dashboard

1. Open your dashboard
2. Click the **share icon** or **settings (gear) icon**
3. Options may include:
   - Share URL (if available)
   - Set as default dashboard
   - Duplicate dashboard

### Best Practices

- **Name dashboards clearly**: "Production - Retry Overview" vs just "Overview"
- **Use consistent time ranges**: Don't mix 1h and 24h charts on same dashboard
- **Group related metrics**: Keep publishing metrics together
- **Document purpose**: Add description text to dashboard (if supported)
- **Review regularly**: Update queries as system evolves

---

## Troubleshooting Dashboard Issues

### Dashboard Not Loading

1. Check Seq is running and accessible
2. Verify queries are valid (test in search box)
3. Check time range isn't too large
4. Refresh browser cache

### Charts Showing "No Data"

1. Verify services are logging to Seq
2. Check time range matches data availability
3. Test query in Events page first
4. Ensure property names are correct (case-sensitive)

### Slow Dashboard Performance

1. Reduce time windows (1h instead of 24h)
2. Add `LIMIT` clauses to table queries
3. Increase refresh intervals
4. Use aggregations instead of raw data
5. Split into multiple dashboards

### Charts Not Updating

1. Check auto-refresh is enabled
2. Verify refresh interval setting
3. Test query manually in search
4. Clear browser cache
5. Check Seq server performance

---

## Next Steps

1. ✅ Create your first dashboard following this guide
2. ✅ Add 2-3 key metrics charts
3. ✅ Test with real system data
4. ✅ Share with team
5. ✅ Iterate based on feedback
6. ✅ Create additional specialized dashboards

**For more help**: See [SEQ_2025_QUICK_REFERENCE.md](../SEQ_2025_QUICK_REFERENCE.md)

---

**Last Updated**: November 5, 2025  
**Seq Version**: 2025.2  
**Status**: ✅ Production Ready

---

## Troubleshooting

### Common Issues

**Query returns no results**
- Check time window (Now() - 1h)
- Verify service names are correct
- Check log message patterns

**Chart not updating**
- Verify refresh interval is set
- Check Seq is receiving logs
- Ensure query syntax is correct

**Performance issues**
- Reduce time window
- Add LIMIT to queries
- Increase refresh interval
- Use aggregations

---

## Additional Resources

- [Seq Documentation](https://docs.datalust.co/docs)
- [Signal Query Language Reference](https://docs.datalust.co/docs/the-seq-query-language)
- [Dashboard Best Practices](https://docs.datalust.co/docs/dashboards)
- Project retry documentation: `RETRY_LOGIC_AND_POLLY.md`

---

**Last Updated**: November 5, 2025  
**Version**: 1.0  
**Maintainer**: DevOps Team
