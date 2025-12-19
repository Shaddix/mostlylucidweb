# DataSummarizer

A DuckDB-first CLI that turns any CSV/Excel/Parquet/JSON into a **reproducible statistical profile**. Optionally, a local LLM (via Ollama) can ask questions using **only**:

- The computed profile (schema + stats + alerts + patterns), and/or
- **DuckDB query results** produced locally from LLM-generated SQL

This keeps the “facts” deterministic and puts the LLM in the role of *query generator + narrator*.

## Trust Model (v1)

- **Deterministic**: profiling, alerts, target analysis, registry ingestion, drift validation.
- **Heuristic** (fast, approximate): distribution labels, trend/seasonality detection, FK/monotonic hints.
- **LLM-generated** (optional): narrative summaries, SQL generation, and result summarization.
- **Data stays local** by default: DuckDB is embedded; LLM calls go to your local Ollama endpoint.

> If you want *zero* row-level data exposure to the LLM, run with `--no-llm`.

---

## Quickstart (single file)

```bash
# Default: Full profile with local LLM narrative (most detailed, recommended)
datasummarizer -f "sampledata/Bank+Customer+Churn/Bank_Churn.csv" --model qwen2.5-coder:7b

# Super fast mode: Stats only, skip expensive patterns (fastest, deterministic)
datasummarizer -f "sampledata/Bank+Customer+Churn/Bank_Churn.csv" --no-llm --fast

# Stats with all patterns (no LLM but thorough analysis)
datasummarizer -f "sampledata/Bank+Customer+Churn/Bank_Churn.csv" --no-llm

# Ask questions about your data (profile-grounded Q&A)
datasummarizer -f "sampledata/Bank+Customer+Churn/Bank_Churn.csv" \
  --model qwen2.5-coder:7b --query "tell me about this data"

# Target-aware profiling (analyze feature effects on target column)
datasummarizer -f "sampledata/Bank+Customer+Churn/Bank_Churn.csv" --target Exited --no-llm
```

> CLI shape: `datasummarizer -f ...` is the human-friendly “pretty report” path. Use subcommands like `datasummarizer profile ...` and `datasummarizer tool ...` for machine-friendly JSON.

---

## What it computes

A profile is designed to answer “what’s in here, and what should I worry about first?”

**Core outputs:**
- Size + schema: row count, inferred column types
- Data quality: nulls, constants, high-cardinality/ID-ish columns, outliers (IQR)
- Numeric stats: min/max, mean/stddev, quantiles, skewness (plus robust MAD on a capped sample)
- Categorical stats: uniques, top values, imbalance/entropy
- Relationships: correlations (Pearson; limited pairs), FK overlap hints
- Patterns: text formats (email/url/uuid/etc.), distribution shape labels, trends, time-series gaps/seasonality

---

## Registry (profiles + retrieval)

DataSummarizer supports multi-file ingestion into a local **Registry**.

- **Registry** = a local DuckDB file (set via `--vector-db`) that stores computed **profiles** plus small derived artifacts (summaries + embeddings + conversation turns).
- **No full tables are copied into the registry**. Profiling and SQL queries read your source files in place.
- “Vector search” is an implementation detail:
  - If DuckDB `vss` is available, it builds an index.
  - If not, it falls back to **in-process cosine distance** over stored embeddings.

Current v1 embeddings are hash-based (128d). They’re good for lightweight retrieval over dataset/column/insight text, but not “deep semantic search”.

### Ingest and cross-dataset Q&A

> “Ingest” here means **profile + cache profile JSON + derived embeddings in the Registry**.

```bash
# Ingest a directory (recursive; globs supported). No LLM for speed.
datasummarizer --ingest-dir "sampledata/Bank+Customer+Churn" \
  --no-llm --vector-db sampledata/registry.duckdb

# Ingest multiple dirs/files at once (glob + mixed paths)
datasummarizer --ingest-files \
  "sampledata/Bank+Customer+Churn/*.csv" \
  "sampledata/CO2+Emissions/*.csv" \
  --no-llm --vector-db sampledata/registry.duckdb

# Ask across the ingested registry (works with or without vss)
datasummarizer --registry-query "Which features drive churn?" \
  --vector-db sampledata/registry.duckdb --no-llm

# Registry ask with LLM summarization (requires Ollama)
datasummarizer --registry-query "Key trends in emissions?" \
  --vector-db sampledata/registry.duckdb --model qwen2.5-coder:7b
```

---

## Requirements

- .NET 9 SDK
- DuckDB (embedded via `DuckDB.NET.Data.Full`)
- Optional: DuckDB `vss` extension (automatic fallback if missing)
- Optional: Ollama running locally for LLM features (`--model`, default `qwen2.5-coder:7b`). `--no-llm` disables LLM.

---

## CLI Subcommands

### `profile` - Profile files and output JSON

```bash
# Profile a single file

datasummarizer profile -f sampledata/Bank+Customer+Churn/Bank_Churn.csv --output bank.profile.json
```

> Note: the JSON profile is the same “facts” used to ground the LLM. It’s also the input to `synth`.

### `synth` - Generate synthetic data from a profile

```bash
# Generate 1000 synthetic rows matching the profile shape

datasummarizer synth --profile bank.profile.json --synthesize-to synthetic.csv --synthesize-rows 1000
```

### `validate` - Compare two datasets and report statistical drift or validate constraints

```bash
# Basic drift comparison (statistical differences between datasets)
datasummarizer validate --source sampledata/Bank+Customer+Churn/Bank_Churn.csv --target synthetic.csv

# Generate constraint suite from source data
datasummarizer validate --source sampledata/Bank+Customer+Churn/Bank_Churn.csv \
  --target synthetic.csv --generate-constraints --output constraints.json

# Validate target against constraint suite
datasummarizer validate --source sampledata/Bank+Customer+Churn/Bank_Churn.csv \
  --target synthetic.csv --constraints constraints.json --format markdown

# Strict mode: exit with error code if constraints fail
datasummarizer validate --source sampledata/Bank+Customer+Churn/Bank_Churn.csv \
  --target synthetic.csv --constraints constraints.json --strict
```

**Output formats**: `--format json` (default), `--format markdown`, `--format html`

### `segment` - Compare two data segments or profiles

```bash
# Compare two datasets (files or stored profile IDs)
datasummarizer segment --segment-a Bank_Churn.csv --segment-b synthetic.csv

# Compare with custom names
datasummarizer segment --segment-a Bank_Churn.csv --segment-b synthetic.csv \
  --segment-name-a "Production" --segment-name-b "Synthetic"

# Output to file in markdown format
datasummarizer segment --segment-a Bank_Churn.csv --segment-b synthetic.csv \
  --output comparison.md --format markdown
```

Shows similarity score, anomaly scores, and detailed column-by-column comparison.

### `tool` - Output JSON for tool/agent integration

```bash
# Profile and output JSON (for MCP tools, pipelines, or LLM agents)

datasummarizer tool -f sampledata/Bank+Customer+Churn/Bank_Churn.csv

# With target analysis

datasummarizer tool -f sampledata/Bank+Customer+Churn/Bank_Churn.csv --target Exited

# Fast mode for quick profiling

datasummarizer tool -f sampledata/Bank+Customer+Churn/Bank_Churn.csv --fast --skip-correlations
```

Sample tool output:

```json
{
  "Success": true,
  "Source": "Bank_Churn.csv",
  "Profile": {
    "RowCount": 10000,
    "ColumnCount": 13,
    "ExecutiveSummary": "This dataset contains **10,000 rows** and **13 columns**...",
    "Columns": [...],
    "Alerts": [
      {
        "Severity": "Info",
        "Column": "Age",
        "Type": "Outliers",
        "Message": "359 outliers (3.6%) outside IQR bounds [14.0, 62.0]"
      }
    ],
    "Insights": [
      {
        "Title": "🎯 Exited Analysis Summary",
        "Score": 0.95,
        "Description": "Target rate: 20.4%. Top drivers: NumOfProducts, Age, Balance"
      }
    ],
    "TargetAnalysis": {
      "TargetColumn": "Exited",
      "IsBinary": true,
      "ClassDistribution": { "0": 79.63, "1": 20.37 },
      "TopDrivers": [...]
    }
  },
  "Metadata": {
    "ProcessingSeconds": 2.5,
    "ColumnsAnalyzed": 13,
    "RowsAnalyzed": 10000
  }
}
```

---

## Examples from bundled `sampledata`

- **Bank churn CSV**: `sampledata/Bank+Customer+Churn/Bank_Churn.csv`
- **CO2 emissions**: `sampledata/CO2+Emissions/visualizing_global_co2_data.csv`
- **Artworks**: `sampledata/MoMA+Art+Collection/Artworks.csv`
- **Retail**: `sampledata/Global+Electronics+Retailer/*.csv`

Try:

```bash
# Churn profiling (no LLM)
datasummarizer -f "sampledata/Bank+Customer+Churn/Bank_Churn.csv" --no-llm --verbose

# Target-aware profiling (analyze what drives churn)
datasummarizer -f "sampledata/Bank+Customer+Churn/Bank_Churn.csv" --target Exited --no-llm

# Emissions profiling with summary question
datasummarizer -f "sampledata/CO2+Emissions/visualizing_global_co2_data.csv" \
  --model qwen2.5-coder:7b --query "tell me about this data"
```

### Sample output (Bank_Churn.csv, no LLM)

```text
── Summary ─────────────────────────────────────────────────────────────────────

This dataset contains **10,000 rows** and **13 columns**. Column breakdown: 5 
numeric, 6 categorical. Found 1 warning(s) to review.

╭─────────────────┬─────────────┬───────┬────────┬─────────────────────────────╮
│ Column          │ Type        │ Nulls │ Unique │ Stats                       │
├─────────────────┼─────────────┼───────┼────────┼─────────────────────────────┤
│ CustomerId      │ Id          │ 0.0%  │ 9,438  │ -                           │
│ Surname         │ Text        │ 0.0%  │ 2,923  │ -                           │
│ CreditScore     │ Numeric     │ 0.0%  │ 509    │ μ=650.5, σ=96.7,            │
│                 │             │       │        │ range=350.0-850.0           │
│ Geography       │ Categorical │ 0.0%  │ 3      │ top: France                 │
│ Gender          │ Categorical │ 0.0%  │ 2      │ top: Male                   │
│ Age             │ Numeric     │ 0.0%  │ 67     │ μ=38.9, σ=10.5,             │
│                 │             │       │        │ range=18.0-92.0             │
│ Tenure          │ Numeric     │ 0.0%  │ 12     │ μ=5.0, σ=2.9,               │
│                 │             │       │        │ range=0.0-10.0              │
│ Balance         │ Numeric     │ 0.0%  │ 6,938  │ μ=76485.9, σ=62397.4,       │
│                 │             │       │        │ range=0.0-250898.1          │
│ NumOfProducts   │ Categorical │ 0.0%  │ 4      │ top: 1                      │
│ HasCrCard       │ Categorical │ 0.0%  │ 2      │ top: 1                      │
│ IsActiveMember  │ Categorical │ 0.0%  │ 2      │ top: 1                      │
│ EstimatedSalary │ Numeric     │ 0.0%  │ 10,000 │ μ=100090.2, σ=57510.5,      │
│                 │             │       │        │ range=11.6-199992.5         │
│ Exited          │ Categorical │ 0.0%  │ 2      │ top: 0                      │
╰─────────────────┴─────────────┴───────┴────────┴─────────────────────────────╯

── Alerts ──────────────────────────────────────────────────────────────────────
- Age: 359 outliers (3.6%) outside IQR bounds [14.0, 62.0]
- NumOfProducts: ℹ Ordinal detected: 4 integer levels - consider treating as ordered numeric
- EstimatedSalary: ⚠ Potential leakage: 100.0% unique (10,000 values) - exclude from modeling

── Insights ────────────────────────────────────────────────────────────────────
💡 Modeling Recommendations (score 0.70)
⚠ Exclude ID columns from features: CustomerId

'CreditScore' Distribution (score 0.59)
Column follows a normal (bell curve) distribution.

'Age' Distribution (score 0.59)
Column is right-skewed (tail extends right).

'Balance' Distribution (score 0.59)
Column is uniformly distributed across its range.
```

### Sample output with target analysis (`--target Exited`)

```text
── Alerts ──────────────────────────────────────────────────────────────────────
- Age: 359 outliers (3.6%) outside IQR bounds [14.0, 62.0]
- NumOfProducts: ℹ Ordinal detected: 4 integer levels - consider treating as ordered numeric
- EstimatedSalary: ⚠ Potential leakage: 100.0% unique (10,000 values) - exclude from modeling

── Insights ────────────────────────────────────────────────────────────────────
🎯 Exited Analysis Summary (score 0.95)
Target rate: 20.4%. Top drivers: NumOfProducts, Age, Balance.

Target driver: NumOfProducts (score 0.86)
NumOfProducts = 4 has 1 rate 100.0% vs baseline 20.4% (Δ 79.6%)

Target driver: Age (score 0.82)
Average Age is 44.8 for 1 vs 37.4 for 0 (Δ 7.4)

💡 Modeling Recommendations (score 0.70)
ℹ Good candidate for logistic regression or gradient boosting | ⚠ Exclude ID columns from features: CustomerId
```

### Sample output (sales.csv, 100k rows)

```text
── Summary ─────────────────────────────────────────────────────────────────────

This dataset contains **100,000 rows** and **14 columns**. Column breakdown: 4 
numeric, 4 categorical, 1 date/time. Found **1 strong correlation(s)**. Found 4 
warning(s) to review.

╭───────────────┬─────────────┬───────┬─────────┬──────────────────────────────╮
│ Column        │ Type        │ Nulls │ Unique  │ Stats                        │
├───────────────┼─────────────┼───────┼─────────┼──────────────────────────────┤
│ OrderId       │ Id          │ 0.0%  │ 97,592  │ -                            │
│ OrderDate     │ DateTime    │ 0.0%  │ 1,264   │ 2022-01-01 → 2024-12-30      │
│ CustomerId    │ Id          │ 0.0%  │ 64,502  │ -                            │
│ CustomerName  │ Text        │ 0.0%  │ 89,958  │ -                            │
│ Email         │ Text        │ 0.0%  │ 100,000 │ -                            │
│ Region        │ Categorical │ 0.0%  │ 5       │ top: South                   │
│ Category      │ Categorical │ 0.0%  │ 6       │ top: Home & Garden           │
│ ProductName   │ Categorical │ 0.0%  │ 53      │ top: Mechanical Keyboard     │
│ Quantity      │ Numeric     │ 0.0%  │ 21      │ μ=10.5, σ=5.8, range=1-20    │
│ UnitPrice     │ Numeric     │ 0.0%  │ 19,586  │ μ=73.4, σ=61.5, range=5-300  │
│ Discount      │ Numeric     │ 0.0%  │ 24      │ μ=0.0, σ=0.1, range=0.0-0.2  │
│ TotalAmount   │ Numeric     │ 0.0%  │ 69,420  │ μ=737.0, σ=813.9, range=4-6k │
│ PaymentMethod │ Categorical │ 0.0%  │ 5       │ top: Cash                    │
│ IsReturned    │ Boolean     │ 0.0%  │ 2       │ -                            │
╰───────────────┴─────────────┴───────┴─────────┴──────────────────────────────╯

── Alerts ──────────────────────────────────────────────────────────────────────
- Email: 100.0% unique - possibly an ID column
- Email: ⚠ Potential leakage: 100.0% unique (100,000 values) - exclude from modeling
- UnitPrice: 5,670 outliers (5.7%) outside IQR bounds [-69.8, 197.0]
- Discount: 5,271 outliers (5.3%) outside IQR bounds [-0.1, 0.2]
- TotalAmount: Skewness: 2.27 - distribution is highly skewed
- TotalAmount: 7,445 outliers (7.4%) outside IQR bounds [-908.4, 2060.8]

── Insights ────────────────────────────────────────────────────────────────────
Text Pattern in 'Email' (score 0.94)
100% of values match Email pattern (100,000 matches).

Text Pattern in 'CustomerName' (score 0.94)
100% of values match Novel pattern (99,500 matches).

💡 Modeling Recommendations (score 0.70)
ℹ High-cardinality categoricals (ProductName) - consider target encoding
⚠ Exclude ID columns from features: OrderId, CustomerId

'Quantity' Distribution (score 0.59)
Column is uniformly distributed across its range.

'UnitPrice' Distribution (score 0.59)
Column is right-skewed (tail extends right).
```

### Sample validation output (source vs synthetic)

`DriftScore` is a 0–1 score (higher = more drift).

```json
{
  "Source": "Bank_Churn.csv",
  "Target": "synthetic.csv",
  "Columns": [
    {
      "Name": "CreditScore",
      "Type": 1,
      "NullDelta": 0,
      "MeanDelta": -8.69,
      "StdDelta": -1.95,
      "MadDelta": -1.36,
      "TopOverlap": null,
      "Note": ""
    },
    {
      "Name": "Geography",
      "Type": 2,
      "NullDelta": 0,
      "TopOverlap": 1.0,
      "Note": ""
    }
  ],
  "DriftScore": 1
}
```

---

## Plain English Queries (LLM)

There are two paths when you use `--query`:

1. **Profile-only answers** (no SQL): for questions like “tell me about this data”, the LLM gets the computed profile and writes a short narrative.
2. **SQL-backed answers**: for more specific questions, the LLM generates DuckDB SQL using the profiled schema, DuckDB executes it locally, and the LLM summarizes the result.

```bash
# Profile-grounded overview (no SQL)
datasummarizer -f sales.csv --model qwen2.5-coder:7b --query "tell me about this data"
```

> Note: SQL-backed answers share the query result (up to 20 rows) with the local LLM for summarization.

### Session-aware Q&A

Supply `--session-id <id>` to keep conversational context across runs.

- Turns are stored in the Registry (`registry_conversations`) and retrieved by similarity.
- This is used as continuity context; it does not invent new columns or facts.

---

## Options reference (common)

- `--file,-f <path>`: single file (CSV/XLSX/XLS/Parquet/JSON)
- `--sheet,-s <name>`: Excel sheet name
- `--model,-m <ollama-model>`: Ollama model (omit or use `--no-llm` to skip)
- `--no-llm`: disable LLM features
- `--target <column>`: target column for supervised analysis (e.g., churn flag)
- `--onnx <path>`: optional ONNX sentinel model path
- `--ingest-dir <dir>`: ingest all supported files (recursive)
- `--ingest-files <paths...>`: ingest specific files/globs
- `--registry-query <text>`: ask across ingested data
- `--vector-db <path>`: Registry DuckDB file path (default `.datasummarizer.vss.duckdb`)
- `--verbose`: extra logging

### Performance options (wide/large tables)

- `--columns <col1,col2,...>`: only analyze these specific columns
- `--exclude-columns <col1,col2,...>`: exclude these columns from analysis
- `--max-columns <n>`: limit columns analyzed (default 50, 0=unlimited). Auto-selects “most interesting” columns.
- `--fast`: skip expensive pattern detection (trends/time-series)
- `--skip-correlations`: skip correlation analysis (faster for many numeric columns)
- `--ignore-errors`: ignore CSV parsing errors (malformed rows)

---

## ONNX sentinel scoring (optional)

If you pass `--onnx path/to/model.onnx`, DataSummarizer can score each column using an ONNX model.

- Input: a fixed-length feature vector built from the computed column stats:
  - null%, unique%, stddev, skewness, outlier ratio, imbalance ratio, avg/max text length, plus type flags
- Output: a single score clamped to 0–1 (higher = “more interesting/risky”)

If the model is missing or incompatible, it safely no-ops.

---

## Heuristics (how patterns are detected)

These are intentionally simple and fast (not a formal test suite):

- **Text formats**: regex match rate must be ≥10% of non-null values.
- **Novel text pattern**: dominant character-class structure must cover ≥70% of sampled distinct values.
- **Distribution labels**: based on skewness + kurtosis; bimodal detection uses a 10-bin histogram and checks for ≥2 peaks.
- **Trends**: linear fit vs date (R² threshold) or row order (first 10k rows).
- **FK overlap hint**: value overlap >90% between candidate columns.
- **Monotonic hint**: >95% of transitions are increasing/decreasing (first 10k rows).
- **Seasonality**: day-of-week count variation (coefficient of variation >0.3).

---

## Notes

- If DuckDB `vss` isn’t available, Registry search transparently uses in-process cosine distance; ingestion and querying still work.
- Sampling for MathNet stats is capped (default 5k rows) to stay memory-safe.
