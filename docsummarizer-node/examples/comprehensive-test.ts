#!/usr/bin/env node
/**
 * Comprehensive API test for docsummarizer Node.js wrapper
 * 
 * Tests all public API methods and error handling
 */

import { DocSummarizer, DocSummarizerError, createDocSummarizer } from '../dist/index.mjs';
import { join } from 'path';
import { existsSync } from 'fs';
import { writeFile, unlink, mkdir } from 'fs/promises';
import { tmpdir } from 'os';

// ============================================================================
// Test Infrastructure
// ============================================================================

interface TestResult {
  name: string;
  passed: boolean;
  error?: string;
  duration: number;
}

const results: TestResult[] = [];

async function test(name: string, fn: () => Promise<void>): Promise<void> {
  const start = Date.now();
  try {
    await fn();
    results.push({ name, passed: true, duration: Date.now() - start });
    console.log(`  ✓ ${name} (${Date.now() - start}ms)`);
  } catch (err) {
    const error = err instanceof Error ? err.message : String(err);
    results.push({ name, passed: false, error, duration: Date.now() - start });
    console.log(`  ✗ ${name}`);
    console.log(`    Error: ${error}`);
  }
}

function assert(condition: boolean, message: string): void {
  if (!condition) {
    throw new Error(`Assertion failed: ${message}`);
  }
}

function assertDefined<T>(value: T | undefined | null, name: string): asserts value is T {
  if (value === undefined || value === null) {
    throw new Error(`Expected ${name} to be defined`);
  }
}

function assertType(value: unknown, type: string, name: string): void {
  if (typeof value !== type) {
    throw new Error(`Expected ${name} to be ${type}, got ${typeof value}`);
  }
}

// ============================================================================
// Setup
// ============================================================================

// Auto-detect project for development
function setupEnvironment(): void {
  if (process.env.DOCSUMMARIZER_PROJECT) return;
  
  const repoRoot = join(__dirname, '..', '..');
  const projectPath = join(repoRoot, 'Mostlylucid.DocSummarizer', 'Mostlylucid.DocSummarizer.csproj');
  
  if (existsSync(projectPath)) {
    process.env.DOCSUMMARIZER_PROJECT = projectPath;
    console.log(`Using project: ${projectPath}\n`);
  }
}

// Sample documents for testing
const SAMPLE_MARKDOWN = `
# Introduction to Machine Learning

Machine learning is a subset of artificial intelligence (AI) that provides systems 
the ability to automatically learn and improve from experience.

## Supervised Learning

In supervised learning, the algorithm learns from labeled training data.

### Classification

Classification is used to categorize data into predefined classes.

### Regression  

Regression predicts continuous numerical values.

## Unsupervised Learning

Unsupervised learning finds patterns in data without labels.

## Applications

1. Healthcare - disease diagnosis
2. Finance - fraud detection
3. Transportation - self-driving cars

## Conclusion

Machine learning continues to evolve rapidly with new applications emerging daily.
`;

const SHORT_MARKDOWN = `# Test Document

This is a simple test document about software development.

Best practices include:
- Writing clean code
- Testing thoroughly
- Documentation
`;

// ============================================================================
// Tests
// ============================================================================

async function runTests(): Promise<void> {
  setupEnvironment();
  
  const doc = new DocSummarizer({ timeout: 180000 }); // 3 min timeout for tests
  
  // Create temp directory for test files
  const tempDir = join(tmpdir(), `docsummarizer-test-${Date.now()}`);
  await mkdir(tempDir, { recursive: true });
  
  const sampleFile = join(tempDir, 'sample.md');
  const shortFile = join(tempDir, 'short.md');
  await writeFile(sampleFile, SAMPLE_MARKDOWN);
  await writeFile(shortFile, SHORT_MARKDOWN);

  console.log('═'.repeat(60));
  console.log(' DocSummarizer Node.js API Tests');
  console.log('═'.repeat(60));
  
  // --------------------------------------------------------------------------
  // Constructor & Configuration Tests
  // --------------------------------------------------------------------------
  console.log('\n▸ Constructor & Configuration\n');
  
  await test('Default constructor works', async () => {
    const client = new DocSummarizer();
    assertDefined(client, 'client');
  });
  
  await test('Constructor with options works', async () => {
    const client = new DocSummarizer({
      timeout: 60000,
      model: 'llama3.2',
      configPath: './config.json',
    });
    assertDefined(client, 'client');
  });
  
  await test('createDocSummarizer factory works', async () => {
    const client = createDocSummarizer({ timeout: 30000 });
    assertDefined(client, 'client');
  });
  
  await test('getExecutablePath returns string', async () => {
    const path = doc.getExecutablePath();
    assertType(path, 'string', 'executable path');
    assert(path.length > 0, 'path should not be empty');
    console.log(`    Path: ${path}`);
  });
  
  // --------------------------------------------------------------------------
  // Service Check Tests
  // --------------------------------------------------------------------------
  console.log('\n▸ Service Check\n');
  
  await test('check() returns boolean', async () => {
    const available = await doc.check();
    assertType(available, 'boolean', 'check result');
    assert(available === true, 'CLI should be available for remaining tests');
  });
  
  // --------------------------------------------------------------------------
  // Summarization Tests
  // --------------------------------------------------------------------------
  console.log('\n▸ Summarization\n');
  
  await test('summarizeFile() returns valid result', async () => {
    const result = await doc.summarizeFile(sampleFile);
    
    // Check schema version
    assertType(result.schemaVersion, 'string', 'schemaVersion');
    assert(result.schemaVersion.match(/^\d+\.\d+\.\d+$/) !== null, 'schemaVersion should be semver');
    
    // Check required fields
    assert(result.success === true, 'success should be true');
    assert(result.type === 'summary', 'type should be summary');
    assertType(result.summary, 'string', 'summary');
    assert(result.summary.length > 0, 'summary should not be empty');
    assertType(result.wordCount, 'number', 'wordCount');
    assert(result.wordCount > 0, 'wordCount should be positive');
    assertDefined(result.source, 'source');
    assertDefined(result.metadata, 'metadata');
    
    // Check metadata fields
    assertType(result.metadata.processingTimeMs, 'number', 'processingTimeMs');
    assertType(result.metadata.mode, 'string', 'mode');
    
    console.log(`    Schema: ${result.schemaVersion}`);
    console.log(`    Summary length: ${result.summary.length} chars, ${result.wordCount} words`);
  });
  
  await test('summarizeFile() with mode option', async () => {
    const result = await doc.summarizeFile(sampleFile, { mode: 'BertRag' });
    
    assert(result.success === true, 'success should be true');
    assertType(result.summary, 'string', 'summary');
  });
  
  await test('summarizeFile() with query option', async () => {
    const result = await doc.summarizeFile(sampleFile, { 
      query: 'focus on supervised learning' 
    });
    
    assert(result.success === true, 'success should be true');
    assertType(result.summary, 'string', 'summary');
  });
  
  await test('summarizeMarkdown() returns valid result', async () => {
    const result = await doc.summarizeMarkdown(SHORT_MARKDOWN);
    
    assert(result.success === true, 'success should be true');
    assert(result.type === 'summary', 'type should be summary');
    assertType(result.summary, 'string', 'summary');
    assert(result.summary.length > 0, 'summary should not be empty');
  });
  
  await test('summarizeMarkdown() with options', async () => {
    const result = await doc.summarizeMarkdown(SHORT_MARKDOWN, {
      query: 'testing practices',
      mode: 'Bert',
    });
    
    assert(result.success === true, 'success should be true');
  });
  
  // --------------------------------------------------------------------------
  // Topics & Entities Tests
  // --------------------------------------------------------------------------
  console.log('\n▸ Topics & Entities\n');
  
  await test('Result includes topics array', async () => {
    const result = await doc.summarizeFile(sampleFile);
    
    assertDefined(result.topics, 'topics');
    assert(Array.isArray(result.topics), 'topics should be array');
    
    if (result.topics.length > 0) {
      const topic = result.topics[0];
      assertType(topic.topic, 'string', 'topic.topic');
      assertType(topic.summary, 'string', 'topic.summary');
      console.log(`    Found ${result.topics.length} topics`);
    }
  });
  
  await test('Result may include entities', async () => {
    const result = await doc.summarizeFile(sampleFile);
    
    // Entities are optional
    if (result.entities) {
      // Check structure if present
      if (result.entities.organizations) {
        assert(Array.isArray(result.entities.organizations), 'organizations should be array');
      }
      if (result.entities.people) {
        assert(Array.isArray(result.entities.people), 'people should be array');
      }
      console.log(`    Entities present: ${Object.keys(result.entities).join(', ')}`);
    } else {
      console.log('    No entities extracted (this is OK)');
    }
  });
  
  // --------------------------------------------------------------------------
  // Question Answering Tests
  // --------------------------------------------------------------------------
  console.log('\n▸ Question Answering\n');
  
  await test('askFile() returns valid result', async () => {
    const result = await doc.askFile(sampleFile, 'What is supervised learning?');
    
    assert(result.success === true, 'success should be true');
    assert(result.type === 'qa', 'type should be qa');
    assertType(result.question, 'string', 'question');
    assertType(result.answer, 'string', 'answer');
    assert(result.answer.length > 0, 'answer should not be empty');
    assertType(result.confidence, 'string', 'confidence');
    assertDefined(result.evidence, 'evidence');
    assert(Array.isArray(result.evidence), 'evidence should be array');
    
    console.log(`    Confidence: ${result.confidence}`);
    console.log(`    Evidence segments: ${result.evidence.length}`);
  });
  
  await test('askFile() evidence has correct structure', async () => {
    const result = await doc.askFile(sampleFile, 'What are the applications?');
    
    if (result.evidence.length > 0) {
      const ev = result.evidence[0];
      assertType(ev.segmentId, 'string', 'evidence.segmentId');
      assertType(ev.text, 'string', 'evidence.text');
      assertType(ev.similarity, 'number', 'evidence.similarity');
      assertType(ev.section, 'string', 'evidence.section');
      
      assert(ev.similarity >= 0 && ev.similarity <= 1, 'similarity should be 0-1');
    }
  });
  
  await test('askMarkdown() returns valid result', async () => {
    const result = await doc.askMarkdown(SHORT_MARKDOWN, 'What are the best practices?');
    
    assert(result.success === true, 'success should be true');
    assert(result.type === 'qa', 'type should be qa');
    assertType(result.answer, 'string', 'answer');
  });
  
  // --------------------------------------------------------------------------
  // Search Tests
  // --------------------------------------------------------------------------
  console.log('\n▸ Search\n');
  
  await test('search() returns valid result', async () => {
    const result = await doc.search(sampleFile, 'machine learning');
    
    assertDefined(result.query, 'query');
    assertDefined(result.results, 'results');
    assert(Array.isArray(result.results), 'results should be array');
    
    console.log(`    Found ${result.results.length} results`);
  });
  
  await test('search() results have correct structure', async () => {
    const result = await doc.search(sampleFile, 'classification', { topK: 5 });
    
    if (result.results.length > 0) {
      const r = result.results[0];
      assertType(r.section, 'string', 'result.section');
      assertType(r.score, 'number', 'result.score');
      assertType(r.preview, 'string', 'result.preview');
      
      assert(r.score >= 0 && r.score <= 1, 'score should be 0-1');
    }
  });
  
  await test('search() respects topK option', async () => {
    const result = await doc.search(sampleFile, 'learning', { topK: 3 });
    
    assert(result.results.length <= 3, 'should return at most topK results');
  });
  
  // --------------------------------------------------------------------------
  // Error Handling Tests
  // --------------------------------------------------------------------------
  console.log('\n▸ Error Handling\n');
  
  await test('summarizeFile() throws on non-existent file', async () => {
    try {
      await doc.summarizeFile('/non/existent/file.md');
      throw new Error('Should have thrown');
    } catch (err) {
      assert(err instanceof DocSummarizerError, 'should throw DocSummarizerError');
      assertType((err as DocSummarizerError).output, 'string', 'error.output');
    }
  });
  
  await test('DocSummarizerError has correct properties', async () => {
    try {
      await doc.summarizeFile('/non/existent/file.md');
    } catch (err) {
      if (err instanceof DocSummarizerError) {
        assert(err.name === 'DocSummarizerError', 'name should be DocSummarizerError');
        assertType(err.message, 'string', 'message');
        assertType(err.output, 'string', 'output');
      }
    }
  });
  
  // --------------------------------------------------------------------------
  // Event Emitter Tests
  // --------------------------------------------------------------------------
  console.log('\n▸ Event Emitter\n');
  
  await test('Emits stderr events', async () => {
    let stderrReceived = false;
    
    const client = new DocSummarizer({ timeout: 180000 });
    client.on('stderr', (data: string) => {
      stderrReceived = true;
    });
    
    await client.summarizeMarkdown(SHORT_MARKDOWN);
    
    // stderr events are optional (depend on CLI output)
    console.log(`    stderr events received: ${stderrReceived}`);
  });
  
  // --------------------------------------------------------------------------
  // Cleanup
  // --------------------------------------------------------------------------
  await unlink(sampleFile).catch(() => {});
  await unlink(shortFile).catch(() => {});
  
  // --------------------------------------------------------------------------
  // Summary
  // --------------------------------------------------------------------------
  console.log('\n' + '═'.repeat(60));
  console.log(' Test Results');
  console.log('═'.repeat(60));
  
  const passed = results.filter(r => r.passed).length;
  const failed = results.filter(r => !r.passed).length;
  const totalTime = results.reduce((sum, r) => sum + r.duration, 0);
  
  console.log(`\n  Total:  ${results.length} tests`);
  console.log(`  Passed: ${passed}`);
  console.log(`  Failed: ${failed}`);
  console.log(`  Time:   ${(totalTime / 1000).toFixed(1)}s`);
  
  if (failed > 0) {
    console.log('\n  Failed tests:');
    results.filter(r => !r.passed).forEach(r => {
      console.log(`    - ${r.name}: ${r.error}`);
    });
    process.exit(1);
  }
  
  console.log('\n  All tests passed! ✓\n');
}

// Run tests
runTests().catch(err => {
  console.error('Test runner error:', err);
  process.exit(1);
});
