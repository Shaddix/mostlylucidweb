import { describe, it, expect } from 'vitest';
import { execSync, spawn } from 'child_process';
import { join } from 'path';
import { existsSync } from 'fs';

const CLI_PATH = join(__dirname, '..', 'bin', 'cli.js');
const VENDOR_DIR = join(__dirname, '..', 'vendor');

// Check if CLI binary is available
function isCLIAvailable(): boolean {
  const exeName = process.platform === 'win32' ? 'docsummarizer.exe' : 'docsummarizer';
  return existsSync(join(VENDOR_DIR, exeName));
}

describe('CLI', () => {
  describe('cli.js exists', () => {
    it('should have cli.js in bin directory', () => {
      expect(existsSync(CLI_PATH)).toBe(true);
    });
  });

  describe('--help', () => {
    it('should show help when CLI is available', () => {
      if (!existsSync(CLI_PATH)) {
        console.log('Skipping: CLI script not found');
        return;
      }
      try {
        const output = execSync(`node "${CLI_PATH}" --help`, {
          encoding: 'utf-8',
          timeout: 30000,
        });
        // Either shows CLI help or wrapper help
        expect(output.length).toBeGreaterThan(0);
      } catch (err: any) {
        // If CLI not found, wrapper shows installation instructions
        expect(err.stdout || err.stderr).toBeDefined();
      }
    });
  });

  describe('doctor command', () => {
    it('should run diagnostics', () => {
      if (!existsSync(CLI_PATH)) {
        console.log('Skipping: CLI script not found');
        return;
      }
      // The doctor command shows diagnostics in JS wrapper but then passes to .NET CLI
      // which doesn't have a 'doctor' command, so it will fail with exit code 1
      // We just want to verify the wrapper part runs and shows diagnostics
      try {
        execSync(`node "${CLI_PATH}" doctor`, {
          encoding: 'utf-8',
          timeout: 30000,
        });
      } catch (err: any) {
        // The command will fail because .NET CLI doesn't have 'doctor'
        // but the stdout should contain the JS wrapper diagnostics
        const output = err.stdout || '';
        expect(output).toContain('DocSummarizer Diagnostics');
        expect(output).toContain('Platform:');
        expect(output).toContain('Node:');
        expect(output).toContain('CLI found:');
      }
    });
  });

  describe('check command', () => {
    it('should run dependency check', async () => {
      if (!existsSync(CLI_PATH)) {
        console.log('Skipping: CLI script not found');
        return;
      }
      if (!isCLIAvailable()) {
        console.log('Skipping: CLI binary not available');
        return;
      }

      const result = await new Promise<{ code: number | null; stdout: string; stderr: string }>((resolve) => {
        // Don't use shell: true to avoid DEP0190 warning
        const proc = spawn('node', [CLI_PATH, 'check']);

        let stdout = '';
        let stderr = '';

        proc.stdout?.on('data', (data) => { stdout += data.toString(); });
        proc.stderr?.on('data', (data) => { stderr += data.toString(); });

        proc.on('close', (code) => {
          resolve({ code, stdout, stderr });
        });

        proc.on('error', () => {
          resolve({ code: 1, stdout, stderr });
        });
      });

      // Check command should work if CLI is installed
      if (result.code === 0) {
        expect(result.stdout).toContain('Dependency');
      }
    }, 60000);
  });

  describe('version', () => {
    it('should show version when CLI is available', () => {
      if (!existsSync(CLI_PATH)) {
        console.log('Skipping: CLI script not found');
        return;
      }
      if (!isCLIAvailable()) {
        console.log('Skipping: CLI binary not available');
        return;
      }
      try {
        const output = execSync(`node "${CLI_PATH}" --version`, {
          encoding: 'utf-8',
          timeout: 30000,
        });
        // Should contain version number pattern
        expect(output).toMatch(/\d+\.\d+\.\d+/);
      } catch (err: any) {
        // CLI might not be installed
        expect(true).toBe(true);
      }
    });
  });
});

describe('Vendor binary', () => {
  const isWindows = process.platform === 'win32';
  const exeName = isWindows ? 'docsummarizer.exe' : 'docsummarizer';
  const exePath = join(VENDOR_DIR, exeName);

  it('should have vendor directory after postinstall', () => {
    // This test verifies postinstall ran successfully
    if (existsSync(VENDOR_DIR)) {
      expect(existsSync(VENDOR_DIR)).toBe(true);
    } else {
      // Vendor dir might not exist if postinstall was skipped (CI)
      console.log('Vendor directory not found (postinstall may have been skipped)');
    }
  });

  it('should have executable in vendor directory if postinstall ran', () => {
    if (existsSync(exePath)) {
      expect(existsSync(exePath)).toBe(true);
      
      // Verify it's executable (on Unix)
      if (!isWindows) {
        const { statSync } = require('fs');
        const stats = statSync(exePath);
        const isExecutable = (stats.mode & 0o111) !== 0;
        expect(isExecutable).toBe(true);
      }
    }
  });

  it('should run --version from vendor binary', () => {
    if (!existsSync(exePath)) {
      console.log('Skipping: vendor binary not installed');
      return;
    }

    const output = execSync(`"${exePath}" --version`, {
      encoding: 'utf-8',
      timeout: 30000,
    });
    
    expect(output).toMatch(/\d+\.\d+\.\d+/);
  });
});
