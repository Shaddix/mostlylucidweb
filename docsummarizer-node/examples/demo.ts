#!/usr/bin/env npx ts-node
/**
 * DocSummarizer Node.js Demo
 * 
 * This demonstrates the main features of the docsummarizer package.
 * 
 * Prerequisites:
 *   dotnet tool install -g Mostlylucid.DocSummarizer
 * 
 * Run:
 *   npx ts-node demo.ts
 *   # or
 *   node demo.js
 */

import { DocSummarizer, DocSummarizerError } from '../src/index';

// Sample markdown document for testing
const SAMPLE_MARKDOWN = `
# Introduction to Machine Learning

Machine learning is a subset of artificial intelligence (AI) that provides systems 
the ability to automatically learn and improve from experience without being explicitly programmed.

## Key Concepts

### Supervised Learning

In supervised learning, the algorithm learns from labeled training data, and makes 
predictions based on that data. Common examples include:

- **Classification**: Categorizing data into predefined classes
- **Regression**: Predicting continuous values

### Unsupervised Learning

Unsupervised learning algorithms find patterns in data without pre-existing labels.
The algorithm must discover the hidden structure in unlabeled data.

## Applications

Machine learning has numerous applications across industries:

1. **Healthcare**: Disease diagnosis, drug discovery
2. **Finance**: Fraud detection, algorithmic trading
3. **Transportation**: Self-driving cars, route optimization
4. **Retail**: Recommendation systems, inventory management

## Challenges

Despite its potential, machine learning faces several challenges:

- Data quality and quantity requirements
- Model interpretability and explainability
- Bias and fairness concerns
- Computational resource demands

## Conclusion

Machine learning continues to evolve rapidly, with new techniques and applications 
emerging regularly. Understanding the fundamentals is essential for anyone working 
in technology today.
`;

// ============================================================================
// Helper Functions
// ============================================================================

function printHeader(title: string) {
  console.log('\n' + '='.repeat(60));
  console.log(` ${title}`);
  console.log('='.repeat(60) + '\n');
}

function printSection(title: string) {
  console.log(`\n--- ${title} ---\n`);
}

// ============================================================================
// Demo Functions
// ============================================================================

async function demoCheck(doc: DocSummarizer) {
  printHeader('1. Service Check');
  
  console.log(`Executable: ${doc.getExecutablePath()}`);
  console.log();
  
  const available = await doc.check();
  if (available) {
    console.log('DocSummarizer CLI is available and ready.');
  } else {
    console.log('DocSummarizer CLI not found.');
    console.log('Install with: dotnet tool install -g Mostlylucid.DocSummarizer');
    process.exit(1);
  }
}

async function demoSummarize(doc: DocSummarizer) {
  printHeader('2. Document Summarization');
  
  console.log('Summarizing sample markdown document...\n');
  
  try {
    const result = await doc.summarizeMarkdown(SAMPLE_MARKDOWN);
    
    printSection('Summary');
    console.log(result.summary);
    
    printSection('Topics');
    result.topics.forEach((topic, i) => {
      console.log(`${i + 1}. ${topic.topic}`);
      console.log(`   ${topic.summary}\n`);
    });
    
    if (result.entities) {
      printSection('Entities');
      if (result.entities.organizations?.length) {
        console.log(`Organizations: ${result.entities.organizations.join(', ')}`);
      }
      if (result.entities.locations?.length) {
        console.log(`Locations: ${result.entities.locations.join(', ')}`);
      }
    }
    
    printSection('Metadata');
    console.log(`Document ID: ${result.metadata.documentId}`);
    console.log(`Chunks processed: ${result.metadata.chunksProcessed}/${result.metadata.totalChunks}`);
    console.log(`Coverage score: ${(result.metadata.coverageScore * 100).toFixed(1)}%`);
    console.log(`Processing time: ${result.metadata.processingTimeMs}ms`);
    console.log(`Mode: ${result.metadata.mode}`);
    
  } catch (error) {
    if (error instanceof DocSummarizerError) {
      console.error('Summarization failed:', error.message);
    } else {
      throw error;
    }
  }
}

async function demoQuestionAnswering(doc: DocSummarizer) {
  printHeader('3. Question Answering');
  
  const questions = [
    'What is supervised learning?',
    'What are the main applications of machine learning?',
    'What challenges does machine learning face?',
  ];
  
  for (const question of questions) {
    console.log(`Q: ${question}`);
    
    try {
      const result = await doc.askMarkdown(SAMPLE_MARKDOWN, question);
      
      console.log(`A: ${result.answer}`);
      console.log(`   Confidence: ${result.confidence}`);
      
      if (result.evidence.length > 0) {
        console.log(`   Evidence: "${result.evidence[0].text.slice(0, 80)}..."`);
      }
      console.log();
      
    } catch (error) {
      if (error instanceof DocSummarizerError) {
        console.error(`   Error: ${error.message}\n`);
      } else {
        throw error;
      }
    }
  }
}

async function demoSearch(doc: DocSummarizer) {
  printHeader('4. Semantic Search');
  
  // For search, we need a file (can't use markdown directly)
  const fs = await import('fs/promises');
  const os = await import('os');
  const path = await import('path');
  
  const tempFile = path.join(os.tmpdir(), `docsummarizer-demo-${Date.now()}.md`);
  await fs.writeFile(tempFile, SAMPLE_MARKDOWN, 'utf-8');
  
  try {
    const queries = ['neural networks', 'data requirements', 'healthcare'];
    
    for (const query of queries) {
      console.log(`Search: "${query}"`);
      
      const results = await doc.search(tempFile, query, { topK: 3 });
      
      results.results.forEach((r, i) => {
        const scoreBar = '█'.repeat(Math.round(r.score * 10));
        console.log(`  ${i + 1}. [${r.score.toFixed(3)}] ${scoreBar}`);
        console.log(`     ${r.preview.slice(0, 70)}...`);
      });
      console.log();
    }
  } finally {
    await fs.unlink(tempFile).catch(() => {});
  }
}

async function demoFocusedSummary(doc: DocSummarizer) {
  printHeader('5. Focused Summarization');
  
  console.log('Summarizing with focus on "challenges and limitations"...\n');
  
  try {
    const result = await doc.summarizeMarkdown(SAMPLE_MARKDOWN, {
      query: 'challenges and limitations',
    });
    
    console.log(result.summary);
    console.log(`\nWord count: ${result.wordCount}`);
    
  } catch (error) {
    if (error instanceof DocSummarizerError) {
      console.error('Focused summarization failed:', error.message);
    } else {
      throw error;
    }
  }
}

// ============================================================================
// Main
// ============================================================================

async function main() {
  console.log('\n╔══════════════════════════════════════════════════════════╗');
  console.log('║           DocSummarizer Node.js Demo                     ║');
  console.log('╚══════════════════════════════════════════════════════════╝');
  
  // Create client
  const doc = new DocSummarizer({
    timeout: 120000, // 2 minutes for demo
  });
  
  // Listen for progress
  doc.on('stderr', (data: string) => {
    // Optionally show progress
    // process.stderr.write(data);
  });
  
  try {
    // Run demos
    await demoCheck(doc);
    await demoSummarize(doc);
    await demoQuestionAnswering(doc);
    await demoSearch(doc);
    await demoFocusedSummary(doc);
    
    printHeader('Demo Complete');
    console.log('All features demonstrated successfully!\n');
    
  } catch (error) {
    console.error('\nDemo failed:', error);
    process.exit(1);
  }
}

main();
