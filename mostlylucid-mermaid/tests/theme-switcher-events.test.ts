import { describe, it, expect, beforeEach, vi } from 'vitest';
import { initMermaid } from '../src/theme-switcher';

// Helper to flush microtasks and rAFs
async function flush() {
  await Promise.resolve();
  // give requestAnimationFrame callbacks a chance
  await new Promise((r) => setTimeout(r, 0));
}

describe('theme-switcher events and initial render', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    // Ensure no lingering dark classes from previous tests
    document.body.classList.remove('dark');
    document.documentElement.classList.remove('dark');
    (window as any).__themeState = undefined;
    // Reset localStorage facade used in setup
    (global as any).localStorage = {
      getItem: vi.fn(),
      setItem: vi.fn((k: string, v: string) => { (global as any).localStorage[k] = v; }),
      removeItem: vi.fn(),
      clear: vi.fn(),
    } as any;

    // reset mermaid mocks
    if (window.mermaid) {
      (window.mermaid.initialize as any)?.mockClear?.();
      (window.mermaid.run as any)?.mockClear?.();
    }
  });

  it('initial load renders with correct theme and passes nodes to mermaid.run', async () => {
    // Simulate dark via body class
    document.body.classList.add('dark');

    const d1 = document.createElement('div');
    d1.className = 'mermaid';
    d1.textContent = 'graph TD; A-->B';
    document.body.appendChild(d1);

    const d2 = document.createElement('pre');
    const code = document.createElement('code');
    code.className = 'language-mermaid';
    code.textContent = 'graph TD; X-->Y';
    d2.appendChild(code);
    document.body.appendChild(d2);

    await initMermaid();
    await flush();

    // initialize called with dark
    expect(window.mermaid?.initialize).toHaveBeenCalledWith(
      expect.objectContaining({ theme: 'dark' })
    );

    // run called with both nodes (normalized pre becomes a .mermaid too)
    expect(window.mermaid?.run).toHaveBeenCalledTimes(1);
    const call = (window.mermaid!.run as any).mock.calls[0][0];
    expect(Array.isArray(call.nodes)).toBe(true);
    expect(call.nodes.length).toBe(2);
  });

  it('re-renders with new theme on dark/light custom events', async () => {
    // Start in light (no dark classes)
    const d = document.createElement('div');
    d.className = 'mermaid';
    d.textContent = 'graph TD; A-->B';
    document.body.appendChild(d);

    await initMermaid();
    await flush();

    // first call should be default theme
    expect(window.mermaid?.initialize).toHaveBeenLastCalledWith(
      expect.objectContaining({ theme: 'default' })
    );

    // track current call count
    const initCalls = (window.mermaid!.initialize as any).mock.calls.length;
    const runCalls = (window.mermaid!.run as any).mock.calls.length;

    // switch to dark via event
    document.body.dispatchEvent(new Event('dark-theme-set'));
    await flush();

    expect((window.mermaid!.initialize as any).mock.calls.length).toBe(initCalls + 1);
    expect(window.mermaid?.initialize).toHaveBeenLastCalledWith(
      expect.objectContaining({ theme: 'dark' })
    );
    expect((window.mermaid!.run as any).mock.calls.length).toBe(runCalls + 1);

    // switch back to light
    document.body.dispatchEvent(new Event('light-theme-set'));
    await flush();

    expect(window.mermaid?.initialize).toHaveBeenLastCalledWith(
      expect.objectContaining({ theme: 'default' })
    );
  });

  it('responds to OS prefers-color-scheme changes via matchMedia(change)', async () => {
    // Prepare controllable matchMedia
    let listener: ((e: MediaQueryListEvent) => void) | null = null;
    const mql = {
      matches: false,
      media: '(prefers-color-scheme: dark)',
      addEventListener: vi.fn((_type: string, cb: (e: any) => void) => { listener = cb; }),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
      onchange: null,
    } as any;
    const mmSpy = vi.spyOn(window, 'matchMedia').mockImplementation(() => mql);

    const d = document.createElement('div');
    d.className = 'mermaid';
    d.textContent = 'sequenceDiagram\nA->>B: Hi';
    document.body.appendChild(d);

    await initMermaid();
    await flush();

    // Initially light/default
    expect(window.mermaid?.initialize).toHaveBeenLastCalledWith(
      expect.objectContaining({ theme: 'default' })
    );

    // Simulate OS change to dark
    expect(listener).toBeTypeOf('function');
    listener && (await listener({ matches: true } as any));
    await flush();

    expect(window.mermaid?.initialize).toHaveBeenLastCalledWith(
      expect.objectContaining({ theme: 'dark' })
    );

    mmSpy.mockRestore();
  });

  it('resets pre-processed diagrams back to original source before re-render', async () => {
    // Ensure matchMedia returns a safe object in this test
    (window as any).matchMedia = vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }));

    const d = document.createElement('div');
    d.className = 'mermaid';
    d.setAttribute('data-processed', 'true');
    d.setAttribute('data-original-code', 'graph TD; A-->B');
    d.innerHTML = '<svg></svg>'; // Simulate processed SVG content from earlier
    document.body.appendChild(d);

    await initMermaid();
    await flush();

    // After init, it should have been reset back to original text before re-render
    expect(d.textContent?.includes('A-->B')).toBe(true);
  });
});
