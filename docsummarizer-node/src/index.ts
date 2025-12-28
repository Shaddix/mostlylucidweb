import { spawn } from 'child_process';
import { EventEmitter } from 'events';
import { existsSync } from 'fs';
import { join } from 'path';
import { homedir } from 'os';

// ============================================================================
// Schema Version - increment when output structure changes
// ============================================================================

export const SCHEMA_VERSION = '1.0.0';

// ============================================================================
// Platform Detection & Executable Resolution
// ============================================================================

interface ExecutableInfo {
  command: string;
  prependArgs: string[];
  isDotnetRun: boolean;
}

/**
 * Find the docsummarizer CLI.
 * 
 * Resolution order:
 * 1. Explicit path (options.executable)
 * 2. DOCSUMMARIZER_PATH env var
 * 3. DOCSUMMARIZER_PROJECT env var (uses dotnet run)
 * 4. Bundled vendor binary (downloaded by postinstall)
 * 5. ~/.dotnet/tools/ (global tool install)
 * 6. System PATH
 */
function findExecutable(customPath?: string): ExecutableInfo {
  if (customPath) {
    return { command: customPath, prependArgs: [], isDotnetRun: false };
  }

  const envPath = process.env.DOCSUMMARIZER_PATH;
  if (envPath && existsSync(envPath)) {
    return { command: envPath, prependArgs: [], isDotnetRun: false };
  }

  const projectPath = process.env.DOCSUMMARIZER_PROJECT;
  if (projectPath && existsSync(projectPath)) {
    return { 
      command: 'dotnet', 
      prependArgs: ['run', '--project', projectPath, '-f', 'net10.0', '--'],
      isDotnetRun: true 
    };
  }

  const isWindows = process.platform === 'win32';
  const exeName = isWindows ? 'docsummarizer.exe' : 'docsummarizer';

  // Check bundled vendor binary (downloaded by postinstall)
  // Use __dirname to find relative to the compiled dist folder
  const vendorDir = join(__dirname, '..', 'vendor');
  const vendorPath = join(vendorDir, exeName);
  if (existsSync(vendorPath)) {
    return { command: vendorPath, prependArgs: [], isDotnetRun: false };
  }

  const home = homedir();
  const dotnetToolsDir = join(home, '.dotnet', 'tools');
  
  if (isWindows) {
    const exePath = join(dotnetToolsDir, 'docsummarizer.exe');
    if (existsSync(exePath)) return { command: exePath, prependArgs: [], isDotnetRun: false };
    
    const cmdPath = join(dotnetToolsDir, 'docsummarizer.cmd');
    if (existsSync(cmdPath)) return { command: cmdPath, prependArgs: [], isDotnetRun: false };
  } else {
    const toolPath = join(dotnetToolsDir, 'docsummarizer');
    if (existsSync(toolPath)) return { command: toolPath, prependArgs: [], isDotnetRun: false };
  }

  return { command: 'docsummarizer', prependArgs: [], isDotnetRun: false };
}

// ============================================================================
// Types - Match actual CLI JSON output
// ============================================================================

/** Summarization mode */
export type SummarizationMode = 
  | 'Auto' 
  | 'BertRag' 
  | 'Bert' 
  | 'BertHybrid' 
  | 'Iterative' 
  | 'MapReduce' 
  | 'Rag';

/** Constructor options */
export interface DocSummarizerOptions {
  /** Path to CLI executable (auto-detected if not provided) */
  executable?: string;
  /** Path to config JSON file */
  configPath?: string;
  /** Ollama model name */
  model?: string;
  /** Timeout in milliseconds (default: 300000 = 5 min) */
  timeout?: number;
}

/** Options for summarization */
export interface SummarizeOptions {
  /** Focus the summary on a specific topic */
  query?: string;
  /** Summarization mode */
  mode?: SummarizationMode;
}

/** Options for search */
export interface SearchOptions {
  /** Maximum results to return (default: 10) */
  topK?: number;
}

// --- Summary Result ---

export interface SummaryTopic {
  topic: string;
  summary: string;
  sourceChunks: string[];
}

export interface SummaryEntities {
  people?: string[];
  organizations?: string[];
  locations?: string[];
  dates?: string[];
  events?: string[];
}

export interface SummaryMetadata {
  documentId: string;
  totalChunks: number;
  chunksProcessed: number;
  /** Coverage score 0-1 (fraction of document covered) */
  coverageScore: number;
  /** Citation rate 0-1 */
  citationRate: number;
  processingTimeMs: number;
  mode: string;
  model: string;
}

export interface SummaryResult {
  schemaVersion: string;
  success: true;
  source: string;
  type: 'summary';
  summary: string;
  wordCount: number;
  topics: SummaryTopic[];
  entities?: SummaryEntities;
  openQuestions?: string[];
  metadata: SummaryMetadata;
}

// --- QA Result ---

export interface QAEvidence {
  segmentId: string;
  text: string;
  /** Similarity score (cosine similarity, typically 0-1 for normalized vectors) */
  similarity: number;
  section: string;
}

export interface QAMetadata {
  processingTimeMs: number;
  model: string;
}

export interface QAResult {
  schemaVersion: string;
  success: true;
  source: string;
  type: 'qa';
  question: string;
  answer: string;
  /** Confidence level */
  confidence: 'High' | 'Medium' | 'Low';
  evidence: QAEvidence[];
  metadata: QAMetadata;
}

// --- Search Result ---

export interface SearchResultItem {
  section: string;
  /** Similarity score (cosine similarity, 0-1 range) */
  score: number;
  preview: string;
}

export interface SearchResult {
  schemaVersion: string;
  query: string;
  results: SearchResultItem[];
}

// --- Error ---

export interface ErrorResult {
  success: false;
  error: string;
}

type CLIResult = (Omit<SummaryResult, 'schemaVersion'> | Omit<QAResult, 'schemaVersion'> | ErrorResult);
type CLISearchResult = Omit<SearchResult, 'schemaVersion'>;

// ============================================================================
// DocSummarizer Client
// ============================================================================

export class DocSummarizer extends EventEmitter {
  private execInfo: ExecutableInfo;
  private configPath?: string;
  private model?: string;
  private timeout: number;

  constructor(options: DocSummarizerOptions = {}) {
    super();
    this.execInfo = findExecutable(options.executable);
    this.configPath = options.configPath;
    this.model = options.model;
    this.timeout = options.timeout ?? 300000;
  }

  /**
   * Get the resolved CLI path (for debugging).
   */
  getExecutablePath(): string {
    if (this.execInfo.isDotnetRun) {
      return `dotnet run --project ${this.execInfo.prependArgs[2]}`;
    }
    return this.execInfo.command;
  }

  // --------------------------------------------------------------------------
  // Summarization
  // --------------------------------------------------------------------------

  /**
   * Summarize a file.
   * 
   * Supports: .md, .txt, .pdf, .docx (PDF/DOCX require Docling service)
   */
  async summarizeFile(filePath: string, options: SummarizeOptions = {}): Promise<SummaryResult> {
    const args = this.buildToolArgs({ file: filePath, ...options });
    const result = await this.executeToolCommand<Omit<SummaryResult, 'schemaVersion'>>(args);
    return { schemaVersion: SCHEMA_VERSION, ...result };
  }

  /**
   * Summarize content from a URL.
   */
  async summarizeUrl(url: string, options: SummarizeOptions = {}): Promise<SummaryResult> {
    const args = this.buildToolArgs({ url, ...options });
    const result = await this.executeToolCommand<Omit<SummaryResult, 'schemaVersion'>>(args);
    return { schemaVersion: SCHEMA_VERSION, ...result };
  }

  /**
   * Summarize markdown content directly.
   * 
   * Creates a temp file, summarizes, then cleans up.
   */
  async summarizeMarkdown(markdown: string, options: SummarizeOptions = {}): Promise<SummaryResult> {
    const fs = await import('fs/promises');
    const os = await import('os');
    const path = await import('path');
    
    const tempFile = path.join(os.tmpdir(), `docsummarizer-${Date.now()}.md`);
    
    try {
      await fs.writeFile(tempFile, markdown, 'utf-8');
      return await this.summarizeFile(tempFile, options);
    } finally {
      await fs.unlink(tempFile).catch(() => {});
    }
  }

  // --------------------------------------------------------------------------
  // Question Answering
  // --------------------------------------------------------------------------

  /**
   * Ask a question about a file.
   * 
   * Returns an answer with supporting evidence from the document.
   */
  async askFile(filePath: string, question: string): Promise<QAResult> {
    const args = this.buildToolArgs({ file: filePath, ask: question });
    const result = await this.executeToolCommand<Omit<QAResult, 'schemaVersion'>>(args);
    return { schemaVersion: SCHEMA_VERSION, ...result };
  }

  /**
   * Ask a question about markdown content.
   */
  async askMarkdown(markdown: string, question: string): Promise<QAResult> {
    const fs = await import('fs/promises');
    const os = await import('os');
    const path = await import('path');
    
    const tempFile = path.join(os.tmpdir(), `docsummarizer-${Date.now()}.md`);
    
    try {
      await fs.writeFile(tempFile, markdown, 'utf-8');
      return await this.askFile(tempFile, question);
    } finally {
      await fs.unlink(tempFile).catch(() => {});
    }
  }

  // --------------------------------------------------------------------------
  // Semantic Search
  // --------------------------------------------------------------------------

  /**
   * Search for relevant segments in a document.
   * 
   * This searches within the provided document only (not a global index).
   * Each call extracts and embeds segments, then finds top matches.
   */
  async search(filePath: string, query: string, options: SearchOptions = {}): Promise<SearchResult> {
    const args = ['search', '--file', filePath, '--query', query, '--json'];
    if (options.topK) args.push('--top', String(options.topK));
    if (this.configPath) args.push('--config', this.configPath);
    
    const result = await this.executeCommand<CLISearchResult>(args);
    return { schemaVersion: SCHEMA_VERSION, ...result };
  }

  // --------------------------------------------------------------------------
  // Service Check
  // --------------------------------------------------------------------------

  /**
   * Check if the CLI is available and working.
   * 
   * Returns diagnostic info about available services.
   */
  async check(): Promise<boolean> {
    try {
      await this.executeRaw(['check']);
      return true;
    } catch {
      return false;
    }
  }

  /**
   * Get detailed diagnostic info.
   * 
   * Runs `docsummarizer check` and returns the raw output.
   */
  async diagnose(): Promise<{ available: boolean; output: string; executablePath: string }> {
    const executablePath = this.getExecutablePath();
    try {
      const output = await this.executeRaw(['check']);
      return { available: true, output, executablePath };
    } catch (err) {
      const output = err instanceof DocSummarizerError ? err.output : String(err);
      return { available: false, output, executablePath };
    }
  }

  // --------------------------------------------------------------------------
  // Internal
  // --------------------------------------------------------------------------

  private buildToolArgs(options: {
    file?: string;
    url?: string;
    query?: string;
    ask?: string;
    mode?: SummarizationMode;
  }): string[] {
    const args = ['tool'];

    if (options.file) args.push('--file', options.file);
    if (options.url) args.push('--url', options.url);
    if (options.query) args.push('--query', options.query);
    if (options.ask) args.push('--ask', options.ask);
    if (options.mode) args.push('--mode', options.mode);
    if (this.model) args.push('--model', this.model);
    if (this.configPath) args.push('--config', this.configPath);

    return args;
  }

  private async executeToolCommand<T>(args: string[]): Promise<T> {
    return this.executeCommand<T>(args);
  }

  private async executeCommand<T>(args: string[]): Promise<T> {
    const output = await this.executeRaw(args);
    
    try {
      const result = JSON.parse(output) as T | ErrorResult;
      
      if (result && typeof result === 'object' && 'success' in result && result.success === false) {
        throw new DocSummarizerError((result as ErrorResult).error, output);
      }
      
      return result as T;
    } catch (e) {
      if (e instanceof DocSummarizerError) throw e;
      throw new DocSummarizerError(`Failed to parse CLI output: ${output.slice(0, 200)}...`, output);
    }
  }

  private executeRaw(args: string[]): Promise<string> {
    return new Promise((resolve, reject) => {
      let fullArgs = [...this.execInfo.prependArgs, ...args];
      
      const useShell = process.platform === 'win32' && 
        !this.execInfo.command.includes('\\') && 
        !this.execInfo.command.includes('/');
      
      // Quote arguments with spaces when using shell
      if (useShell) {
        fullArgs = fullArgs.map(arg => 
          arg.includes(' ') && !arg.startsWith('"') ? `"${arg}"` : arg
        );
      }
      
      const proc = spawn(this.execInfo.command, fullArgs, {
        stdio: ['ignore', 'pipe', 'pipe'],
        shell: useShell,
      });

      let stdout = '';
      let stderr = '';

      proc.stdout.on('data', (data) => { stdout += data.toString(); });
      proc.stderr.on('data', (data) => { 
        stderr += data.toString();
        this.emit('stderr', data.toString());
      });

      const timer = setTimeout(() => {
        proc.kill();
        reject(new DocSummarizerError(`Timeout after ${this.timeout}ms`, stderr));
      }, this.timeout);

      proc.on('error', (err) => {
        clearTimeout(timer);
        reject(new DocSummarizerError(`Failed to spawn CLI: ${err.message}`, stderr));
      });

      proc.on('close', (code) => {
        clearTimeout(timer);
        if (code === 0) {
          resolve(stdout);
        } else {
          reject(new DocSummarizerError(`CLI exited with code ${code}`, stderr || stdout));
        }
      });
    });
  }
}

// ============================================================================
// Error Class
// ============================================================================

export class DocSummarizerError extends Error {
  /** Raw CLI output (stderr or stdout) */
  public readonly output: string;

  constructor(message: string, output: string) {
    super(message);
    this.name = 'DocSummarizerError';
    this.output = output;
  }
}

// ============================================================================
// Factory Function
// ============================================================================

/**
 * Create a DocSummarizer client.
 * 
 * @example
 * ```typescript
 * const doc = createDocSummarizer();
 * const { summary } = await doc.summarizeFile('./doc.md');
 * ```
 */
export function createDocSummarizer(options?: DocSummarizerOptions): DocSummarizer {
  return new DocSummarizer(options);
}

// ============================================================================
// Default Export
// ============================================================================

export default DocSummarizer;
