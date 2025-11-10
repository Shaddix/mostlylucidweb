// Mermaid theme/initialization helper - using npm package
'use strict';

// Import everything from the npm package
import { initMermaid as npmInitMermaid } from '@mostlylucid/mermaid-enhancements/bundle';
import '@mostlylucid/mermaid-enhancements/styles.css';

// Re-export initMermaid from the npm package
export { initMermaid } from '@mostlylucid/mermaid-enhancements/bundle';

// Preserve global for legacy calls
if (typeof window !== 'undefined') {
    window.initMermaid = npmInitMermaid;
}