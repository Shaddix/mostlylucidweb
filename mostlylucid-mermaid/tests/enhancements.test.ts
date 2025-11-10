/**
 * Unit tests for enhancements module
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { enhanceMermaidDiagrams, cleanupMermaidEnhancements, configure, hideToolbar, showToolbar, toggleToolbar } from '../src/enhancements';

describe('enhancements', () => {
  beforeEach(() => {
    // Clear document body
    document.body.innerHTML = '';

    // Reset configuration to defaults
    configure({
      icons: {
        fullscreen: 'bx bx-fullscreen',
        zoomIn: 'bx bx-zoom-in',
        zoomOut: 'bx bx-zoom-out',
        reset: 'bx bx-reset',
        exportPng: 'bx bx-image',
        exportSvg: 'bx bx-code-alt'
      },
      controls: {
        fullscreen: true,
        zoomIn: true,
        zoomOut: true,
        reset: true,
        exportPng: true,
        exportSvg: true
      }
    });
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
            exportPng: false,
            exportSvg: false,
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
          exportPng: false,
          exportSvg: false,
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

    it('should have export buttons', () => {
      const exportPng = document.querySelector('[data-action="exportPng"]');
      const exportSvg = document.querySelector('[data-action="exportSvg"]');

      expect(exportPng).toBeTruthy();
      expect(exportSvg).toBeTruthy();
    });
  });

  describe('toolbar visibility functions', () => {
    beforeEach(() => {
      // Create two diagrams for testing
      for (let i = 0; i < 2; i++) {
        const diagram = document.createElement('div');
        diagram.className = 'mermaid';
        diagram.setAttribute('data-processed', 'true');

        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        diagram.appendChild(svg);
        document.body.appendChild(diagram);
      }

      enhanceMermaidDiagrams();
    });

    describe('hideToolbar', () => {
      it('should hide all toolbars when no diagram ID specified', () => {
        hideToolbar();

        const toolbars = document.querySelectorAll('.mermaid-controls');
        toolbars.forEach((toolbar) => {
          expect((toolbar as HTMLElement).style.display).toBe('none');
        });
      });

      it('should hide only specific diagram toolbar when ID provided', () => {
        const wrappers = document.querySelectorAll('.mermaid-wrapper');
        const firstId = wrappers[0].getAttribute('data-diagram-id');

        if (firstId) {
          hideToolbar(firstId);

          const firstControls = wrappers[0].querySelector('.mermaid-controls') as HTMLElement;
          const secondControls = wrappers[1].querySelector('.mermaid-controls') as HTMLElement;

          expect(firstControls.style.display).toBe('none');
          expect(secondControls.style.display).not.toBe('none');
        }
      });

      it('should not throw when called with non-existent ID', () => {
        expect(() => hideToolbar('non-existent-id')).not.toThrow();
      });
    });

    describe('showToolbar', () => {
      it('should show all toolbars when no diagram ID specified', () => {
        hideToolbar(); // Hide first
        showToolbar(); // Then show

        const toolbars = document.querySelectorAll('.mermaid-controls');
        toolbars.forEach((toolbar) => {
          expect((toolbar as HTMLElement).style.display).toBe('flex');
        });
      });

      it('should show only specific diagram toolbar when ID provided', () => {
        const wrappers = document.querySelectorAll('.mermaid-wrapper');
        const firstId = wrappers[0].getAttribute('data-diagram-id');

        hideToolbar(); // Hide all first

        if (firstId) {
          showToolbar(firstId); // Show only first

          const firstControls = wrappers[0].querySelector('.mermaid-controls') as HTMLElement;
          const secondControls = wrappers[1].querySelector('.mermaid-controls') as HTMLElement;

          expect(firstControls.style.display).toBe('flex');
          expect(secondControls.style.display).toBe('none');
        }
      });

      it('should not throw when called with non-existent ID', () => {
        expect(() => showToolbar('non-existent-id')).not.toThrow();
      });
    });

    describe('toggleToolbar', () => {
      it('should toggle all toolbars when no diagram ID specified', () => {
        const toolbars = document.querySelectorAll('.mermaid-controls');
        const initialDisplay = (toolbars[0] as HTMLElement).style.display;

        toggleToolbar();

        toolbars.forEach((toolbar) => {
          const newDisplay = (toolbar as HTMLElement).style.display;
          expect(newDisplay).not.toBe(initialDisplay);
        });
      });

      it('should toggle only specific diagram toolbar when ID provided', () => {
        const wrappers = document.querySelectorAll('.mermaid-wrapper');
        const firstId = wrappers[0].getAttribute('data-diagram-id');

        if (firstId) {
          const firstControls = wrappers[0].querySelector('.mermaid-controls') as HTMLElement;
          const secondControls = wrappers[1].querySelector('.mermaid-controls') as HTMLElement;

          const initialFirst = firstControls.style.display;
          const initialSecond = secondControls.style.display;

          toggleToolbar(firstId);

          expect(firstControls.style.display).not.toBe(initialFirst);
          expect(secondControls.style.display).toBe(initialSecond);
        }
      });

      it('should not throw when called with non-existent ID', () => {
        expect(() => toggleToolbar('non-existent-id')).not.toThrow();
      });
    });
  });
});
