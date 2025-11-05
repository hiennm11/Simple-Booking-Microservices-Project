# Seq 2025.2 JSON Configuration Guide

## Overview

This guide shows you how to use the JSON files to configure Seq 2025.2 for monitoring your Simple Booking Microservices project. **Note**: Seq 2025.2 may not support direct JSON import for dashboards, so these files serve as **configuration references** for manual creation.

---

## Available JSON Files

### 1. **dashboards-seq-format.json** (Native Seq Dashboard Format)

✅ **Best for**: Understanding Seq's native structure, comprehensive reference

**Contents**:
- 4 complete dashboards in Seq's native JSON format
- Matches actual Seq dashboard export structure
- All chart configurations with positioning
- 13 total charts across all dashboards

**Structure Example**:
```json
{
  "OwnerId": null,
  "Title": "Retry Overview Dashboard",
  "Charts": [
    {
      "Id": "chart-1",
      "Title": "Total Retry Attempts",
      "Queries": [{
        "Measurements": [{"Value": "count(*)", "Label": "Retries"}],
        "Where": "@Message like '%retry%' ci",
        "GroupBy": [],
        "DisplayStyle": {"Type": "Value", "Palette": "Default"}
      }],
      "DisplayStyle": {"WidthColumns": 3, "HeightRows": 1}
    }
  ]
}
```

**How to Use**:
1. Open file to see complete dashboard structure
2. Use as reference for manual recreation in Seq
3. Copy field values into Seq UI
4. Refer to metadata section for instructions

---

### 2. **queries-export-NEW.json** (Simplified Query Reference)

✅ **Best for**: Copy-paste individual queries, learning query structure

**Contents**:
- 17 monitoring queries in simplified format
- Query descriptions and use cases
- Chart type recommendations
- Measurements, WHERE clauses, GROUP BY fields
- Embedded how-to-use instructions

**Structure Example**:
```json
{
  "name": "Total Retry Attempts (Last Hour)",
  "description": "Count of all retry attempts across all services",
  "measurements": [{"Value": "count(*)", "Label": "Retries"}],
  "where": "@Message like '%retry%' ci and @Message like '%Attempt%' ci",
  "groupBy": [],
  "chartType": "Value",
  "category": "Retry Overview"
}
```

**How to Use**:
1. Find query by name or category
2. In Seq: Events page → SQL mode
3. Copy `measurements` → Add to SELECT section
4. Copy `where` → Add to WHERE field
5. Copy `groupBy` → Add to GROUP BY section
6. Run and save query

---

### 3. **quick-reference.json** (Fast Setup - 15 minutes)

✅ **Best for**: Fast initial setup, essential monitoring only

**Contents**:
- 10 essential queries organized by purpose
- 1 recommended starter dashboard layout
- 2 critical alert configurations
- Minimal format for quick wins

**Query Categories**:
- **realTimeMonitoring**: Active issues (retries, failures)
- **connectivity**: RabbitMQ, database, HTTP
- **businessMetrics**: Success rates, health scores
- **troubleshooting**: Recent errors, DLQ messages

---

### 4. **signals-export.json** (Alert Configurations)

✅ **Best for**: Setting up critical alerts and notifications

**Contents**:
- 8 alert signal definitions
- Notification app mappings (Email, Slack, PagerDuty)
- Suppression windows
- Priority levels

**Structure Example**:
```json
{
  "title": "High Retry Rate Alert",
  "expression": "SELECT COUNT(*) as RetryCount FROM stream WHERE @Message LIKE '%retry%' GROUP BY time(5m) HAVING RetryCount > 100",
  "priority": "Warning",
  "suppress": "10 minutes",
  "action": "Email - notify-team@company.com",
  "description": "Triggered when retry attempts exceed 100 in 5 minutes"
}
```

---

## Quick Setup Workflows

### 🚀 15-Minute Quick Start

**Goal**: Get essential monitoring running fast

**Steps**:

1. **Open `quick-reference.json`**

2. **Create First Query** (Total Retry Attempts):
   ```
   Seq: Events page → SQL mode
   
   SELECT section:
   - Click '+' → Value: count(*) → Label: Retries
   
   WHERE section:
   - @Message like '%retry%' ci and @Message like '%Attempt%' ci
   
   Click Run → Click Save → Name: "Total Retry Attempts"
   ```

3. **Create Dashboard**:
   ```
   Dashboards → Create dashboard
   Title: "Quick Monitoring"
   
   Add chart → Select saved query "Total Retry Attempts"
   Chart type: Value
   Size: Small (3 columns wide)
   ```

4. **Add 2 More Queries**:
   - "Retry Attempts by Service" (Bar chart)
   - "Failed Operations" (Bar chart with red palette)

5. **Test**: Verify charts show data from your running services

**Time**: 15 minutes  
**Result**: Basic retry monitoring dashboard

---

### ⚙️ 1-Hour Complete Setup

**Goal**: Full monitoring with all queries, dashboards, and alerts

**Phase 1: Create All Queries (30 min)**

From `queries-export-NEW.json`:

1. **System Health Queries** (5 queries):
   - System Health Score
   - Event Count by Level
   - Errors and Exceptions
   - Service Activity Summary
   - Recent Error Messages

2. **Retry Overview Queries** (4 queries):
   - Total Retry Attempts
   - Retry Attempts by Service
   - Retry Rate Over Time
   - Failed Operations

3. **Connectivity Queries** (5 queries):
   - RabbitMQ Connection Retries
   - RabbitMQ Connection Timeline
   - Database Query Retries
   - HTTP Call Retries
   - Publishing Retries by Service

4. **Business Metrics** (3 queries):
   - Publishing Success Rate
   - Failed Publishing Events
   - DLQ Messages

**Phase 2: Build Dashboards (20 min)**

From `dashboards-seq-format.json`:

1. **Retry Overview Dashboard**:
   - Add 4 charts using retry queries
   - Arrange: Value chart (small) → Bar charts → Line chart (full width)

2. **System Health Dashboard**:
   - Add 4 charts using health queries
   - Arrange: Health score → Pie chart → Bar chart → Summary table

3. **Event Publishing Dashboard**:
   - Add 3 charts using publishing queries
   - Arrange: Success rate → Retry bar chart → Failed events timeline

4. **RabbitMQ Dashboard**:
   - Add 2 charts using RabbitMQ queries
   - Arrange: Connection retries → Connection timeline

**Phase 3: Configure Alerts (10 min)**

From `signals-export.json`:

1. **Critical Alerts** (set up first):
   - High Retry Rate Alert
   - Dead Letter Queue Alert
   - System Health Critical

2. **Warning Alerts**:
   - Publishing Failure Rate Warning
   - RabbitMQ Connection Issues
   - Database Retry Warning

3. **For Each Alert**:
   - Workspace → Signals tab → Add signal
   - Enter Title, Expression (SQL query)
   - Set Priority (Warning/Error/Critical)
   - Configure Action (Email/Slack/PagerDuty)
   - Set Suppress window (5-30 minutes)

**Time**: 1 hour  
**Result**: Complete monitoring infrastructure

---

## Step-by-Step: Creating a Query

### Using `queries-export-NEW.json`

**Example**: Creating "Total Retry Attempts" query

**1. Find the Query in JSON**:
```json
{
  "name": "Total Retry Attempts (Last Hour)",
  "measurements": [{"Value": "count(*)", "Label": "Retries"}],
  "where": "@Message like '%retry%' ci and @Message like '%Attempt%' ci",
  "groupBy": [],
  "chartType": "Value",
  "category": "Retry Overview"
}
```

**2. Open Seq Query Builder**:
- Navigate to http://localhost:5341
- Click "Events" in sidebar
- Click "SQL" button (top-right)

**3. Add Measurements**:
- In SELECT section, click '+'
- Value: `count(*)`
- Label: `Retries`

**4. Add WHERE Clause**:
- In WHERE field, paste:
  ```sql
  @Message like '%retry%' ci and @Message like '%Attempt%' ci
  ```

**5. Test and Save**:
- Click "Run" to test
- Verify results appear
- Click "Save" button
- Enter name: "Total Retry Attempts"
- Click Save

**6. Add to Dashboard**:
- Go to Dashboards
- Open or create dashboard
- Click "Add chart"
- Select your saved query
- Choose chart type: "Value"
- Set size: 3 columns wide, 1 row tall
- Position on dashboard

---

## Step-by-Step: Creating a Dashboard

### Using `dashboards-seq-format.json`

**Example**: Creating "Retry Overview Dashboard"

**1. Find Dashboard in JSON**:
```json
{
  "Title": "Retry Overview Dashboard",
  "Charts": [
    { "Title": "Total Retry Attempts", "..." },
    { "Title": "Retry Attempts by Service", "..." },
    { "Title": "Retry Rate Over Time", "..." },
    { "Title": "Failed Operations", "..." }
  ]
}
```

**2. Create New Dashboard**:
- Dashboards → "Create dashboard"
- Enter Title: "Retry Overview Dashboard"
- Dashboard auto-creates

**3. Add First Chart** (Total Retry Attempts):
- Click "Add chart"
- Select saved query "Total Retry Attempts" (or create on-the-fly)
- Chart settings:
  - Type: Value
  - Width: 3 columns
  - Height: 1 row
- Click outside to place chart

**4. Add Second Chart** (Retry Attempts by Service):
- Click "Add chart"
- Measurements: `count(*)`
- WHERE: `@Message like '%retry%' ci`
- GROUP BY: `Service`
- ORDER BY: `count(*)` DESC
- Chart type: Bar
- Width: 9 columns
- Height: 1 row

**5. Add Third Chart** (Retry Rate Over Time):
- Click "Add chart"
- Measurements: `count(*)`
- WHERE: `@Message like '%retry%' ci`
- Chart type: Line
- Display options:
  - Fill to zero: Yes
  - Show markers: No
- Width: 12 columns (full width)
- Height: 2 rows

**6. Add Fourth Chart** (Failed Operations):
- Click "Add chart"
- Measurements: `count(*)`
- WHERE: `(@Message like '%failed after%retries%' ci or @Message like '%Moving to DLQ%' ci)`
- Chart type: Bar
- Palette: Reds
- Width: 12 columns
- Height: 1 row

**7. Arrange Charts**:
- Drag charts to arrange in logical order
- Dashboard auto-saves on changes

**Result**: Complete retry monitoring dashboard with 4 charts

---

## Step-by-Step: Creating an Alert

### Using `signals-export.json`

**Example**: Creating "High Retry Rate Alert"

**1. Find Signal in JSON**:
```json
{
  "title": "High Retry Rate Alert",
  "expression": "SELECT COUNT(*) as RetryCount FROM stream WHERE @Message LIKE '%retry%' AND @Timestamp > Now() - 5m GROUP BY time(5m) HAVING RetryCount > 100",
  "priority": "Warning",
  "suppress": "10 minutes",
  "action": "Email - notify-team@company.com"
}
```

**2. Open Signals**:
- Click profile icon (top-right)
- Select "Workspace"
- Click "Signals" tab
- Click "Add signal" button

**3. Configure Signal**:

**Title**:
```
High Retry Rate Alert
```

**Expression** (copy from JSON):
```sql
SELECT COUNT(*) as RetryCount 
FROM stream 
WHERE @Message LIKE '%retry%' 
  AND @Timestamp > Now() - 5m 
GROUP BY time(5m) 
HAVING RetryCount > 100
```

**4. Set Priority**:
- Dropdown: Select "Warning"

**5. Configure Action**:
- Click "Add action"
- Select app: "Email" (or Slack, PagerDuty)
- Configure notification settings
- Enter: `notify-team@company.com`

**6. Set Suppression**:
- Suppress for: `10` minutes
- Prevents alert spam

**7. Add Description** (optional):
```
Triggered when retry attempts exceed 100 in 5 minutes. 
Indicates potential service degradation.
```

**8. Save**:
- Click "Save" button
- Signal activates immediately

**9. Test**:
- Trigger high retry rate in your services
- Verify alert fires
- Check notification received

---

## Understanding JSON Structure

### Query JSON Structure

```json
{
  "name": "Query Name",              // Display name
  "description": "What it does",      // Use case explanation
  "measurements": [                   // SELECT section
    {
      "Value": "count(*)",            // SQL expression
      "Label": "Retries"              // Chart label
    }
  ],
  "where": "@Message like '%text%'",  // WHERE clause (or null)
  "groupBy": ["Service"],             // GROUP BY fields (array)
  "orderBy": [                        // ORDER BY (optional)
    {
      "Column": "count(*)",
      "Direction": "Desc"
    }
  ],
  "limit": 50,                        // LIMIT (optional)
  "chartType": "Value",               // Chart type recommendation
  "displayOptions": {                 // Visual options
    "Palette": "Reds",
    "LineFillToZeroY": true
  },
  "category": "Retry Overview"        // Organization category
}
```

### Dashboard JSON Structure (Native Seq Format)

```json
{
  "OwnerId": null,                   // Dashboard owner (null = shared)
  "Title": "Dashboard Name",          // Display name
  "IsProtected": false,               // Read-only flag
  "SignalExpression": null,           // Optional signal filter
  "Id": "dashboard-id",               // Unique identifier
  "Charts": [                         // Array of chart objects
    {
      "Id": "chart-1",                // Unique chart ID
      "Title": "Chart Title",         // Display name
      "SignalExpression": null,       // Optional signal
      "Queries": [                    // Array of query objects
        {
          "Id": "query-1",
          "Measurements": [...],       // Same as query JSON
          "Where": "...",
          "GroupBy": [...],
          "DisplayStyle": {            // Chart visualization
            "Type": "Value",           // Chart type
            "Palette": "Default",      // Color scheme
            "LineFillToZeroY": false
          },
          "OrderBy": [...],
          "Limit": null
        }
      ],
      "DisplayStyle": {                // Chart positioning
        "WidthColumns": 3,             // Width (1-12 columns)
        "HeightRows": 1                // Height (rows)
      },
      "Description": "Chart description"
    }
  ]
}
```

### Signal JSON Structure

```json
{
  "title": "Alert Name",              // Display name
  "expression": "SELECT ... HAVING",  // SQL query with threshold
  "priority": "Warning",              // Warning | Error | Critical
  "suppress": "10 minutes",           // Cooldown period
  "action": "Email - address",        // Notification method
  "description": "When it triggers",  // Use case explanation
  "category": "Retry Monitoring"      // Organization category
}
```

---

## Chart Type Reference

### Choosing the Right Chart Type

| Chart Type | Best For | Use Cases | JSON chartType |
|------------|----------|-----------|----------------|
| **Value** | Single number metrics | Total counts, percentages, health scores | `"Value"` |
| **Line** | Time series trends | Retry rates over time, error trends | `"Line"` |
| **Bar** | Comparing values across groups | Retries by service, errors by level | `"Bar"` |
| **Pie** | Distribution/proportions | Log level distribution, error types | `"Pie"` |
| **Table** | Detailed listings | Recent errors, DLQ messages | `"Table"` |

### Display Options Reference

```json
"DisplayStyle": {
  "Type": "Line",                    // Chart type
  "LineFillToZeroY": true,           // Fill area under line
  "LineShowMarkers": false,          // Show data point markers
  "BarOverlaySum": false,            // Stack bars when multiple series
  "UseLogarithmicScale": false,      // Use log scale for Y-axis
  "SuppressLegend": false,           // Hide legend
  "Palette": "Default"               // Color scheme
}
```

**Available Palettes**:
- `Default`: Blue tones
- `Greens`: Green tones (good for success/health)
- `Reds`: Red tones (good for errors/failures)
- `Custom`: Define custom colors

---

## Customization Tips

### Adjusting Thresholds

**In Queries**: Change numeric thresholds in WHERE clause
```json
// Original
"where": "RetryCount > 100"

// Adjusted for your environment
"where": "RetryCount > 50"   // More sensitive
"where": "RetryCount > 200"  // Less sensitive
```

### Adding Time Windows

**Default**: Last 1 hour (Seq default range)

**Custom time windows**:
```sql
-- Last 5 minutes
AND @Timestamp > Now() - 5m

-- Last 24 hours
AND @Timestamp > Now() - 24h

-- Last 7 days
AND @Timestamp > Now() - 7d
```

### Service-Specific Queries

**Add service filter**:
```json
"where": "@Message like '%retry%' ci AND Service = 'BookingService'"
```

### Custom Measurements

**Add calculated fields**:
```json
"measurements": [
  {"Value": "count(*)", "Label": "Total"},
  {"Value": "avg(Duration)", "Label": "Avg Duration"},
  {"Value": "max(RetryAttempt)", "Label": "Max Attempts"}
]
```

---

## Troubleshooting

### JSON File Not Directly Importable

**Problem**: Seq 2025.2 doesn't support direct JSON dashboard import  
**Solution**: Use JSON files as **reference** for manual creation  
**Workaround**: Copy field values into Seq UI

### Query Syntax Errors

**Problem**: Query doesn't run in Seq  
**Common issues**:
- Missing `ci` suffix for case-insensitive matching
- Wrong field names (check Seq Events page for available fields)
- SQL syntax differences from standard SQL

**Fix**: Test queries in SQL mode before saving

### Charts Not Showing Data

**Problem**: Chart displays "No data"  
**Checks**:
1. Verify services are running and logging
2. Check time range (default is last 1 hour)
3. Test WHERE clause returns results
4. Verify field names exist in your logs

### Alert Not Firing

**Problem**: Signal created but never triggers  
**Checks**:
1. Verify notification app is configured (Profile → Apps)
2. Test query returns data
3. Check threshold is reachable
4. Verify suppression window isn't active

---

## Success Checklist

After setup, verify:

### Queries
- [ ] All 17 queries created and tested
- [ ] Queries return expected data
- [ ] Query names are clear and descriptive

### Dashboards
- [ ] 4 dashboards created (Retry, Health, Publishing, RabbitMQ)
- [ ] All charts show real data
- [ ] Dashboard layout is organized and readable
- [ ] Charts refresh automatically

### Alerts
- [ ] 8 signals configured
- [ ] Notification apps set up (Email/Slack/PagerDuty)
- [ ] Test alert triggered successfully
- [ ] Team receives notifications

### Documentation
- [ ] Team knows how to access dashboards
- [ ] Alert response procedures documented
- [ ] Threshold adjustments documented

---

## Next Steps

1. **Monitor for 1 week**: Observe patterns, adjust thresholds
2. **Add custom queries**: Create queries for your specific needs
3. **Fine-tune alerts**: Adjust suppression windows and thresholds
4. **Team training**: Walkthrough dashboards with team
5. **Document findings**: Update thresholds based on real-world data

---

## Quick Reference

### File Purposes

| File | Purpose | Use When |
|------|---------|----------|
| `dashboards-seq-format.json` | Native Seq structure reference | Need complete dashboard structure |
| `queries-export-NEW.json` | Query library with descriptions | Creating individual queries |
| `quick-reference.json` | Fast essential setup | First-time setup, need quick wins |
| `signals-export.json` | Alert configurations | Setting up monitoring alerts |

### Key URLs

- **Seq UI**: http://localhost:5341
- **Events Page**: http://localhost:5341/#/events
- **Dashboards**: http://localhost:5341/#/dashboards
- **Signals**: Profile icon → Workspace → Signals tab

### Quick Commands

**Test services are logging**:
```bash
# Check Seq logs
curl http://localhost:5341/api/events?count=10
```

**Trigger retry for testing**:
```bash
# Stop RabbitMQ temporarily to trigger retries
docker stop rabbitmq
# Wait 30 seconds, check Seq for retry logs
docker start rabbitmq
```

---

## Additional Resources

- **Main Documentation**: See `PHASE5_OBSERVABILITY.md`
- **Seq 2025.2 Reference**: See `SEQ_2025_QUICK_REFERENCE.md`
- **SQL Query Examples**: See `retry-monitoring.sql`
- **Alert Examples**: See `signals-alerts.sql`

---

*Phase 5 Complete - Happy Monitoring! 🚀*
