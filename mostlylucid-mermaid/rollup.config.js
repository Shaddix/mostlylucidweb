import resolve from '@rollup/plugin-node-resolve';
import commonjs from '@rollup/plugin-commonjs';
import typescript from '@rollup/plugin-typescript';
import terser from '@rollup/plugin-terser';

const banner = '/* @mostlylucid/mermaid-enhancements - Public Domain (Unlicense) */';

export default [
  // Bundled ESM (all dependencies included)
  {
    input: 'src/index.ts',
    output: {
      file: 'dist/index.bundle.js',
      format: 'esm',
      banner,
      sourcemap: true,
    },
    external: ['mermaid'], // Mermaid is a peer dependency, don't bundle it
    plugins: [
      resolve({
        browser: true,
      }),
      commonjs(),
      typescript({
        declaration: false,
        declarationMap: false,
      }),
    ],
  },
  // Bundled ESM minified
  {
    input: 'src/index.ts',
    output: {
      file: 'dist/index.bundle.min.js',
      format: 'esm',
      banner,
      sourcemap: true,
    },
    external: ['mermaid'],
    plugins: [
      resolve({
        browser: true,
      }),
      commonjs(),
      typescript({
        declaration: false,
        declarationMap: false,
      }),
      terser({
        compress: {
          drop_console: false,
          drop_debugger: true,
          pure_funcs: ['console.debug'],
          passes: 2,
        },
        mangle: {
          toplevel: false,
          reserved: ['init', 'configure', 'enhanceMermaidDiagrams', 'cleanupMermaidEnhancements', 'initMermaid'],
        },
        format: {
          comments: false,
          preamble: banner,
        },
      }),
    ],
  },
  // UMD build for browser <script> tags
  {
    input: 'src/index.ts',
    output: {
      file: 'dist/index.umd.js',
      format: 'umd',
      name: 'MermaidEnhancements',
      banner,
      sourcemap: true,
      globals: {
        mermaid: 'mermaid',
      },
    },
    external: ['mermaid'],
    plugins: [
      resolve({
        browser: true,
      }),
      commonjs(),
      typescript({
        declaration: false,
        declarationMap: false,
      }),
      terser({
        compress: {
          drop_console: false,
          drop_debugger: true,
          pure_funcs: ['console.debug'],
          passes: 2,
        },
        mangle: {
          toplevel: false,
          reserved: ['init', 'configure', 'enhanceMermaidDiagrams', 'cleanupMermaidEnhancements', 'initMermaid'],
        },
        format: {
          comments: false,
          preamble: banner,
        },
      }),
    ],
  },
];
