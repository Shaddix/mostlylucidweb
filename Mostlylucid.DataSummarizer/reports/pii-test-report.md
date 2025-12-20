# Data Summary: pii-test.csv

> Generated in 0.1s

## Dataset Overview

| Property | Value |
|----------|-------|
| **Rows** | 5 |
| **Columns** | 4 |
| **Source Type** | Csv |

### Column Types

- **Categorical** (4): `Name`, `Email`, `Phone`, `SSN`

## Column Profiles

| Column | Type | Nulls | Unique | Stats |
|--------|------|-------|--------|-------|
| `Name` | Categorical | 0.0% | 4 | top: Jane Smith (20%) |
| `Email` | Categorical | 0.0% | 4 | top: bob.wilson@test.co.uk (20%) |
| `Phone` | Categorical | 0.0% | 5 | top: 555-789-0123 (20%) |
| `SSN` | Categorical | 0.0% | 5 | top: 777-88-9999 (20%) |

## Data Quality Alerts

- 🔴 **Email**: Potential Email detected (100% confidence). Risk level: High. Consider masking or excluding this column.
- 🔴 **Phone**: Potential PhoneNumber detected (100% confidence). Risk level: High. Consider masking or excluding this column.
- 🔴 **SSN**: Potential SSN detected (100% confidence). Risk level: Critical. Consider masking or excluding this column.
- 🟡 **Name**: Potential PersonName detected (30% confidence). Risk level: Medium. Consider masking or excluding this column.
- 🔵 **Phone**: 100.0% unique - possibly an ID column
- 🔵 **SSN**: 100.0% unique - possibly an ID column

## Insights

### Data Quality Issues _(score 0.89)_

Found 3 critical and 1 warning-level data quality issues. Review alerts for details.

### Dataset Overview _(score 0.44)_

Dataset contains 5 rows and 4 columns. 0 numeric, 4 categorical, 0 date/time columns.

## Top Values (Categorical Columns)

### Name

| Value | Count | % |
|-------|-------|---|
| Jane Smith | 1 | 20.0% |
| Charlie Davis | 1 | 20.0% |
| Bob Wilson | 1 | 20.0% |
| Alice Brown | 1 | 20.0% |
| John Doe | 1 | 20.0% |

### Email

| Value | Count | % |
|-------|-------|---|
| bob.wilson@test.co.uk | 1 | 20.0% |
| jane@company.org | 1 | 20.0% |
| john.doe@example.com | 1 | 20.0% |
| charlie.davis@mail.com | 1 | 20.0% |
| alice@domain.net | 1 | 20.0% |

### Phone

| Value | Count | % |
|-------|-------|---|
| 555-789-0123 | 1 | 20.0% |
| 555-987-6543 | 1 | 20.0% |
| 555-456-7890 | 1 | 20.0% |
| 555-234-5678 | 1 | 20.0% |
| 555-123-4567 | 1 | 20.0% |

### SSN

| Value | Count | % |
|-------|-------|---|
| 777-88-9999 | 1 | 20.0% |
| 111-22-3333 | 1 | 20.0% |
| 123-45-6789 | 1 | 20.0% |
| 444-55-6666 | 1 | 20.0% |
| 987-65-4321 | 1 | 20.0% |

