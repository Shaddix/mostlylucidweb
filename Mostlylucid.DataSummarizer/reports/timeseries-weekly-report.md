# Data Summary: timeseries-weekly.csv

> Generated in 0.5s

## Dataset Overview

| Property | Value |
|----------|-------|
| **Rows** | 365 |
| **Columns** | 3 |
| **Source Type** | Csv |

### Column Types

- **DateTime** (1): `Date`
- **Numeric** (1): `Value`
- **Categorical** (1): `DayOfWeek`

## Column Profiles

| Column | Type | Nulls | Unique | Stats |
|--------|------|-------|--------|-------|
| `Date` | DateTime | 0.0% | 365 | 2023-01-01 → 2023-12-31 |
| `Value` | Numeric | 0.0% | 66 | μ=101.5, σ=23.3, MAD=12.0 |
| `DayOfWeek` | Categorical | 0.0% | 7 | top: 0 (15%) |

## Data Quality Alerts

- 🔵 **DayOfWeek**: ℹ Ordinal detected: 7 integer levels - consider treating as ordered numeric

## Insights

### Time Series Characteristics _(score 0.53)_

Data is a daily time series spanning 2023-01-01 to 2023-12-31.

### Dataset Overview _(score 0.44)_

Dataset contains 365 rows and 3 columns. 1 numeric, 1 categorical, 1 date/time columns.

### Value Distribution _(score 0.41)_

Mean (101.47) differs significantly from median (110.00), suggesting a left-skewed distribution.

### Trend in 'Value'

Values are decreasing over 'Date' (R²=0.00).

### TimeSeries Pattern

Data appears to be a Daily time series indexed by 'Date' (contiguous)

## Top Values (Categorical Columns)

### DayOfWeek

| Value | Count | % |
|-------|-------|---|
| 0 | 53 | 14.5% |
| 1 | 52 | 14.2% |
| 5 | 52 | 14.2% |
| 2 | 52 | 14.2% |
| 4 | 52 | 14.2% |
| 3 | 52 | 14.2% |
| 6 | 52 | 14.2% |

