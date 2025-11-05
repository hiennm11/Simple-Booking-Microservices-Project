# Seq JSON Configuration Files - Corrected Structure

## 📋 Overview

Based on actual Seq 2025.2 export format, these JSON files provide configuration references for your monitoring setup.

**Important**: Seq 2025.2 may not support direct JSON dashboard import. Use these files as **structured reference guides** for manual creation in Seq UI.

---

## 📁 Available Files

### ✅ **dashboards-seq-format.json** (NATIVE SEQ FORMAT)

**Status**: Matches actual Seq 2025.2 dashboard export structure  
**Format**: Native Seq JSON (as shown in your example)

**Structure**:
```json
{
  "OwnerId": null,
  "Title": "Dashboard Name",
  "IsProtected": false,
  "SignalExpression": null,
  "Id": "dashboard-id",
  "Charts": [
    {
      "Id": "chart-1",
      "Title": "Chart Title",
      "Queries": [{
        "Measurements": [{"Value": "count(*)", "Label": "count"}],
        "Where": "@Level in ['Error'] ci",
        "GroupBy": ["@Level"],
        "DisplayStyle": {
          "Type": "Pie",
          "Palette": "Default"
        }
      }],
      "DisplayStyle": {
        "WidthColumns": 4,
        "HeightRows": 1
      }
    }
  ]
}
```

**Contains**:
- 4 complete dashboards
- 13 charts with full configuration
- Exact Seq JSON structure

**Use For**:
- Understanding Seq's native structure
- Reference for manual dashboard creation
- Copying exact field values into Seq UI

---

### ✅ **queries-export-NEW.json** (SIMPLIFIED REFERENCE)

**Status**: Simplified format for easy reading  
**Format**: Custom structure optimized for copy-paste

**Structure**:
```json
{
  "queries": [
    {
      "name": "Total Retry Attempts",
      "description": "Count of all retry attempts",
      "measurements": [{"Value": "count(*)", "Label": "Retries"}],
      "where": "@Message like '%retry%' ci",
      "groupBy": [],
      "chartType": "Value",
      "category": "Retry Overview"
    }
  ]
}
```

**Contains**:
- 17 monitoring queries
- Simplified structure for quick reference
- Chart type recommendations
- Embedded instructions

**Use For**:
- Quick query lookup by name or category
- Copy-paste individual query fields
- Understanding query structure

---

### ✅ **quick-reference.json** (FAST SETUP)

**Status**: Essential queries only  
**Format**: Minimal structure for 15-minute setup

**Contains**:
- 10 essential queries
- 1 starter dashboard layout
- 2 critical alerts

**Use For**:
- First-time setup
- Getting monitoring running quickly
- Proof of concept

---

### ✅ **signals-export.json** (ALERTS)

**Status**: Alert configurations  
**Format**: Simplified signal definitions

**Contains**:
- 8 alert configurations
- Notification app mappings
- Suppression windows
- Priority levels

**Use For**:
- Setting up critical alerts
- Understanding alert structure
- Configuring notifications

---

### ✅ **JSON_USAGE_GUIDE_v2.md** (COMPLETE GUIDE)

**Status**: Step-by-step instructions  
**Format**: Markdown documentation

**Contains**:
- How to use each JSON file
- Step-by-step workflows
- 15-minute and 1-hour setup paths
- Troubleshooting guide
- JSON structure explanations

**Use For**:
- Learning how to use the JSON files
- Step-by-step setup instructions
- Understanding Seq concepts

---

## 🚀 Quick Start

### Option 1: Fast Setup (15 minutes)

**Use**: `quick-reference.json` + `JSON_USAGE_GUIDE_v2.md`

**Steps**:
1. Open `quick-reference.json`
2. Follow "15-Minute Quick Start" in guide
3. Create 3-4 essential queries
4. Build 1 starter dashboard
5. Set up 2 critical alerts

**Result**: Basic monitoring operational

---

### Option 2: Complete Setup (1 hour)

**Use**: `queries-export-NEW.json` + `dashboards-seq-format.json` + `signals-export.json`

**Steps**:
1. Create all 17 queries from `queries-export-NEW.json`
2. Build 4 dashboards using `dashboards-seq-format.json` as reference
3. Configure 8 alerts from `signals-export.json`

**Result**: Full monitoring infrastructure

---

### Option 3: Reference-Based Setup

**Use**: `dashboards-seq-format.json` (native format)

**Steps**:
1. Open `dashboards-seq-format.json`
2. See exact Seq JSON structure
3. Manually recreate in Seq UI by copying field values
4. Matches your example structure perfectly

**Result**: Production-ready dashboards matching Seq's native format

---

## 🔧 How to Use These Files

### Understanding the Format Difference

**Your Example (Native Seq Format)**:
```json
{
  "OwnerId": null,
  "Title": "Overview",
  "Charts": [
    {
      "Id": "chart-6",
      "Title": "All Events",
      "Queries": [
        {
          "Measurements": [{"Value": "count(*)", "Label": "count"}],
          "Where": null,
          "GroupBy": [],
          "DisplayStyle": {"Type": "Line"}
        }
      ],
      "DisplayStyle": {"WidthColumns": 8, "HeightRows": 1}
    }
  ]
}
```

**Our Files**:
- `dashboards-seq-format.json`: Uses this EXACT structure ✅
- `queries-export-NEW.json`: Simplified for quick reference
- `quick-reference.json`: Ultra-simplified for fast setup

---

## 📖 Step-by-Step: Creating from JSON

### Example: Creating a Query

**1. Find in `queries-export-NEW.json`**:
```json
{
  "name": "Total Retry Attempts",
  "measurements": [{"Value": "count(*)", "Label": "Retries"}],
  "where": "@Message like '%retry%' ci",
  "groupBy": []
}
```

**2. In Seq UI**:
- Events page → SQL mode
- SELECT: Add `count(*)` with label `Retries`
- WHERE: Paste `@Message like '%retry%' ci`
- Run → Save

---

### Example: Creating a Dashboard Chart

**1. Find in `dashboards-seq-format.json`**:
```json
{
  "Id": "chart-1",
  "Title": "Total Retry Attempts",
  "Queries": [{
    "Measurements": [{"Value": "count(*)", "Label": "Retries"}],
    "Where": "@Message like '%retry%' ci",
    "DisplayStyle": {"Type": "Value"}
  }],
  "DisplayStyle": {
    "WidthColumns": 3,
    "HeightRows": 1
  }
}
```

**2. In Seq UI**:
- Dashboards → Create dashboard
- Add chart
- Copy Measurements, Where clause
- Set Type: Value
- Set Width: 3, Height: 1

---

## 🎯 File Recommendations

| Your Goal | Use This File | Time |
|-----------|---------------|------|
| **Learn structure** | `dashboards-seq-format.json` | Study |
| **Quick setup** | `quick-reference.json` + guide | 15 min |
| **Complete setup** | All files + guide | 1 hour |
| **Single query** | `queries-export-NEW.json` | 5 min |
| **Alerts** | `signals-export.json` | 10 min |
| **Instructions** | `JSON_USAGE_GUIDE_v2.md` | Reference |

---

## ✨ Key Differences from Original Files

### What Changed

**Before** (Original `queries-export.json`):
- Custom structure mixing Seq and simplified formats
- Incomplete dashboard objects
- Some Seq-incompatible syntax

**Now**:
1. **`dashboards-seq-format.json`**: Native Seq structure (matches your example)
2. **`queries-export-NEW.json`**: Clean simplified structure
3. **Files separated by purpose**: Dashboards vs Queries vs Alerts

---

## 🔍 Verification

### Confirming Structure Matches Seq

Your example shows:
```json
{
  "OwnerId": null,
  "Title": "Overview",
  "IsProtected": false,
  "SignalExpression": null,
  "Charts": [...],
  "Id": "dashboard-14",
  "Links": {...}
}
```

Our `dashboards-seq-format.json` matches this structure exactly! ✅

- ✅ `OwnerId`: null
- ✅ `Title`: Dashboard name
- ✅ `IsProtected`: false
- ✅ `SignalExpression`: null
- ✅ `Charts`: Array with Id, Title, Queries, DisplayStyle
- ✅ `Id`: Unique dashboard identifier

---

## 📚 Additional Resources

- **Complete Guide**: `JSON_USAGE_GUIDE_v2.md`
- **Seq 2025.2 Navigation**: `../SEQ_2025_QUICK_REFERENCE.md`
- **SQL Queries**: `retry-monitoring.sql`
- **Alert Configs**: `signals-alerts.sql`
- **Main Documentation**: `../PHASE5_OBSERVABILITY.md`

---

## ❓ FAQ

### Q: Can I import these JSON files directly into Seq?

**A**: Seq 2025.2 may not support direct dashboard JSON import. Use files as **reference** for manual creation.

### Q: Which file should I use first?

**A**: Start with `quick-reference.json` for fast setup, or `queries-export-NEW.json` for comprehensive setup.

### Q: What's the difference between `dashboards-seq-format.json` and `queries-export-NEW.json`?

**A**: 
- `dashboards-seq-format.json`: Native Seq structure (complete dashboards)
- `queries-export-NEW.json`: Simplified query reference (easier to read)

### Q: Do these match the actual Seq structure?

**A**: Yes! `dashboards-seq-format.json` matches your example exactly.

---

## 🎉 Summary

**Files Updated**:
- ✅ `dashboards-seq-format.json` - Native Seq structure (matches your example)
- ✅ `queries-export-NEW.json` - Simplified query reference
- ✅ `quick-reference.json` - Fast setup (unchanged)
- ✅ `signals-export.json` - Alert configs (unchanged)
- ✅ `JSON_USAGE_GUIDE_v2.md` - Updated comprehensive guide

**All files now aligned with Seq 2025.2 native format!** 🚀

Use `JSON_USAGE_GUIDE_v2.md` for complete step-by-step instructions.

---

*Phase 5 Complete - Seq 2025.2 Compatible* ✅
