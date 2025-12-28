#!/usr/bin/env node
/**
 * Quick test to verify executable detection works
 * 
 * Run with: node examples/test-detection.js (after build)
 * Or: npx tsx examples/test-detection.ts
 * 
 * For development without global install, set:
 *   DOCSUMMARIZER_PROJECT=/path/to/Mostlylucid.DocSummarizer.csproj
 */

import { DocSummarizer } from '../dist/index.mjs';
import { homedir } from 'os';
import { join } from 'path';
import { existsSync } from 'fs';

// For local development, auto-detect project path
function getProjectPath(): string | undefined {
  // Check env var first
  if (process.env.DOCSUMMARIZER_PROJECT) {
    return process.env.DOCSUMMARIZER_PROJECT;
  }
  
  // Try to find it relative to this repo
  const repoRoot = join(__dirname, '..', '..');
  const projectPath = join(repoRoot, 'Mostlylucid.DocSummarizer', 'Mostlylucid.DocSummarizer.csproj');
  
  if (existsSync(projectPath)) {
    return projectPath;
  }
  
  return undefined;
}

// Set env var if we found the project
const projectPath = getProjectPath();
if (projectPath) {
  console.log('Found project at:', projectPath);
  process.env.DOCSUMMARIZER_PROJECT = projectPath;
}

const doc = new DocSummarizer();

console.log('Platform:', process.platform);
console.log('Home:', homedir());
console.log('Resolved executable:', doc.getExecutablePath());

// Try to run check
doc.check().then(available => {
  console.log('CLI available:', available);
  if (available) {
    console.log('\nRunning quick summarize test...');
    return doc.summarizeMarkdown('# Test\n\nThis is a test document about machine learning.');
  }
}).then(result => {
  if (result) {
    console.log('Summary:', result.summary.slice(0, 200) + '...');
  }
}).catch(err => {
  console.log('Error:', err.message);
});
