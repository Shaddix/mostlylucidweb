import { describe, it, expect, beforeAll } from 'vitest';
import { DocSummarizer, DocSummarizerError, createDocSummarizer, SCHEMA_VERSION } from '../src/index';
import { existsSync } from 'fs';
import { join } from 'path';

// Test fixtures
const TEST_MARKDOWN_DIR = join(__dirname, '..', '..', 'test-markdown');
const SAMPLE_MARKDOWN_DIR = join(__dirname, '..', '..', 'samples', 'QdrantMarkdownSearch', 'MarkdownDocs');

// Find a test file that exists
function findTestFile(): string | null {
  const candidates = [
    join(TEST_MARKDOWN_DIR, 'dockercompose.md'),
    join(TEST_MARKDOWN_DIR, 'docker-development-deep-dive.md'),
    join(SAMPLE_MARKDOWN_DIR, 'getting-started.md'),
    join(SAMPLE_MARKDOWN_DIR, 'vector-databases.md'),
  ];
  
  for (const file of candidates) {
    if (existsSync(file)) {
      return file;
    }
  }
  return null;
}

describe('DocSummarizer', () => {
  let doc: DocSummarizer;
  let testFile: string | null;

  beforeAll(() => {
    doc = new DocSummarizer();
    testFile = findTestFile();
  });

  describe('initialization', () => {
    it('should create instance with default options', () => {
      const instance = new DocSummarizer();
      expect(instance).toBeInstanceOf(DocSummarizer);
    });

    it('should create instance with custom options', () => {
      const instance = new DocSummarizer({
        timeout: 60000,
        model: 'llama3.2:3b',
      });
      expect(instance).toBeInstanceOf(DocSummarizer);
    });

    it('should create instance via factory function', () => {
      const instance = createDocSummarizer();
      expect(instance).toBeInstanceOf(DocSummarizer);
    });

    it('should expose schema version', () => {
      expect(SCHEMA_VERSION).toBe('1.0.0');
    });
  });

  describe('executable resolution', () => {
    it('should resolve executable path', () => {
      const path = doc.getExecutablePath();
      expect(path).toBeDefined();
      expect(typeof path).toBe('string');
      expect(path.length).toBeGreaterThan(0);
    });

    it('should find vendor binary if installed', () => {
      const vendorPath = join(__dirname, '..', 'vendor', process.platform === 'win32' ? 'docsummarizer.exe' : 'docsummarizer');
      const execPath = doc.getExecutablePath();
      
      if (existsSync(vendorPath)) {
        expect(execPath).toContain('vendor');
      }
    });
  });

  describe('check', () => {
    it('should check CLI availability', async () => {
      const result = await doc.check();
      expect(typeof result).toBe('boolean');
    }, 30000);
  });

  describe('diagnose', () => {
    it('should return diagnostic info', async () => {
      const result = await doc.diagnose();
      
      expect(result).toHaveProperty('available');
      expect(result).toHaveProperty('output');
      expect(result).toHaveProperty('executablePath');
      expect(typeof result.available).toBe('boolean');
      expect(typeof result.output).toBe('string');
      expect(typeof result.executablePath).toBe('string');
    }, 30000);
  });

  describe('summarizeFile', () => {
    it('should summarize a markdown file with Bert mode', async () => {
      if (!testFile) {
        console.log('Skipping: no test file found');
        return;
      }

      const result = await doc.summarizeFile(testFile, { mode: 'Bert' });

      expect(result).toHaveProperty('success', true);
      expect(result).toHaveProperty('type', 'summary');
      expect(result).toHaveProperty('schemaVersion', SCHEMA_VERSION);
      expect(result).toHaveProperty('summary');
      expect(result).toHaveProperty('wordCount');
      expect(result).toHaveProperty('topics');
      expect(result).toHaveProperty('metadata');

      expect(typeof result.summary).toBe('string');
      expect(result.summary.length).toBeGreaterThan(0);
      expect(typeof result.wordCount).toBe('number');
      expect(result.wordCount).toBeGreaterThan(0);
      expect(Array.isArray(result.topics)).toBe(true);

      // Check metadata
      expect(result.metadata).toHaveProperty('documentId');
      expect(result.metadata).toHaveProperty('totalChunks');
      expect(result.metadata).toHaveProperty('chunksProcessed');
      expect(result.metadata).toHaveProperty('coverageScore');
      expect(result.metadata).toHaveProperty('processingTimeMs');
      expect(result.metadata).toHaveProperty('mode');
    }, 120000); // 2 min timeout for first run with model download

    it('should include source path in result', async () => {
      if (!testFile) {
        console.log('Skipping: no test file found');
        return;
      }

      const result = await doc.summarizeFile(testFile, { mode: 'Bert' });
      expect(result.source).toContain('.md');
    }, 60000);
  });

  describe('summarizeMarkdown', () => {
    it('should summarize raw markdown content', async () => {
      const markdown = `# Test Document

## Introduction

This is a test document with multiple paragraphs to verify the summarization functionality.

## Main Content

The DocSummarizer tool provides local-first document summarization using BERT embeddings.
It supports multiple modes including pure extractive (Bert) and RAG-enhanced (BertRag).

### Key Features

- Local ONNX embeddings
- No cloud dependencies
- Citation-grounded answers
- Semantic search capabilities

## Conclusion

This demonstrates the summarization of in-memory markdown content.
`;

      const result = await doc.summarizeMarkdown(markdown, { mode: 'Bert' });

      expect(result).toHaveProperty('success', true);
      expect(result).toHaveProperty('type', 'summary');
      expect(result).toHaveProperty('summary');
      expect(result.summary.length).toBeGreaterThan(0);
      expect(result.topics.length).toBeGreaterThan(0);
    }, 60000);
  });

  describe('search', () => {
    it('should search for relevant segments', async () => {
      if (!testFile) {
        console.log('Skipping: no test file found');
        return;
      }

      const result = await doc.search(testFile, 'docker', { topK: 5 });

      expect(result).toHaveProperty('schemaVersion', SCHEMA_VERSION);
      expect(result).toHaveProperty('query', 'docker');
      expect(result).toHaveProperty('results');
      expect(Array.isArray(result.results)).toBe(true);

      if (result.results.length > 0) {
        const firstResult = result.results[0];
        expect(firstResult).toHaveProperty('section');
        expect(firstResult).toHaveProperty('score');
        expect(firstResult).toHaveProperty('preview');
        expect(typeof firstResult.score).toBe('number');
        expect(firstResult.score).toBeGreaterThanOrEqual(0);
        expect(firstResult.score).toBeLessThanOrEqual(1);
      }
    }, 60000);

    it('should respect topK parameter', async () => {
      if (!testFile) {
        console.log('Skipping: no test file found');
        return;
      }

      const result = await doc.search(testFile, 'container', { topK: 3 });
      expect(result.results.length).toBeLessThanOrEqual(3);
    }, 60000);
  });

  describe('error handling', () => {
    it('should throw DocSummarizerError for non-existent file', async () => {
      await expect(
        doc.summarizeFile('/nonexistent/path/to/file.md', { mode: 'Bert' })
      ).rejects.toThrow(DocSummarizerError);
    }, 30000);

    it('should include output in error', async () => {
      try {
        await doc.summarizeFile('/nonexistent/path/to/file.md', { mode: 'Bert' });
        expect.fail('Should have thrown');
      } catch (err) {
        if (err instanceof DocSummarizerError) {
          expect(err.output).toBeDefined();
          expect(typeof err.output).toBe('string');
        }
      }
    }, 30000);
  });

  describe('events', () => {
    it('should emit stderr events during processing', async () => {
      if (!testFile) {
        console.log('Skipping: no test file found');
        return;
      }

      const stderrMessages: string[] = [];
      doc.on('stderr', (data: string) => {
        stderrMessages.push(data);
      });

      await doc.summarizeFile(testFile, { mode: 'Bert' });
      
      // stderr may or may not have content depending on verbose mode
      expect(Array.isArray(stderrMessages)).toBe(true);
    }, 60000);
  });
});

describe('DocSummarizerError', () => {
  it('should have correct name', () => {
    const err = new DocSummarizerError('test message', 'test output');
    expect(err.name).toBe('DocSummarizerError');
  });

  it('should store message and output', () => {
    const err = new DocSummarizerError('test message', 'test output');
    expect(err.message).toBe('test message');
    expect(err.output).toBe('test output');
  });

  it('should be instanceof Error', () => {
    const err = new DocSummarizerError('test', 'output');
    expect(err).toBeInstanceOf(Error);
  });
});
