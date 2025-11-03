# JavaScript Unit Tests

Comprehensive unit tests for the Mostlylucid frontend JavaScript, focusing on theme switching and Mermaid diagram integration.

## Setup

Install test dependencies:

```bash
npm install
```

## Running Tests

```bash
# Run all tests once
npm test

# Run tests in watch mode (auto-rerun on file changes)
npm run test:watch

# Run tests with UI (browser-based test runner)
npm run test:ui

# Run tests with coverage report
npm run test:coverage
```

## What's Tested

### Theme System (`global.test.js`)

Tests the Alpine.js-based theme switching system:

- ✅ Theme initialization from localStorage
- ✅ Theme initialization from system preferences
- ✅ Theme switching (dark ↔ light)
- ✅ Stylesheet enabling/disabling
- ✅ CustomEvent dispatching (`dark-theme-set`, `light-theme-set`)
- ✅ DOM class updates (`dark`, `light`)

**Critical for:** Ensuring theme events fire correctly, which Mermaid depends on

### Mermaid Integration (`mermaid.test.js`)

Tests the complete Mermaid diagram rendering system:

#### Code Fence Normalization
- ✅ Converts Markdig output (`<pre><code class="language-mermaid">`) to Mermaid format
- ✅ Handles multiple diagrams
- ✅ Avoids re-processing already normalized blocks
- ✅ Handles orphaned elements gracefully

#### Data Attribute Management
- ✅ Saves original diagram code to `data-original-code`
- ✅ Preserves original code across theme changes
- ✅ Respects `data-processed` attribute

#### Theme Event Handling
- ✅ Listens for `dark-theme-set` and `light-theme-set` events
- ✅ Re-renders diagrams with correct theme
- ✅ Properly removes old event listeners before adding new
- ✅ Prevents duplicate event handler accumulation

#### OS Theme Preference
- ✅ Listens to `matchMedia` for OS theme changes
- ✅ Updates diagrams when system theme changes
- ✅ Cleans up old matchMedia listeners

#### Error Handling
- ✅ Gracefully handles Mermaid rendering errors
- ✅ Continues functioning after theme switch errors
- ✅ Handles missing elements

#### HTMX Integration
- ✅ Handles re-initialization with new content
- ✅ Processes dynamically loaded diagrams
- ✅ Maintains state across content swaps

## Test Environment

- **Framework:** Vitest (fast, modern, ESM-native)
- **DOM:** happy-dom (lightweight, fast DOM implementation)
- **Mocking:** Built-in Vitest mocks for browser APIs

## Coverage

View coverage reports after running:

```bash
npm run test:coverage
```

Coverage reports are generated in `./coverage/`:
- `coverage/index.html` - Interactive HTML report
- `coverage/coverage-final.json` - JSON data
- Terminal output shows summary

## CI Integration

These tests are designed to run in CI/CD pipelines:

```yaml
# Example GitHub Actions
- name: Run JS tests
  run: npm test

- name: Check coverage
  run: npm run test:coverage
```

## Debugging Tests

### With VS Code

1. Install "Vitest" extension
2. Tests appear in the Test Explorer
3. Set breakpoints and debug directly

### With Browser UI

```bash
npm run test:ui
```

Opens a browser interface at `http://localhost:51204/__vitest__/`

### Console Debugging

Tests log to console during development:
- "Mermaid initialized, waiting for theme event"
- "Dark theme set for Mermaid"
- "Light theme set for Mermaid"
- "OS theme changed for Mermaid: dark"

## Common Issues

### Tests fail with "mermaid is not defined"

The mermaid global is mocked in tests. If you're testing production bundles, ensure mermaid is properly exported.

### Event listeners not being called

Check that CustomEvents are dispatched to `document.body`, not other elements.

### Theme not updating diagrams

Ensure `themeInit()` and `themeSwitch()` dispatch events (see `global.js` changes).

## Adding New Tests

1. Create `*.test.js` or `*.spec.js` in `src/js/__tests__/`
2. Import functions from source files
3. Write tests using Vitest API
4. Run with `npm run test:watch`

Example:

```javascript
import { describe, it, expect } from 'vitest';
import { myFunction } from '../myModule';

describe('My Module', () => {
  it('should do something', () => {
    expect(myFunction()).toBe(expected);
  });
});
```

## Why These Tests Matter

The Mermaid integration has multiple failure points:

1. **Timing issues** - Theme must initialize before Mermaid
2. **Event accumulation** - Re-initialization can create duplicate listeners
3. **HTMX content swaps** - New diagrams must be detected and processed
4. **Theme changes** - Diagrams must update without losing state

These tests ensure all scenarios work correctly and catch regressions early.
