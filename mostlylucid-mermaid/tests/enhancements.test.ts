/**
 * Unit tests for enhancements module
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { enhanceMermaidDiagrams, cleanupMermaidEnhancements, configure } from '../src/enhancements';

describe('enhancements', () => {
  beforeEach(() => {
    // Clear document body
    document.body.innerHTML = '';
  });

  afterEach(() => {
    cleanupMermaidEnhancements();
  });

  describe('configure', () => {
    it('should accept icon configuration', () => {
      expect(() => {
        configure({
          icons: {
            fullscreen: 'fas fa-expand',
            zoomIn: 'fas fa-plus',
          },
        });
      }).not.toThrow();
    });

    it('should accept controls configuration', () => {
      expect(() => {
        configure({
          controls: {
            export: false,
            pan: false,
          },
        });
      }).not.toThrow();
    });
  });

  describe('enhanceMermaidDiagrams', () => {
    it('should not throw when no diagrams exist', () => {
      expect(() => {
        enhanceMermaidDiagrams();
      }).not.toThrow();
    });

    it('should enhance processed mermaid diagrams', () => {
      // Create a mock processed diagram
      const diagram = document.createElement('div');
      diagram.className = 'mermaid';
      diagram.setAttribute('data-processed', 'true');

      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      svg.setAttribute('width', '100');
      svg.setAttribute('height', '100');
      diagram.appendChild(svg);

      document.body.appendChild(diagram);

      enhanceMermaidDiagrams();

      // Should wrap diagram in mermaid-wrapper
      const wrapper = document.querySelector('.mermaid-wrapper');
      expect(wrapper).toBeTruthy();

      // Should add controls
      const controls = document.querySelector('.mermaid-controls');
      expect(controls).toBeTruthy();
    });

    it('should add control buttons with default icons', () => {
      const diagram = document.createElement('div');
      diagram.className = 'mermaid';
      diagram.setAttribute('data-processed', 'true');

      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      diagram.appendChild(svg);
      document.body.appendChild(diagram);

      enhanceMermaidDiagrams();

      // Check for default Boxicon classes
      const fullscreenBtn = document.querySelector('[data-action="fullscreen"]');
      expect(fullscreenBtn?.className).toContain('bx-fullscreen');
    });

    it('should respect custom icon configuration', () => {
      configure({
        icons: {
          fullscreen: 'custom-icon-class',
        },
      });

      const diagram = document.createElement('div');
      diagram.className = 'mermaid';
      diagram.setAttribute('data-processed', 'true');

      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      diagram.appendChild(svg);
      document.body.appendChild(diagram);

      enhanceMermaidDiagrams();

      const fullscreenBtn = document.querySelector('[data-action="fullscreen"]');
      expect(fullscreenBtn?.className).toContain('custom-icon-class');
    });

    it('should hide controls when disabled', () => {
      configure({
        controls: {
          export: false,
        },
      });

      const diagram = document.createElement('div');
      diagram.className = 'mermaid';
      diagram.setAttribute('data-processed', 'true');

      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      diagram.appendChild(svg);
      document.body.appendChild(diagram);

      enhanceMermaidDiagrams();

      const exportPngBtn = document.querySelector('[data-action="exportPng"]');
      const exportSvgBtn = document.querySelector('[data-action="exportSvg"]');
      expect(exportPngBtn).toBeNull();
      expect(exportSvgBtn).toBeNull();
    });

    it('should not duplicate enhancements on already wrapped diagrams', () => {
      const diagram = document.createElement('div');
      diagram.className = 'mermaid';
      diagram.setAttribute('data-processed', 'true');

      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      diagram.appendChild(svg);
      document.body.appendChild(diagram);

      enhanceMermaidDiagrams();
      enhanceMermaidDiagrams();

      const wrappers = document.querySelectorAll('.mermaid-wrapper');
      expect(wrappers.length).toBe(1);
    });
  });

  describe('cleanupMermaidEnhancements', () => {
    it('should not throw when called multiple times', () => {
      expect(() => {
        cleanupMermaidEnhancements();
        cleanupMermaidEnhancements();
      }).not.toThrow();
    });

    it('should clean up pan-zoom instances', () => {
      const diagram = document.createElement('div');
      diagram.className = 'mermaid';
      diagram.setAttribute('data-processed', 'true');

      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      diagram.appendChild(svg);
      document.body.appendChild(diagram);

      enhanceMermaidDiagrams();

      expect(() => {
        cleanupMermaidEnhancements();
      }).not.toThrow();
    });
  });

  describe('control buttons', () => {
    beforeEach(() => {
      const diagram = document.createElement('div');
      diagram.className = 'mermaid';
      diagram.setAttribute('data-processed', 'true');

      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      diagram.appendChild(svg);
      document.body.appendChild(diagram);

      enhanceMermaidDiagrams();
    });

    it('should have fullscreen button', () => {
      const btn = document.querySelector('[data-action="fullscreen"]');
      expect(btn).toBeTruthy();
      expect(btn?.getAttribute('aria-label')).toBe('Fullscreen');
    });

    it('should have zoom buttons', () => {
      const zoomIn = document.querySelector('[data-action="zoomIn"]');
      const zoomOut = document.querySelector('[data-action="zoomOut"]');
      const reset = document.querySelector('[data-action="reset"]');

      expect(zoomIn).toBeTruthy();
      expect(zoomOut).toBeTruthy();
      expect(reset).toBeTruthy();
    });

    it('should have pan button', () => {
      const btn = document.querySelector('[data-action="pan"]');
      expect(btn).toBeTruthy();
      expect(btn?.getAttribute('aria-label')).toBe('Pan');
    });

    it('should have export buttons', () => {
      const exportPng = document.querySelector('[data-action="exportPng"]');
      const exportSvg = document.querySelector('[data-action="exportSvg"]');

      expect(exportPng).toBeTruthy();
      expect(exportSvg).toBeTruthy();
    });
  });
});
