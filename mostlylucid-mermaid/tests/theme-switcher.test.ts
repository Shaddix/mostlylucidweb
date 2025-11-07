/**
 * Unit tests for theme-switcher module
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { initMermaid } from '../src/theme-switcher';

describe('theme-switcher', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    vi.clearAllMocks();
  });

  afterEach(() => {
    document.body.innerHTML = '';
  });

  describe('initMermaid', () => {
    it('should not throw when no mermaid diagrams exist', async () => {
      await expect(initMermaid()).resolves.not.toThrow();
    });

    it('should normalize code fence blocks', async () => {
      const pre = document.createElement('pre');
      const code = document.createElement('code');
      code.className = 'language-mermaid';
      code.textContent = 'graph TD\n  A --> B';
      pre.appendChild(code);
      document.body.appendChild(pre);

      await initMermaid();

      expect(pre.classList.contains('mermaid')).toBe(true);
      expect(pre.textContent).toBe('graph TD\n  A --> B');
    });

    it('should save original diagram code', async () => {
      const diagram = document.createElement('div');
      diagram.className = 'mermaid';
      diagram.textContent = 'graph LR\n  X --> Y';
      document.body.appendChild(diagram);

      await initMermaid();

      const saved = diagram.getAttribute('data-original-code');
      expect(saved).toBe('graph LR\n  X --> Y');
    });

    it('should initialize with dark theme when localStorage.theme is dark', async () => {
      localStorage.theme = 'dark';

      const diagram = document.createElement('div');
      diagram.className = 'mermaid';
      diagram.textContent = 'graph TD\n  A --> B';
      document.body.appendChild(diagram);

      await initMermaid();

      expect(window.mermaid?.initialize).toHaveBeenCalledWith(
        expect.objectContaining({ theme: 'dark' })
      );
    });

    it('should initialize with default theme when localStorage.theme is light', async () => {
      localStorage.theme = 'light';

      const diagram = document.createElement('div');
      diagram.className = 'mermaid';
      diagram.textContent = 'graph TD\n  A --> B';
      document.body.appendChild(diagram);

      await initMermaid();

      expect(window.mermaid?.initialize).toHaveBeenCalledWith(
        expect.objectContaining({ theme: 'default' })
      );
    });

    it('should respect window.__themeState', async () => {
      (window as any).__themeState = 'dark';

      const diagram = document.createElement('div');
      diagram.className = 'mermaid';
      diagram.textContent = 'graph TD\n  A --> B';
      document.body.appendChild(diagram);

      await initMermaid();

      expect(window.mermaid?.initialize).toHaveBeenCalledWith(
        expect.objectContaining({ theme: 'dark' })
      );
    });

    it('should detect dark mode from document element class', async () => {
      document.documentElement.classList.add('dark');

      const diagram = document.createElement('div');
      diagram.className = 'mermaid';
      diagram.textContent = 'graph TD\n  A --> B';
      document.body.appendChild(diagram);

      await initMermaid();

      expect(window.mermaid?.initialize).toHaveBeenCalledWith(
        expect.objectContaining({ theme: 'dark' })
      );

      document.documentElement.classList.remove('dark');
    });

    it('should add theme change event listeners', async () => {
      const diagram = document.createElement('div');
      diagram.className = 'mermaid';
      diagram.textContent = 'graph TD\n  A --> B';
      document.body.appendChild(diagram);

      await initMermaid();

      // Verify event listeners were added (can't directly test but shouldn't throw)
      expect(() => {
        document.body.dispatchEvent(new Event('dark-theme-set'));
        document.body.dispatchEvent(new Event('light-theme-set'));
      }).not.toThrow();
    });
  });
});
