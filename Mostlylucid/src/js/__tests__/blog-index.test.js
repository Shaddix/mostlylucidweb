import { describe, it, expect, vi } from 'vitest';

// The test verifies that language changes refresh calendar highlights
// and that HTMX swaps cause the date selection to be adjusted to the last post
// when posts fall outside the currently selected flatpickr range.

describe('blog-index integration (flatpickr + language + htmx)', () => {
  it('refreshes highlights on language change and adjusts selection when posts are outside range', async () => {
    // Prepare DOM
    document.body.innerHTML = `
      <div id="content">
        <input id="dateRange" type="text" />
        <select id="languageSelect">
          <option value="en">English</option>
          <option value="es">Spanish</option>
        </select>
        <select id="orderSelect"><option value="date_desc">Newest</option></select>
        <button id="clearDateFilter"></button>
        <div class="post" data-published-date="2025-12-05"></div>
      </div>
    `;

    // Spies for flatpickr instance methods
    const setDateSpy = vi.fn();
    const redrawSpy = vi.fn();
    const destroySpy = vi.fn();

    // Mock global flatpickr before importing the module
    global.flatpickr = (input, opts) => {
      // Create a fake instance and attach to input as the real code expects
      const fp = {
        currentYear: 2025,
        currentMonth: 10, // November (0-based +1 => 11)
        selectedDates: [new Date('2025-11-01'), new Date('2025-11-30')],
        redraw: redrawSpy,
        setDate: setDateSpy,
        destroy: destroySpy,
      };
      input._flatpickr = fp;

      // If there is a defaultDate in opts, mimic applying it
      if (opts && opts.defaultDate) {
        fp.selectedDates = opts.defaultDate.map(d => (typeof d === 'string' ? new Date(d + 'T00:00:00') : d));
      }

      return fp;
    };

    // Mock fetch to return a predictable set of highlight dates
    global.fetch = vi.fn((url) => {
      if (typeof url === 'string' && url.includes('/blog/calendar-days')) {
        return Promise.resolve({
          ok: true,
          json: () => Promise.resolve({ dates: ['2025-11-15', '2025-12-05'] })
        });
      }
      return Promise.resolve({ ok: false });
    });

    // Stub htmx to prevent actual Ajax calls
    global.htmx = { ajax: vi.fn() };

    // Import the module (it executes and attaches window.blogIndexInit)
    await import('../blog-index.js');

    // Initialize the blog index on the content root
    const root = document.querySelector('#content');
    expect(typeof window.blogIndexInit).toBe('function');
    window.blogIndexInit(root);

    // Simulate language change to 'es'
    const langSelect = document.querySelector('#languageSelect');
    langSelect.value = 'es';
    langSelect.dispatchEvent(new Event('change'));

    // Wait for async language handler to fetch and redraw
    await new Promise(resolve => setTimeout(resolve, 50));

    // Expect fetch called with language param and flatpickr redraw to be invoked
    const calledWithLang = global.fetch.mock.calls.some(call => call[0] && call[0].includes('language=es'));
    expect(calledWithLang).toBe(true);
    expect(redrawSpy).toHaveBeenCalled();

    // Now simulate HTMX afterSettle which should cause the selection to be adjusted
    document.body.dispatchEvent(new Event('htmx:afterSettle'));

    // Wait for the ensureSelectionCoversPosts timeout to run inside the module
    await new Promise(resolve => setTimeout(resolve, 200));

    // Expect flatpickr.setDate to have been called with the last post date
    expect(setDateSpy).toHaveBeenCalled();
    const callArgs = setDateSpy.mock.calls[0][0];
    // setDate called with an array of yyyy-mm-dd strings
    expect(callArgs).toBeInstanceOf(Array);
    expect(callArgs[0]).toBe('2025-12-05');
    expect(callArgs[1]).toBe('2025-12-05');
  });
});

