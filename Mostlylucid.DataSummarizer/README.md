# DataSummarizer

A DuckDB-first, LLM-optional CLI that profiles diverse tabular sources (CSV, Excel, Parquet, JSON), detects patterns, and can answer questions grounded in the computed profile. It supports multi-file ingestion into a persistent DuckDB registry with vector search (fallback cosine when `vss` is unavailable), plus optional ONNX sentinel scoring.

## Features (what’s analyzed)
- **Streaming profiling via DuckDB**: counts, uniques, nulls, quantiles, stddev, skew, correlations (numeric), outliers (IQR), top categorical values, text lengths.
- **Robust stats**: median absolute deviation (MAD) via MathNet on sampled numeric data (up to 5k rows).
- **Pattern detection**: time-series gaps/seasonality, trends vs. date/row order, monotonic sequences, foreign-key overlap, distribution shape (normal/uniform/skewed/bimodal/etc.), text formats (email/url/uuid/phone/ip/card/%/currency), ID heuristics.
- **LLM grounding**: prompts include stats, distributions, trends, alerts, patterns, top values, and correlations. Broad “tell me about this data” questions return a profile-grounded summary (no speculative columns).
- **Registry + vector search**: ingest many files/directories (globs supported); query across cached profiles. Falls back to cosine similarity if DuckDB `vss` isn’t present.
- **Optional ONNX sentinel**: score columns with a custom ONNX model; insights are added when provided.

## Requirements
- .NET 9 SDK
- DuckDB (embedded via `DuckDB.NET.Data.Full`); `vss` extension optional (automatic fallback if missing).
- Optional: running Ollama endpoint for LLM (`--model`, default qwen2.5-coder:7b). `--no-llm` skips LLM.

## Quickstart (single file)
```bash
# Stats only
datasummarizer -f "sampledata/Bank+Customer+Churn/Bank_Churn.csv" --no-llm

# With LLM insights
datasummarizer -f "sampledata/Bank+Customer+Churn/Bank_Churn.csv" --model qwen2.5-coder:7b

# Broad summary question (profile-grounded, no SQL)
datasummarizer -f "sampledata/Bank+Customer+Churn/Bank_Churn.csv" \
  --model qwen2.5-coder:7b --query "tell me about this data"

# Target-aware profiling (analyze feature effects on target column)
datasummarizer -f "sampledata/Bank+Customer+Churn/Bank_Churn.csv" --target Exited --no-llm
```

## Ingest and cross-dataset Q&A
> "Ingest" here means **profile + cache metadata/embeddings in DuckDB**. No data is imported or moved—queries still read from the source files.
```bash
# Ingest a directory (recursive; globs supported). No LLM for speed.
datasummarizer --ingest-dir "sampledata/Bank+Customer+Churn" \
  --no-llm --vector-db sampledata/registry.duckdb

# Ingest multiple dirs/files at once (glob + mixed paths)
datasummarizer --ingest-files \
  "sampledata/Bank+Customer+Churn/*.csv" \
  "sampledata/CO2+Emissions/*.csv" \
  --no-llm --vector-db sampledata/registry.duckdb

# Ask across the ingested registry (falls back to cosine if vss is absent)
datasummarizer --registry-query "Which features drive churn?" \
  --vector-db sampledata/registry.duckdb --no-llm

# Registry ask with LLM summarization (requires Ollama)
datasummarizer --registry-query "Key trends in emissions?" \
  --vector-db sampledata/registry.duckdb --model qwen2.5-coder:7b
```

## CLI Subcommands

### `profile` - Profile files and output JSON
```bash
# Profile a single file
datasummarizer profile -f sampledata/Bank+Customer+Churn/Bank_Churn.csv --output bank.profile.json

# Profile with target column (for feature effect analysis)
datasummarizer profile -f sampledata/Bank+Customer+Churn/Bank_Churn.csv --target Exited --output bank.profile.json

# Profile multiple files (glob support)
datasummarizer profile --ingest-files "sampledata/**/*.csv" --output all-profiles.json
```

### `synth` - Generate synthetic data from a profile
```bash
# Generate 1000 synthetic rows matching the profile shape
datasummarizer synth --profile bank.profile.json --synthesize-to synthetic.csv --synthesize-rows 1000
```

### `validate` - Compare two datasets and report statistical drift
```bash
# Compare source vs synthetic (or any two datasets)
datasummarizer validate --source sampledata/Bank+Customer+Churn/Bank_Churn.csv --target synthetic.csv
```

### `tool` - Output JSON for LLM tool integration
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
      { "Severity": "Info", "Column": "Age", "Type": "Outliers", 
        "Message": "359 outliers (3.6%) outside IQR bounds [14.0, 62.0]" }
    ],
    "Insights": [
      { "Title": "🎯 Exited Analysis Summary", "Score": 0.95,
        "Description": "Target rate: 20.4%. Top drivers: NumOfProducts, Age, Balance" }
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

## Examples from bundled sampledata
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
```
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
│ Geography       │ Categorical │ 0.0%  │ 2      │ top: France                 │
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

### Sample output with target analysis (--target Exited)
```
── Alerts ──────────────────────────────────────────────────────────────────────
- Age: 359 outliers (3.6%) outside IQR bounds [14.0, 62.0]
- NumOfProducts: ℹ Ordinal detected: 4 integer levels - consider treating as ordered numeric
- EstimatedSalary: ⚠ Potential leakage: 100.0% unique (10,000 values) - exclude from modeling

── Insights ────────────────────────────────────────────────────────────────────
🎯 Exited Analysis Summary (score 0.95)
Target rate: 20.4%. Top drivers: NumOfProducts (Δ0.8%), Age (Δ0.7%), Balance (Δ0.3%). 
See feature effects below for actionable segments.

Target driver: NumOfProducts (score 0.86)
NumOfProducts = 4 has 1 rate 100.0% vs baseline 20.4% (Δ 79.6%)

Target driver: Age (score 0.82)
Average Age is 44.8 for 1 vs 37.4 for 0 (Δ 7.4)

💡 Modeling Recommendations (score 0.70)
ℹ Good candidate for logistic regression or gradient boosting | ⚠ Exclude ID columns from features: CustomerId
```

### Sample validation output (source vs synthetic)
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

### Session-aware Q&A
You can supply `--session-id <id>` to keep conversational context across runs. Conversations are stored in the vector registry (or cosine fallback) and reused in `--query` / `--registry-query` prompts without inventing new columns or facts.

## Options reference (common)
- `--file,-f <path>`: single file (CSV/XLSX/XLS/Parquet/JSON)
- `--sheet,-s <name>`: Excel sheet name
- `--model,-m <ollama-model>`: LLM model (omit or `--no-llm` to skip)
- `--no-llm`: disable LLM
- `--target <column>`: target column for supervised analysis (e.g., churn flag, outcome)
- `--onnx <path>`: optional ONNX sentinel
- `--ingest-dir <dir>`: ingest all supported files (recursive)
- `--ingest-files <paths...>`: ingest specific files/globs
- `--registry-query <text>`: ask across ingested data
- `--vector-db <path>`: DuckDB registry path (default `.datasummarizer.vss.duckdb`)
- `--verbose`: extra logging

### Performance options (for wide/large tables)
- `--columns <col1,col2,...>`: only analyze these specific columns
- `--exclude-columns <col1,col2,...>`: exclude these columns from analysis
- `--max-columns <n>`: limit columns analyzed (default 50, 0=unlimited). Auto-selects most interesting columns.
- `--fast`: skip expensive pattern detection (time series, trends)
- `--skip-correlations`: skip correlation analysis (faster for many numeric columns)
- `--ignore-errors`: ignore CSV parsing errors (malformed rows)

## Decision-Oriented Alerts

DataSummarizer provides actionable alerts designed for data scientists:

| Alert Type | Example | Recommendation |
|------------|---------|----------------|
| **Target Imbalance** | `⚠ Target imbalance: 1=20.4% vs 0=79.6%` | Use stratified splits, class weights, or SMOTE |
| **Potential Leakage** | `⚠ Potential leakage: 100% unique` | Exclude from modeling or verify causality |
| **Ordinal as Category** | `ℹ Ordinal detected: 4 integer levels` | Consider treating as ordered numeric |
| **Zero-Inflated** | `ℹ Zero-inflated: 36% zeros` | Consider log transform or two-part model |
| **Outliers with IQR** | `359 outliers outside IQR bounds [14, 62]` | Review for data quality or genuine extremes |

## Modeling Recommendations

When a target column is specified (`--target`), DataSummarizer provides:

1. **Target analysis summary**: Rate, top feature drivers, effect sizes
2. **Feature effects**: Cohen's d (numeric) or rate delta (categorical)
3. **Modeling hints**: Suitable algorithms, imbalance handling, columns to exclude

## What the LLM sees (grounding)
- Column stats: null%, unique%, range, mean, std, MAD, skew, outliers, top values.
- Patterns: distributions, trends, time-series gaps/seasonality, text formats, monotonic/FK hints.
- Alerts: null-heavy, high-cardinality, skew, outliers, imbalance, leakage warnings.
- Correlations: numeric pairs with |r| ≥ 0.3.
- Target analysis: class distribution, feature effects, modeling recommendations.
- Registry Q&A: top-K nearest embeddings (dataset/column/insight text) from the vector store; falls back to cosine if `vss` absent.

## Notes
- If DuckDB `vss` isn’t available, vector search transparently uses in-process cosine similarity; ingestion and querying still work.
- ONNX sentinel is optional; without a model the run proceeds normally.
- Sampling for MathNet stats is capped (default 5k rows) to stay memory-safe.
