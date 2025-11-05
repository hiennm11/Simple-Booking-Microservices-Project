# Seq 2025.2 - Quick Reference Guide

**Version**: Seq 2025.2  
**For**: Simple Booking Microservices Project  
**Last Updated**: November 5, 2025

---

## 🎯 Common Tasks - Seq 2025.2 Navigation

This guide provides the **actual navigation paths** for Seq 2025.2, as the main documentation was written for older versions.

---

## 📊 Working with Queries

### Create a New Query

1. Go to **Events** page (default landing page)
2. Click in the **search box** at the top
3. For SQL queries: Click **"SQL"** button to switch to SQL mode
4. Enter your query
5. Press **Enter** or click search button to execute

### Save a Query

1. After executing a query, click the **bookmark/star icon** near the search bar
2. Enter a name (e.g., "High Retry Rate")
3. Optionally add description
4. Click **"Save"**

### View Saved Queries

1. Click **"Workspace"** in the left sidebar
2. Your saved queries appear in the list
3. Click any query to run it

### Edit a Saved Query

1. **Workspace** > Click on the query name
2. Query opens in search bar
3. Modify as needed
4. Save with same name to overwrite, or different name for new query

---

## 📈 Creating Dashboards

### Create New Dashboard

1. Click **"Dashboards"** in left sidebar
2. Click **"Create dashboard"** button
3. Enter dashboard name (e.g., "Retry Overview")
4. Dashboard is created

### Add Chart to Dashboard

**Method 1: From Dashboard**
1. Open your dashboard
2. Click **"Add chart"** button
3. Choose chart type:
   - **Value**: Single number (e.g., total count)
   - **Time series**: Line graph over time
   - **Table**: Tabular data
4. Configure:
   - **Signal expression**: Enter SQL query or select saved query
   - **Title**: Chart title
   - **Time range**: e.g., "Last 1 hour"
   - **Refresh interval**: e.g., "30 seconds"
5. Click **"Add to dashboard"**

**Method 2: From Saved Query**
1. **Workspace** > Select a saved query
2. Click **"Add to dashboard"** option
3. Select target dashboard
4. Configure chart type and settings

### Arrange Charts

- **Drag and drop** charts to reorder
- **Resize** by dragging chart corners
- Changes auto-save

### Dashboard Settings

1. Open dashboard
2. Click **settings icon** (gear) in top-right
3. Options:
   - Auto-refresh interval
   - Time range
   - Share dashboard
   - Export/Delete

---

## 🚨 Setting Up Alerts (Signals)

### Install Notification Apps First

1. Click your **profile icon** (top-right corner)
2. Select **"Apps"**
3. Click **"Install from NuGet"**
4. Search for and install:
   - **Seq.App.EmailPlus** - for email notifications
   - **Seq.App.Slack** - for Slack notifications
   - **Seq.App.MSTeams** - for Microsoft Teams
   - Others as needed

### Configure Notification App

1. **Profile icon** > **Apps**
2. Click on the installed app
3. Enter configuration:
   - **Email**: SMTP host, port, credentials, from address
   - **Slack**: Webhook URL, channel
   - **Teams**: Webhook URL
4. Click **"Save"**
5. Test if available

### Create Alert Signal

1. Go to **Workspace** (left sidebar)
2. Click **"Signals"** tab
3. Click **"Create signal"** button
4. Configure signal:
   - **Title**: Descriptive name (e.g., "High Retry Rate")
   - **Signal expression**: Your SQL query
   - **Group by** (optional): Fields to group results
   - **Where** (optional): Additional filters
   - **Suppress**: Time to wait before re-firing (e.g., 10 minutes)

### Add Notification Action

1. In signal configuration, scroll to **Actions** section
2. Click **"Add action"**
3. Select your notification app (Email/Slack/Teams)
4. Configure message template:
   ```
   Title: Alert: {{$Signal}}
   Message: {{$Count}} events detected
   Details: {{$Events}}
   ```
5. Click **"Save action"**
6. Click **"Save"** on signal

### Test Your Signal

1. **Workspace** > **Signals** tab
2. Find your signal
3. Click on it to view details
4. Check recent triggers in signal history
5. Verify notifications were sent

---

## 🔍 Search Tips for Seq 2025.2

### Simple Text Search

```
# Search for text in messages
retry

# Search in specific service
Service = 'PaymentService'

# Combine conditions
retry Service = 'PaymentService'
```

### SQL Mode (Click "SQL" button)

```sql
-- Count events
SELECT COUNT(*) FROM stream 
WHERE @Message LIKE '%retry%'

-- Group by service
SELECT Service, COUNT(*) as Total
FROM stream
WHERE @Timestamp > Now() - 1h
GROUP BY Service

-- Filter by properties
SELECT * FROM stream
WHERE BookingId IS NOT NULL
AND @Level = 'Error'
```

### Time Ranges

- **Last 5 minutes**: `@Timestamp > Now() - 5m`
- **Last hour**: `@Timestamp > Now() - 1h`
- **Last 24 hours**: `@Timestamp > Now() - 24h`
- **Last 7 days**: `@Timestamp > Now() - 7d`

### Common Filters

```sql
-- By log level
WHERE @Level = 'Error'
WHERE @Level IN ('Error', 'Warning')

-- By service
WHERE Service = 'PaymentService'

-- By message content
WHERE @Message LIKE '%retry%'

-- By exception
WHERE @Exception IS NOT NULL

-- By custom property
WHERE BookingId = 'abc-123'
```

---

## 📁 UI Navigation Map

### Main Menu (Left Sidebar)

```
├── Events          # Main log viewer with search
├── Dashboards      # Custom dashboards
├── Workspace       # Saved queries and signals
├── Settings        # (May not be visible in 2025.2 free version)
```

### Top Bar

```
├── Search Box       # Main query interface
├── Time Range       # Quick time filters
├── Profile Icon     # User settings, Apps
├── Help             # Documentation
```

### Workspace Tab Structure

```
Workspace
├── Queries         # Saved SQL queries
├── Signals         # Alert configurations
└── (Other items)
```

---

## 🎯 Quick Actions

### Most Common Tasks

| Task | Navigation | Action |
|------|------------|--------|
| **Search logs** | Events page | Type in search box |
| **Run SQL query** | Search box | Click "SQL", enter query |
| **Save query** | After search | Click bookmark icon |
| **View saved queries** | Workspace | Select query from list |
| **Create dashboard** | Dashboards | Click "Create dashboard" |
| **Add chart** | Open dashboard | Click "Add chart" |
| **Create alert** | Workspace > Signals | Click "Create signal" |
| **Install app** | Profile > Apps | Click "Install from NuGet" |
| **Configure app** | Profile > Apps | Click app name |

---

## 🔧 Settings & Configuration

### Finding Settings in 2025.2

**Note**: Some settings paths have changed in 2025.2

**Instead of**: Settings > Apps  
**Use**: Profile Icon > Apps

**Instead of**: Settings > Signals  
**Use**: Workspace > Signals tab

**Instead of**: Settings > Retention  
**Use**: May require paid version or check Profile settings

**Instead of**: Analytics Tab  
**Use**: Events page with SQL mode

---

## 🚦 Common Issues & Solutions

### "I can't find the Settings menu"

In Seq 2025.2 free/community version, many settings are accessed through:
- **Profile icon** (top-right) for Apps, user settings
- **Workspace** for Signals and saved queries
- **Dashboard settings icon** for dashboard configuration

### "My saved query disappeared"

- Check **Workspace** sidebar
- Ensure you clicked "Save" (bookmark icon) after creating query
- Queries are user-specific in multi-user setups

### "Alert not firing"

1. Check **Workspace > Signals** - is it listed?
2. Click on signal to view configuration
3. Verify notification app is installed (**Profile > Apps**)
4. Check suppression window hasn't been triggered
5. Test signal expression in **Events** search

### "Can't create dashboard"

- Ensure you're using **Dashboards** in left sidebar
- Click **"Create dashboard"** button (not "Add chart")
- Give it a name first, then add charts

---

## 📖 Documentation References

### For Seq 2025.2

- [Official Seq Documentation](https://docs.datalust.co/docs)
- [Query Language Reference](https://docs.datalust.co/docs/query-syntax)
- [App Catalog](https://docs.datalust.co/docs/apps)

### Project Documentation

- [PHASE5_OBSERVABILITY.md](PHASE5_OBSERVABILITY.md) - Complete guide (paths updated)
- [seq-queries/README.md](seq-queries/README.md) - Query library
- [seq-queries/DASHBOARD_GUIDE.md](seq-queries/DASHBOARD_GUIDE.md) - Dashboard templates
- [seq-queries/retry-monitoring.sql](seq-queries/retry-monitoring.sql) - 29 ready queries
- [seq-queries/signals-alerts.sql](seq-queries/signals-alerts.sql) - 8 alert configs

---

## ✅ Verification Checklist

After setting up, verify:

- [ ] Can access Seq at <http://localhost:5341>
- [ ] Can search logs in Events page
- [ ] Can switch to SQL mode for complex queries
- [ ] Can save a query successfully
- [ ] Can find saved queries in Workspace
- [ ] Can create a dashboard
- [ ] Can add charts to dashboard
- [ ] Notification app installed (Email/Slack)
- [ ] Notification app configured
- [ ] At least one signal created
- [ ] Signal has action configured
- [ ] Test alert received (email/Slack)

---

## 🎓 Learning Path

### Day 1: Basic Navigation
1. Explore Events page
2. Try simple text searches
3. Save your first query
4. View saved queries in Workspace

### Day 2: SQL Queries
1. Switch to SQL mode
2. Try queries from `retry-monitoring.sql`
3. Save useful queries
4. Understand time ranges

### Day 3: Dashboards
1. Create your first dashboard
2. Add 2-3 simple charts
3. Arrange and customize
4. Set auto-refresh

### Day 4: Alerts
1. Install notification app
2. Configure app settings
3. Create test signal
4. Verify notification received

### Day 5: Production Setup
1. Import all critical queries
2. Build operational dashboards
3. Configure priority alerts
4. Document for team

---

**Questions or Issues?**

- Check [PHASE5_OBSERVABILITY.md](PHASE5_OBSERVABILITY.md) troubleshooting section
- Review Seq official docs at <https://docs.datalust.co>
- Test with simple queries first before complex ones
- Remember: UI paths may differ between Seq versions

---

**Last Updated**: November 5, 2025  
**Seq Version**: 2025.2  
**Status**: ✅ Verified and tested
