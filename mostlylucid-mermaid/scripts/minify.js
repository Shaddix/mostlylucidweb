#!/usr/bin/env node

/**
 * Minification script for production builds
 * Creates .min.js versions of all dist files
 */

const { minify } = require('terser');
const fs = require('fs').promises;
const path = require('path');

const distDir = path.join(__dirname, '..', 'dist');

// Terser options for optimal minification
const terserOptions = {
  compress: {
    drop_console: false, // Keep console logs for debugging
    drop_debugger: true,
    pure_funcs: ['console.debug'], // Remove console.debug calls
    passes: 2, // Multiple passes for better compression
  },
  mangle: {
    toplevel: false, // Don't mangle top-level names (for exports)
    reserved: ['init', 'configure', 'enhanceMermaidDiagrams', 'cleanupMermaidEnhancements', 'initMermaid'], // Keep public API names
  },
  format: {
    comments: false, // Remove all comments
    preamble: '/* @mostlylucid/mermaid-enhancements - MIT License */',
  },
  sourceMap: {
    filename: undefined,
    url: undefined,
  },
};

async function minifyFile(filePath) {
  const fileName = path.basename(filePath);
  const minFileName = fileName.replace('.js', '.min.js');
  const minFilePath = path.join(path.dirname(filePath), minFileName);

  console.log(`Minifying ${fileName}...`);

  try {
    const code = await fs.readFile(filePath, 'utf8');
    const result = await minify(code, terserOptions);

    if (result.error) {
      throw result.error;
    }

    await fs.writeFile(minFilePath, result.code, 'utf8');

    const originalSize = (await fs.stat(filePath)).size;
    const minifiedSize = (await fs.stat(minFilePath)).size;
    const savings = ((1 - minifiedSize / originalSize) * 100).toFixed(1);

    console.log(`  ✓ ${minFileName} created`);
    console.log(`    Original: ${(originalSize / 1024).toFixed(2)} KB`);
    console.log(`    Minified: ${(minifiedSize / 1024).toFixed(2)} KB`);
    console.log(`    Savings: ${savings}%`);

    return { fileName, originalSize, minifiedSize, savings };
  } catch (error) {
    console.error(`  ✗ Error minifying ${fileName}:`, error.message);
    throw error;
  }
}

async function minifyAll() {
  console.log('🗜️  Starting minification...\n');

  try {
    // Get all .js files in dist (exclude .min.js files)
    const files = await fs.readdir(distDir);
    const jsFiles = files
      .filter(file => file.endsWith('.js') && !file.endsWith('.min.js'))
      .map(file => path.join(distDir, file));

    if (jsFiles.length === 0) {
      console.log('⚠️  No JavaScript files found in dist/');
      console.log('   Run "npm run build" first');
      process.exit(1);
    }

    const results = [];
    for (const file of jsFiles) {
      const result = await minifyFile(file);
      results.push(result);
      console.log('');
    }

    // Summary
    console.log('📊 Minification Summary:');
    console.log('━'.repeat(60));

    let totalOriginal = 0;
    let totalMinified = 0;

    results.forEach(({ fileName, originalSize, minifiedSize, savings }) => {
      totalOriginal += originalSize;
      totalMinified += minifiedSize;
      console.log(`${fileName.padEnd(25)} ${savings}% smaller`);
    });

    console.log('━'.repeat(60));
    console.log(`Total Original: ${(totalOriginal / 1024).toFixed(2)} KB`);
    console.log(`Total Minified: ${(totalMinified / 1024).toFixed(2)} KB`);
    console.log(`Total Savings:  ${((1 - totalMinified / totalOriginal) * 100).toFixed(1)}%`);
    console.log('');
    console.log('✅ Minification complete!');
  } catch (error) {
    console.error('❌ Minification failed:', error.message);
    process.exit(1);
  }
}

minifyAll();
