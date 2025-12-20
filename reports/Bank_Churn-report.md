# Data Summary: Bank_Churn.csv

> Generated in 5.6s

## Dataset Overview

| Property | Value |
|----------|-------|
| **Rows** | 10,000 |
| **Columns** | 13 |
| **Source Type** | Csv |
| **Target** | Exited |

### Column Types

- **Categorical** (6): `Geography`, `Gender`, `NumOfProducts`, `HasCrCard`, `IsActiveMember`, `Exited`
- **Numeric** (5): `CreditScore`, `Age`, `Tenure`, `Balance`, `EstimatedSalary`
- **Id** (1): `CustomerId`
- **Text** (1): `Surname`

## Column Profiles

| Column | Type | Nulls | Unique | Stats |
|--------|------|-------|--------|-------|
| `CustomerId` | Id | 0.0% | 9,438 | identifier |
| `Surname` | Text | 0.0% | 2,626 | avg len: 6 |
| `CreditScore` | Numeric | 0.0% | 509 | μ=650.5, σ=96.7, MAD=67.0 |
| `Geography` | Categorical | 0.0% | 3 | top: France (50%) |
| `Gender` | Categorical | 0.0% | 2 | top: Male (55%) |
| `Age` | Numeric | 0.0% | 67 | μ=38.9, σ=10.5, MAD=6.0 |
| `Tenure` | Numeric | 0.0% | 12 | μ=5.0, σ=2.9, MAD=3.0 |
| `Balance` | Numeric | 0.0% | 6,938 | μ=76485.9, σ=62397.4, MAD=48624.2 |
| `NumOfProducts` | Categorical | 0.0% | 4 | top: 1 (51%) |
| `HasCrCard` | Categorical | 0.0% | 2 | top: 1 (71%) |
| `IsActiveMember` | Categorical | 0.0% | 2 | top: 1 (52%) |
| `EstimatedSalary` | Numeric | 0.0% | 10,000 | μ=100090.2, σ=57510.5, MAD=49645.6 |
| `Exited` | Categorical | 0.0% | 2 | top: 0 (80%) |

## Target Analysis

### Class Distribution

| Class | Share |
|-------|-------|
| 0 | 79.6% |
| 1 | 20.4% |

### Top Drivers

- **NumOfProducts** (rate_delta): NumOfProducts = 4 has 1 rate 100.0% vs baseline 20.4% (Δ 79.6%)
- **Age** (cohens_d): Average Age is 44.8 for 1 vs 37.4 for 0 (Δ 7.4)
- **Balance** (cohens_d): Average Balance is 91108.5 for 1 vs 72745.3 for 0 (Δ 18363.2)
- **Geography** (rate_delta): Geography = Germany has 1 rate 32.4% vs baseline 20.4% (Δ 12.1%)
- **CreditScore** (cohens_d): Average CreditScore is 645.4 for 1 vs 651.9 for 0 (Δ -6.5)
- **IsActiveMember** (rate_delta): IsActiveMember = 0 has 1 rate 26.9% vs baseline 20.4% (Δ 6.5%)
- **Gender** (rate_delta): Gender = Female has 1 rate 25.1% vs baseline 20.4% (Δ 4.7%)
- **Tenure** (cohens_d): Average Tenure is 4.9 for 1 vs 5.0 for 0 (Δ -0.1)
- **EstimatedSalary** (cohens_d): Average EstimatedSalary is 101465.7 for 1 vs 99738.4 for 0 (Δ 1727.3)
- **Surname** (rate_delta): Surname = McGregor has 1 rate 42.9% vs baseline 20.4% (Δ 22.5%)

## Data Quality Alerts

- 🔴 **Geography**: Potential PassportNumber detected (67% confidence). Risk level: High. Consider masking or excluding this column.
- 🟡 **EstimatedSalary**: ⚠ Potential leakage: 100.0% unique (10,000 values) - exclude from modeling or verify
- 🔵 **Age**: 359 outliers (3.6%) outside IQR bounds [14.0, 62.0]
- 🔵 **NumOfProducts**: ℹ Ordinal detected: 4 integer levels - consider treating as ordered numeric

## Insights

### 🎯 Exited Analysis Summary _(score 0.95)_

Target rate: 20.4%. Top drivers: NumOfProducts (Δ0.8%), Age (Δ0.7%), Balance (Δ0.3%). See feature effects below for actionable segments.

### Text Pattern in 'Surname' _(score 0.93)_

98% of values match Novel pattern (9,750 matches).

### Target driver: NumOfProducts _(score 0.86)_

NumOfProducts = 4 has 1 rate 100.0% vs baseline 20.4% (Δ 79.6%)

### Target driver: Age _(score 0.82)_

Average Age is 44.8 for 1 vs 37.4 for 0 (Δ 7.4)

### 💡 Modeling Recommendations _(score 0.70)_

ℹ Good candidate for logistic regression or gradient boosting | ⚠ Exclude ID columns from features: CustomerId

### 'CreditScore' Distribution _(score 0.59)_

Column follows a normal (bell curve) distribution.

### 'Age' Distribution _(score 0.59)_

Column is right-skewed (tail extends right).

### 'Balance' Distribution _(score 0.59)_

Column is uniformly distributed across its range.

### 'EstimatedSalary' Distribution _(score 0.59)_

Column is uniformly distributed across its range.

### Target driver: Balance _(score 0.51)_

Average Balance is 91108.5 for 1 vs 72745.3 for 0 (Δ 18363.2)

### Dataset Overview _(score 0.44)_

Dataset contains 10,000 rows and 13 columns. 5 numeric, 6 categorical, 0 date/time columns.

### Data Quality Issues _(score 0.43)_

Found 1 critical and 1 warning-level data quality issues. Review alerts for details.

### Target driver: Geography _(score 0.38)_

Geography = Germany has 1 rate 32.4% vs baseline 20.4% (Δ 12.1%)

### Target driver: CreditScore _(score 0.35)_

Average CreditScore is 645.4 for 1 vs 651.9 for 0 (Δ -6.5)

### Target driver: IsActiveMember _(score 0.35)_

IsActiveMember = 0 has 1 rate 26.9% vs baseline 20.4% (Δ 6.5%)

### Target driver: Gender _(score 0.33)_

Gender = Female has 1 rate 25.1% vs baseline 20.4% (Δ 4.7%)

### Target driver: Tenure _(score 0.32)_

Average Tenure is 4.9 for 1 vs 5.0 for 0 (Δ -0.1)

## Top Values (Categorical Columns)

### Geography

| Value | Count | % |
|-------|-------|---|
| France | 5,014 | 50.1% |
| Germany | 2,509 | 25.1% |
| Spain | 2,477 | 24.8% |

### Gender

| Value | Count | % |
|-------|-------|---|
| Male | 5,457 | 54.6% |
| Female | 4,543 | 45.4% |

### NumOfProducts

| Value | Count | % |
|-------|-------|---|
| 1 | 5,084 | 50.8% |
| 2 | 4,590 | 45.9% |
| 3 | 266 | 2.7% |
| 4 | 60 | 0.6% |

### HasCrCard

| Value | Count | % |
|-------|-------|---|
| 1 | 7,055 | 70.5% |
| 0 | 2,945 | 29.4% |

### IsActiveMember

| Value | Count | % |
|-------|-------|---|
| 1 | 5,151 | 51.5% |
| 0 | 4,849 | 48.5% |

