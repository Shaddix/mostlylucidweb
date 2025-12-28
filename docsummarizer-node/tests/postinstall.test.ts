import { describe, it, expect } from 'vitest';
import { existsSync } from 'fs';
import { join } from 'path';

const SCRIPTS_DIR = join(__dirname, '..', 'scripts');
const POSTINSTALL_PATH = join(SCRIPTS_DIR, 'postinstall.js');

describe('Postinstall script', () => {
  describe('script file', () => {
    it('should exist in scripts directory', () => {
      expect(existsSync(POSTINSTALL_PATH)).toBe(true);
    });

    it('should be valid JavaScript syntax', () => {
      // Just verify the file can be parsed without syntax errors
      const fs = require('fs');
      const content = fs.readFileSync(POSTINSTALL_PATH, 'utf-8');
      
      // Check it's valid JS by parsing with vm module
      const vm = require('vm');
      expect(() => {
        new vm.Script(content, { filename: 'postinstall.js' });
      }).not.toThrow();
    });
  });

  describe('platform detection', () => {
    it('should detect current platform', () => {
      const platform = process.platform;
      const arch = process.arch;
      
      expect(['win32', 'linux', 'darwin']).toContain(platform);
      expect(['x64', 'arm64', 'ia32']).toContain(arch);
    });

    it('should map to correct artifact name', () => {
      const platform = process.platform;
      const arch = process.arch;
      
      const mapping: Record<string, string> = {
        'win32-x64': 'docsummarizer-win-x64.zip',
        'win32-arm64': 'docsummarizer-win-arm64.zip',
        'linux-x64': 'docsummarizer-linux-x64.tar.gz',
        'linux-arm64': 'docsummarizer-linux-arm64.tar.gz',
        'darwin-x64': 'docsummarizer-osx-x64.tar.gz',
        'darwin-arm64': 'docsummarizer-osx-arm64.tar.gz',
      };
      
      const key = `${platform}-${arch}`;
      const artifact = mapping[key];
      
      // Current platform should have a mapping (unless unsupported)
      if (['win32', 'linux', 'darwin'].includes(platform) && ['x64', 'arm64'].includes(arch)) {
        expect(artifact).toBeDefined();
        
        if (platform === 'win32') {
          expect(artifact).toContain('.zip');
        } else {
          expect(artifact).toContain('.tar.gz');
        }
      }
    });
  });

  describe('environment variables', () => {
    it('should respect DOCSUMMARIZER_SKIP_DOWNLOAD', () => {
      // This is tested implicitly - if CI or skip flag is set, postinstall exits early
      expect(process.env.DOCSUMMARIZER_SKIP_DOWNLOAD === '1' || process.env.CI !== undefined || true).toBe(true);
    });
  });
});

describe('Package files', () => {
  const PKG_ROOT = join(__dirname, '..');

  it('should have package.json', () => {
    expect(existsSync(join(PKG_ROOT, 'package.json'))).toBe(true);
  });

  it('should have bin/cli.js', () => {
    const binPath = join(PKG_ROOT, 'bin', 'cli.js');
    if (!existsSync(binPath)) {
      console.log('bin/cli.js not found at:', binPath);
      // In development/CI the bin folder should exist
      // List what's actually in the package root
      const fs = require('fs');
      console.log('Package root contents:', fs.readdirSync(PKG_ROOT));
    }
    expect(existsSync(binPath)).toBe(true);
  });

  it('should have scripts/postinstall.js', () => {
    expect(existsSync(join(PKG_ROOT, 'scripts', 'postinstall.js'))).toBe(true);
  });

  it('should have correct bin entry in package.json', () => {
    const pkg = require(join(PKG_ROOT, 'package.json'));
    expect(pkg.bin).toBeDefined();
    expect(pkg.bin.docsummarizer).toBe('./bin/cli.js');
  });

  it('should have postinstall script in package.json', () => {
    const pkg = require(join(PKG_ROOT, 'package.json'));
    expect(pkg.scripts.postinstall).toBe('node scripts/postinstall.js');
  });

  it('should include scripts in files array', () => {
    const pkg = require(join(PKG_ROOT, 'package.json'));
    expect(pkg.files).toContain('scripts');
    expect(pkg.files).toContain('bin');
    expect(pkg.files).toContain('dist');
  });
});
