# JSON Export Files - Usage Guide

## 📋 Available Files

This directory contains 3 JSON files for easy Seq configuration:

### 1. `quick-reference.json` ⭐ START HERE

**Best for**: Quick setup, essential queries only

**Contains**:
- 10 essential queries organized by purpose
- Recommended starter dashboard layout
- 2 critical alerts
- Copy-paste ready

**Use when**: You want to get started quickly with the most important queries

---

### 2. `queries-export.json`

**Best for**: Complete query library with dashboard templates

**Contains**:
- 17 saved queries with full metadata
- 2 pre-configured dashboard layouts (Retry Overview, System Health)
- Chart types and positioning
- Refresh intervals

**Use when**: You want comprehensive monitoring setup

---

### 3. `signals-export.json`

**Best for**: Alert/signal configuration

**Contains**:
- All 8 alert signals
- Notification app mappings
- Suppression windows
- Priority levels

**Use when**: Setting up proactive monitoring and alerts

---

## 🚀 How to Use These Files

### ⚠️ Important Note

**Seq 2025.2 does not support direct JSON import for signals and queries.** These files are reference documents to help you manually configure Seq. Think of them as "configuration recipes" rather than import files.

---

## 📖 Step-by-Step Usage

### Using `quick-reference.json` (Recommended First Step)

**Step 1: Open the file**
```bash
# Open in your editor
code quick-reference.json

# Or open in browser
start quick-reference.json
```

**Step 2: Copy a query**
```json
// Example: Copy this query
{
  "name": "Real-Time Retry Monitor",
  "query": "SELECT Service, COUNT(*) as RetryCount FROM stream WHERE @Message LIKE '%retry%' AND @Timestamp > Now() - 5m GROUP BY Service"
}
```

**Step 3: Run in Seq**
1. Open Seq: <http://localhost:5341>
2. Click search box → Click "SQL" button
3. Paste the `query` value
4. Press Enter

**Step 4: Save it**
1. Click bookmark/star icon
2. Enter the `name` value
3. Find it later in Workspace

---

### Using `queries-export.json` (Complete Setup)

**For creating dashboards:**

1. Open `queries-export.json`
2. Find the `dashboards` section
3. Look at `"Retry Overview Dashboard"`:
   ```json
   {
     "name": "Retry Overview Dashboard",
     "charts": [
       {
         "title": "Total Retry Attempts",
         "queryName": "Total Retry Attempts (Last Hour)",
         "position": { "row": 1, "col": 1 }
       }
     ]
   }
   ```
4. In Seq:
   - Dashboards → Create dashboard
   - Name it "Retry Overview Dashboard"
   - For each chart, click "Add chart"
   - Find the query in `savedQueries` section
   - Copy the `query` field into Seq
   - Set chart type and title as specified

---

### Using `signals-export.json` (Alerts)

**For creating alerts:**

1. Open `signals-export.json`
2. Find a signal:
   ```json
   {
     "title": "High Retry Rate",
     "expression": "SELECT Service, COUNT(*) as RetryCount...",
     "suppressionMinutes": 10,
     "notificationApps": ["Email", "Slack"]
   }
   ```
3. In Seq:
   - Workspace → Signals → Create signal
   - **Title**: Copy from `title` field
   - **Signal expression**: Copy from `expression` field
   - **Suppress**: Use value from `suppressionMinutes`
   - **Add action**: Select apps from `notificationApps`
4. Save the signal

---

## 🎯 Recommended Workflow

### For First-Time Setup (15 minutes)

**Phase 1: Essential Queries (5 min)**
1. Open `quick-reference.json`
2. Copy and run these 3 queries:
   - Real-Time Retry Monitor
   - Failed Operations (Critical)
   - System Health Score
3. Save each one in Seq

**Phase 2: Starter Dashboard (5 min)**
1. Still in `quick-reference.json`
2. Go to `recommendedStarterDashboard`
3. Create dashboard following the layout
4. Add 4 charts as specified

**Phase 3: Critical Alerts (5 min)**
1. Open `signals-export.json`
2. Create these 2 signals:
   - Retry Exhaustion
   - High Retry Rate
3. Configure notification apps

**✅ You now have working monitoring!**

---

### For Complete Setup (1 hour)

**After completing First-Time Setup above:**

**Phase 4: Additional Queries**
1. Open `queries-export.json`
2. Review `savedQueries` section
3. Add queries you need based on:
   - `category`: What area they monitor
   - `description`: What they show
4. Create 5-10 more saved queries

**Phase 5: Full Dashboards**
1. Still in `queries-export.json`
2. Create both dashboards:
   - Retry Overview Dashboard (operational)
   - System Health Dashboard (overview)
3. Follow the `charts` array for layout

**Phase 6: All Alerts**
1. Open `signals-export.json`
2. Create remaining 6 signals
3. Configure all notification apps
4. Test each signal

**✅ You now have comprehensive monitoring!**

---

## 📊 JSON Structure Guide

### Query Object Structure

```json
{
  "name": "Display name for saved query",
  "description": "What this query does",
  "query": "SELECT ... SQL query to run",
  "category": "Grouping/organization",
  "chartType": "value|gauge|bar|timeSeries|table",
  "refreshSeconds": 30,
  "thresholds": {  // Optional, for gauges
    "green": ">= 95",
    "yellow": "85-95",
    "red": "< 85"
  }
}
```

### Signal Object Structure

```json
{
  "title": "Signal name",
  "description": "When this alert fires",
  "expression": "SELECT ... SQL query",
  "suppressionMinutes": 10,
  "notificationApps": ["Email", "Slack"],
  "priority": "Warning|Error"
}
```

### Dashboard Object Structure

```json
{
  "name": "Dashboard name",
  "description": "Purpose of this dashboard",
  "charts": [
    {
      "title": "Chart title",
      "queryName": "Reference to saved query",
      "position": {
        "row": 1,    // Vertical position
        "col": 1,    // Horizontal position (1-12)
        "width": 6,  // Chart width (1-12)
        "height": 4  // Chart height in units
      }
    }
  ]
}
```

---

## 🔧 Customization Tips

### Adjusting Time Ranges

All queries use time ranges like:
```sql
WHERE @Timestamp > Now() - 1h  -- Last hour
```

Common adjustments:
- `Now() - 5m` → Real-time (5 minutes)
- `Now() - 1h` → Recent (1 hour)
- `Now() - 24h` → Daily (24 hours)
- `Now() - 7d` → Weekly (7 days)

### Adjusting Thresholds

For alerts, change numbers in expressions:
```sql
HAVING COUNT(*) > 20  -- Change 20 to your threshold
```

### Adding Custom Fields

Include your custom properties:
```sql
WHERE BookingId = 'your-id'
WHERE CustomProperty = 'your-value'
```

---

## ❓ Troubleshooting

### "Property not found" error

**Problem**: Query references property that doesn't exist

**Solution**: Check your logs for actual property names
```sql
-- Find available properties
SELECT DISTINCT Service FROM stream WHERE @Timestamp > Now() - 1h
```

### Query returns no results

**Problem**: Time range or filters too restrictive

**Solution**: 
1. Broaden time range: `Now() - 24h` instead of `Now() - 1h`
2. Remove filters one by one
3. Start with simple: `SELECT COUNT(*) FROM stream WHERE @Timestamp > Now() - 1h`

### Dashboard not updating

**Problem**: Auto-refresh not working

**Solution**:
1. Check refresh interval is set
2. Verify query returns results manually
3. Clear browser cache
4. Check Seq is receiving logs

---

## 📚 Related Documentation

- **Full Implementation Guide**: [../PHASE5_OBSERVABILITY.md](../PHASE5_OBSERVABILITY.md)
- **Seq 2025.2 Quick Reference**: [../SEQ_2025_QUICK_REFERENCE.md](../SEQ_2025_QUICK_REFERENCE.md)
- **Dashboard Templates**: [DASHBOARD_GUIDE.md](DASHBOARD_GUIDE.md)
- **All Queries (SQL)**: [retry-monitoring.sql](retry-monitoring.sql)
- **All Signals (SQL)**: [signals-alerts.sql](signals-alerts.sql)

---

## 💡 Pro Tips

1. **Start Small**: Don't try to create everything at once. Start with 3-5 essential queries.

2. **Test First**: Always test queries in search box before saving or adding to dashboards.

3. **Use Categories**: When saving queries, use consistent names like:
   - "Retry - [specific query]"
   - "Health - [specific query]"
   - "Error - [specific query]"

4. **Dashboard Organization**: 
   - Put most important metrics at top
   - Use gauges for percentages
   - Use tables for detailed investigation
   - Use time series for trends

5. **Alert Tuning**: Start with longer suppression times (15-30 min) and adjust down based on alert volume.

---

## ✅ Success Checklist

After setup, verify:

- [ ] Can access Seq at <http://localhost:5341>
- [ ] At least 3 queries saved in Workspace
- [ ] At least 1 dashboard created with 2+ charts
- [ ] At least 1 signal created and active
- [ ] Notification app installed and configured
- [ ] Test alert received (email/Slack)
- [ ] Dashboards auto-refreshing
- [ ] Team knows how to access dashboards

---

**Last Updated**: November 5, 2025  
**Seq Version**: 2025.2  
**Status**: Production Ready

---

**Need Help?** See [SEQ_2025_QUICK_REFERENCE.md](../SEQ_2025_QUICK_REFERENCE.md) for detailed Seq navigation.
